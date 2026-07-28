using ClaudeLauncher.Config;

namespace ClaudeLauncher.Providers;

/// <summary>
/// Environment variables that are about the Claude Code *client* rather than
/// about a specific provider, so both providers set them identically.
/// </summary>
internal static class ProviderEnvironment
{
    /// <summary>
    /// Turns off everything Claude Code sends that is not the conversation
    /// itself: telemetry, error reporting, the bug-report command, update
    /// checks, and writing the current project's name into the terminal title.
    ///
    /// What this cannot do — and no launcher setting can — is hide the project
    /// from the model provider: the files, paths and shell output Claude Code
    /// puts in a request are the request. The only lever there is which
    /// provider gets to see it, which is what the provider switch is for.
    /// </summary>
    public static void ApplyPrivacy(
        IDictionary<string, string> environment,
        LauncherConfig launcher)
    {
        environment["DISABLE_AUTOUPDATER"] = "1";

        if (!launcher.PrivacyMode)
            return;

        environment["CLAUDE_CODE_DISABLE_NONESSENTIAL_TRAFFIC"] = "1";
        environment["DISABLE_TELEMETRY"] = "1";
        environment["DISABLE_ERROR_REPORTING"] = "1";
        environment["DISABLE_BUG_COMMAND"] = "1";
        environment["CLAUDE_CODE_DISABLE_TERMINAL_TITLE"] = "1";
    }
}
