namespace ClaudeLauncher.Config;

using ClaudeLauncher.Providers;
using ClaudeLauncher.Core.Enums;

public sealed class AppConfig
{
    public ProviderType Provider { get; set; } = ProviderType.Kiro;

    public GatewayConfig Gateway { get; set; } = new();

    /// <summary>
    /// Local completions-proxy gateway settings for the Completions provider.
    /// The proxy translates Claude Code's Anthropic-format requests into the
    /// OpenAI-compatible format the upstream endpoint expects, and back. Executable
    /// and Arguments are always computed at runtime from the bundled proxy
    /// files (see CompletionsProvider.Gateway) so only the tunable knobs
    /// (port/timeouts) live here.
    /// </summary>
    public GatewayConfig CompletionsGateway { get; set; } = new()
    {
        BaseUrl = "http://localhost:4242",
        HealthEndpoint = "/health",
        AutoStart = true,
        StartupTimeoutSeconds = 25
    };

    public ClaudeConfig Claude { get; set; } = new();

    public CompletionsConfig Completions { get; set; } = new();

    public LauncherConfig Launcher { get; set; } = new();
}

public sealed class GatewayConfig
{
    public string BaseUrl { get; set; } = "";

    public string HealthEndpoint { get; set; } = "";

    public bool AutoStart { get; set; }

    public int StartupTimeoutSeconds { get; set; }

    public string Executable { get; set; } = "omniroute.cmd";

    public string Arguments { get; set; } = "--no-open";

    /// <summary>
    /// Directory the gateway process is started in. Empty means "inherit the
    /// launcher's own working directory". The bundled Completions proxy sets
    /// this to its own folder so Node resolves its bundled node_modules (and
    /// any relative paths) correctly no matter where the launcher was started
    /// from — e.g. when it is pinned to the taskbar or started by a shortcut
    /// whose "Start in" folder points somewhere else entirely.
    /// </summary>
    public string WorkingDirectory { get; set; } = "";

    /// <summary>
    /// What the running gateway must report as its upstream for the launcher
    /// to accept it. A gateway that is already listening on the port is
    /// reused instead of started twice — but if the user has since changed
    /// the endpoint (or the model) in Settings, that running instance is
    /// still talking to the *old* endpoint with the *old* key, which is why
    /// changing the Base URL used to have no effect at all until the proxy
    /// was killed by hand. When the value reported by /health differs, the
    /// launcher retires the stale gateway and starts a fresh one.
    /// Empty disables the comparison.
    /// </summary>
    public string ExpectedUpstream { get; set; } = "";

    /// <summary>Model the running gateway must report; see <see cref="ExpectedUpstream"/>.</summary>
    public string ExpectedModel { get; set; } = "";

    /// <summary>Endpoint (POST) that asks a stale gateway to exit. Empty means it cannot be retired automatically.</summary>
    public string ShutdownEndpoint { get; set; } = "";

    /// <summary>
    /// Optional substring that the health endpoint's response body must
    /// contain for the gateway to count as "ours and healthy". Guards against
    /// an unrelated service already listening on the configured port, which
    /// would otherwise be mistaken for a running gateway and make Claude talk
    /// to the wrong process. Empty means "any 2xx response is enough".
    /// </summary>
    public string HealthResponseMustContain { get; set; } = "";
}

public sealed class ClaudeConfig
{
    public string Executable { get; set; } = "";

    public string Model { get; set; } = "";

    public string Token { get; set; } = "";
}

public sealed class LauncherConfig
{
    public bool CloseAfterLaunch { get; set; }

    public string Theme { get; set; } = "Dark";

    /// <summary>
    /// When true, the launcher automatically switches to the other configured
    /// provider if the currently selected one reports exhausted credits/quota
    /// (HTTP 401/402/403/429) while being checked before launch.
    /// </summary>
    public bool AutoFallback { get; set; } = true;

    /// <summary>
    /// Folder Claude Code is started in — i.e. the project it will see as
    /// its workspace. Empty means "the launcher's own folder", which is
    /// almost never what the user wants: it made every session start in
    /// bin\Debug next to the executable.
    /// </summary>
    public string WorkingDirectory { get; set; } = "";

    /// <summary>
    /// When true, Claude Code starts in the user's home folder instead of
    /// the selected project folder — effectively a "clean" launch with no
    /// project context.
    /// </summary>
    public bool LaunchWithoutProject { get; set; }

    /// <summary>
    /// Most recently used project folders, newest first, capped at
    /// <see cref="RecentProjectsLimit"/> entries.
    /// </summary>
    public List<string> RecentProjects { get; set; } = [];

    /// <summary>How many entries <see cref="RecentProjects"/> keeps.</summary>
    public const int RecentProjectsLimit = 8;

    /// <summary>
    /// When true, Claude Code is started with every non-essential outbound
    /// channel switched off: telemetry, error reporting, the bug-report
    /// command, update checks and the terminal title (which otherwise
    /// broadcasts the project name). It does not — and cannot — hide the
    /// project from the model provider itself.
    /// </summary>
    public bool PrivacyMode { get; set; } = true;

    /// <summary>
    /// When true, the launcher verifies the provider's credentials against
    /// the provider itself before starting Claude. Without it an invalid or
    /// mismatched key only surfaces as a raw "401 Missing Authentication
    /// header" inside the Claude session, long after the launcher reported
    /// success.
    /// </summary>
    public bool PreflightAuthCheck { get; set; } = true;

    /// <summary>
    /// Set to true once the user has completed the first-run setup wizard
    /// (entered credentials for at least one provider). While false and no
    /// provider is configured, the app shows the setup wizard instead of
    /// the main window on startup.
    /// </summary>
    public bool SetupCompleted { get; set; }
}