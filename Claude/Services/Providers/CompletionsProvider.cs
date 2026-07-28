using ClaudeLauncher.Config;
using ClaudeLauncher.Providers;
using System.IO;

namespace ClaudeLauncher.Providers;

/// <summary>
/// Custom API provider for OpenAI-compatible endpoints (OpenRouter, etc.).
///
/// Claude Code only speaks Anthropic's Messages API on ANTHROPIC_BASE_URL
/// — it has no OpenAI-format mode. A bundled local Node.js proxy
/// (Resources/CompletionsProxy) bridges the gap: Claude Code talks
/// Anthropic format to the proxy, which translates each request into
/// OpenAI Chat Completions format before forwarding to the configured
/// endpoint (and translates the streamed response back).
///
/// The proxy reuses the same gateway auto-start/health-check pipeline
/// that Kiro's OmniRoute already relies on (see GatewayService).
///
/// BaseUrl/AuthToken point at the real upstream endpoint/key: they are
/// used pre-launch by the Settings window's model list and by the
/// credit/quota probe. Only GetEnvironmentVariables() (used when
/// actually launching Claude Code) points at the local proxy instead.
/// </summary>
public sealed class CompletionsProvider : IProvider
{
    private readonly AppConfig _config;

    /// <summary>Folder the bundled Node proxy was copied to next to the launcher executable.</summary>
    public static string ProxyDirectory { get; } =
        Path.Combine(AppContext.BaseDirectory, "CompletionsProxy");

    /// <summary>Entry point of the bundled Node proxy.</summary>
    public static string ProxyScriptPath { get; } =
        Path.Combine(ProxyDirectory, "server.js");

    /// <summary>The proxy's bundled dependencies — without them Node exits immediately.</summary>
    public static string ProxyModulesPath { get; } =
        Path.Combine(ProxyDirectory, "node_modules", "express");

    /// <summary>
    /// Placeholder credential Claude Code sends to the local proxy. It must
    /// stay "copilot-proxy": the proxy pre-approves exactly this value in
    /// ~/.claude.json (customApiKeyResponses), which is what stops Claude
    /// Code from interrupting the launch with a "Do you want to use this API
    /// key?" prompt. The proxy itself never validates it.
    /// </summary>
    public const string ProxyPlaceholderKey = "copilot-proxy";

    public CompletionsProvider(AppConfig config)
    {
        _config = config;
    }

    public string Id => "completions";

    public string DisplayName => "Custom API";

    public string Executable => _config.Claude.Executable;

    /// <summary>The upstream API base URL — used for model listing / credit checks, not for launching Claude.</summary>
    public string BaseUrl => _config.Completions.BaseUrl;

    public string Model => _config.Completions.Model;

    /// <summary>The upstream API key — used for model listing / credit checks, not for launching Claude.</summary>
    public string? AuthToken => _config.Completions.ApiKey;

    /// <summary>
    /// The local translation proxy gateway. Executable/Arguments always
    /// point at the bundled proxy script (computed here, never persisted
    /// to config.json), while BaseUrl/HealthEndpoint/AutoStart/timeout
    /// stay user-tunable via AppConfig.CompletionsGateway.
    /// </summary>
    public GatewayConfig Gateway
    {
        get
        {
            var configured = _config.CompletionsGateway;

            return new GatewayConfig
            {
                BaseUrl = string.IsNullOrWhiteSpace(configured.BaseUrl)
                    ? "http://localhost:4242"
                    : configured.BaseUrl,
                HealthEndpoint = string.IsNullOrWhiteSpace(configured.HealthEndpoint)
                    ? "/health"
                    : configured.HealthEndpoint,
                AutoStart = configured.AutoStart,
                StartupTimeoutSeconds = configured.StartupTimeoutSeconds > 0
                    ? configured.StartupTimeoutSeconds
                    : 25,
                Executable = "node",
                Arguments = $"\"{ProxyScriptPath}\" --no-launch",
                WorkingDirectory = ProxyDirectory,

                // "authenticated" only ever appears in our own proxy's
                // /health payload, so a stranger already sitting on port 4242
                // can no longer be mistaken for a healthy gateway.
                HealthResponseMustContain = "authenticated",

                // A proxy left over from an earlier session keeps serving the
                // endpoint/key/model it was started with. Comparing these two
                // values against its /health payload is what makes a change in
                // Settings actually take effect.
                ExpectedUpstream = NormalizeUpstreamUrl(_config.Completions.BaseUrl),
                ExpectedModel = NormalizeModelId(Model),
                ShutdownEndpoint = "/shutdown"
            };
        }
    }

    public bool RequiresGateway => true;

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(_config.Completions.BaseUrl) &&
        !string.IsNullOrWhiteSpace(_config.Completions.ApiKey);

