using ClaudeLauncher.Config;
using ClaudeLauncher.Providers;

namespace ClaudeLauncher.Providers;

public sealed class KiroProvider : IProvider
{
    private readonly AppConfig _config;

    public KiroProvider(AppConfig config)
    {
        _config = config;
    }

    public string Id => "kiro";

    public string DisplayName => "Kiro";

    public string Executable => _config.Claude.Executable;

    public string BaseUrl => _config.Gateway.BaseUrl;

    public string Model => _config.Claude.Model;

    public string? AuthToken => _config.Claude.Token;

    public GatewayConfig Gateway => _config.Gateway;

    public bool RequiresGateway => true;

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(_config.Gateway.BaseUrl) &&
        !string.IsNullOrWhiteSpace(_config.Claude.Token);

    public IReadOnlyDictionary<string, string>? GatewayEnvironmentVariables => null;

    public IReadOnlyDictionary<string, string> GetEnvironmentVariables()
    {
        var environment = new Dictionary<string, string>
        {
            ["ANTHROPIC_BASE_URL"] = BaseUrl,

            // OmniRoute routes by model id, so unlike the Completions proxy
            // (which pins the model itself) this one has to be passed through.
            ["ANTHROPIC_MODEL"] = Model,

            // Empty means "delete the variable" in .NET. Claude Code warns
            // ("Both ANTHROPIC_AUTH_TOKEN and ANTHROPIC_API_KEY set - auth may
            // not work as expected") when both are present, and a stray system
            // key would otherwise leak into the session.
            ["ANTHROPIC_API_KEY"] = string.Empty
        };

        if (!string.IsNullOrWhiteSpace(AuthToken))
        {
            environment["ANTHROPIC_AUTH_TOKEN"] = AuthToken;
        }

        ProviderEnvironment.ApplyPrivacy(environment, _config.Launcher);

        return environment;
    }
}