    /// <summary>
    /// Environment for Claude Code itself: points at the local proxy, not
    /// at the upstream endpoint. The proxy authenticates upstream on its
    /// own (see GatewayEnvironmentVariables), so Claude Code only needs a
    /// placeholder token here — the proxy does not check it.
    /// </summary>
    public IReadOnlyDictionary<string, string> GetEnvironmentVariables()
    {
        var baseUrl = Gateway.BaseUrl;

        var environment = new Dictionary<string, string>
        {
            ["ANTHROPIC_BASE_URL"] = baseUrl,

            // Claude Code reads ANTHROPIC_API_URL in some code paths and
            // ANTHROPIC_BASE_URL in others; setting both guarantees no
            // request ever escapes to api.anthropic.com.
            ["ANTHROPIC_API_URL"] = baseUrl,

            // ANTHROPIC_MODEL is deliberately NOT set for this provider.
            //
            // Every request is pinned to the configured model by the proxy
            // (FORCE_MODEL), so the only thing this variable still influenced
            // was Claude Code's own guess about which model family it is
            // talking to — and that guess is made by string-matching the id.
            // Feeding it catalog ids produced false deprecation banners
            // ("Claude Sonnet 4 retired June 15, 2026", "Claude Opus 4
            // retired …") even though the request itself was routed to a
            // current model. Leaving it unset removes the wrong banner without
            // changing a single byte of what goes on the wire.

            ["ANTHROPIC_AUTH_TOKEN"] = ProxyPlaceholderKey,

            // Deliberately empty, not set to the placeholder: Claude Code
            // warns ("Both ANTHROPIC_AUTH_TOKEN and ANTHROPIC_API_KEY set ·
            // auth may not work as expected") whenever both are present, and
            // an empty value also clears a real ANTHROPIC_API_KEY the user
            // may have in their own environment, which would otherwise leak
            // into the session. .NET treats "" as "remove this variable",
            // which is exactly the wanted behaviour.
            ["ANTHROPIC_API_KEY"] = string.Empty
        };

        ProviderEnvironment.ApplyPrivacy(environment, _config.Launcher);

        return environment;
    }

    /// <summary>
    /// Environment for the proxy process itself: the real API key, which
    /// upstream URL to forward to, which port to listen on, and NO_LAUNCH=1
    /// so the proxy doesn't try to spawn Claude Code a second time (our own
    /// ClaudeService does that).
    /// </summary>
    public IReadOnlyDictionary<string, string>? GatewayEnvironmentVariables
    {
        get
        {
            var environment = new Dictionary<string, string>
            {
                ["NO_LAUNCH"] = "1",
                ["PORT"] = ExtractPort(Gateway.BaseUrl),

                // The launcher health-checks (and points Claude Code at) one
                // specific port, so the proxy must not quietly fall back to
                // the next free one — that mismatch looked exactly like "the
                // gateway never started".
                ["PORT_STRICT"] = "1",
                ["MODEL"] = NormalizeModelId(Model),

                // Every request is served with the model picked in Settings,
                // exactly as the upstream catalog spells it (including any
                // "vendor/" prefix). Without this the proxy would translate
                // whatever id Claude Code happens to ask for — including its
                // internal Haiku id for background tasks — and the upstream
                // would answer "model not found".
                ["FORCE_MODEL"] = NormalizeModelId(Model),
                ["COMPLETIONS_URL"] = NormalizeUpstreamUrl(_config.Completions.BaseUrl)
            };

            if (!string.IsNullOrWhiteSpace(_config.Completions.ApiKey))
            {
                environment["COMPLETIONS_API_KEY"] = _config.Completions.ApiKey;
            }

            return environment;
        }
    }

    /// <summary>
    /// The model id is used exactly as the provider's own "/models" endpoint
    /// reported it — we only trim whitespace.
    ///
    /// Bug history: this method used to strip a leading "vendor/" prefix
    /// ("anthropic/claude-sonnet-4" -> "claude-sonnet-4") as a defensive
    /// measure for OpenRouter-style catalogs. That was backwards: the model
    /// list shown in Settings comes from the very same endpoint the request
    /// is later sent to, so whatever it returned is by definition the id that
    /// endpoint expects back. For OpenRouter-compatible gateways the prefix
    /// is mandatory, and stripping it turned a valid selection into a
    /// "model not found" error.
    /// </summary>
    internal static string NormalizeModelId(string modelId)
    {
        return string.IsNullOrWhiteSpace(modelId)
            ? modelId
            : modelId.Trim();
    }

    /// <summary>
    /// The proxy's own COMPLETIONS_URL expects a bare origin (it appends
    /// "/api/v1/..." itself). The Settings field also accepts a full API
    /// base such as "https://openrouter.ai/api/v1" (used directly for model
    /// listing), so strip a trailing "/api/v1" or "/api/v1/" when handing
    /// the value to the proxy.
    /// </summary>
    internal static string NormalizeUpstreamUrl(string baseUrl)
    {
        if (string.IsNullOrWhiteSpace(baseUrl))
            return baseUrl;

        var url = baseUrl.Trim().TrimEnd('/');

        if (url.EndsWith("/api/v1", StringComparison.OrdinalIgnoreCase))
        {
            url = url[..^"/api/v1".Length];
        }

        return url;
    }

    /// <summary>Extracts the port from a "http://localhost:PORT" style URL, defaulting to 4242.</summary>
    internal static string ExtractPort(string baseUrl)
    {
        if (Uri.TryCreate(baseUrl, UriKind.Absolute, out var uri) && uri.Port > 0)
        {
            return uri.Port.ToString();
        }

        return "4242";
    }
}
