using ClaudeLauncher.Config;

namespace ClaudeLauncher.UI.Helpers;

public static class ConfigCloner
{
    public static AppConfig Clone(AppConfig config)
    {
        return new AppConfig
        {
            Provider = config.Provider,

            Gateway = new GatewayConfig
            {
                BaseUrl = config.Gateway.BaseUrl,
                HealthEndpoint = config.Gateway.HealthEndpoint,
                AutoStart = config.Gateway.AutoStart,
                StartupTimeoutSeconds = config.Gateway.StartupTimeoutSeconds,
                Executable = config.Gateway.Executable,
                Arguments = config.Gateway.Arguments
            },

            CompletionsGateway = new GatewayConfig
            {
                BaseUrl = config.CompletionsGateway.BaseUrl,
                HealthEndpoint = config.CompletionsGateway.HealthEndpoint,
                AutoStart = config.CompletionsGateway.AutoStart,
                StartupTimeoutSeconds = config.CompletionsGateway.StartupTimeoutSeconds,
                Executable = config.CompletionsGateway.Executable,
                Arguments = config.CompletionsGateway.Arguments
            },

            Claude = new ClaudeConfig
            {
                Executable = config.Claude.Executable,
                Model = config.Claude.Model,
                Token = config.Claude.Token
            },

            Completions = new CompletionsConfig
            {
                BaseUrl = config.Completions.BaseUrl,
                ApiKey = config.Completions.ApiKey,
                Model = config.Completions.Model
            },

            Launcher = new LauncherConfig
            {
                CloseAfterLaunch = config.Launcher.CloseAfterLaunch,
                Theme = config.Launcher.Theme,
                AutoFallback = config.Launcher.AutoFallback,
                SetupCompleted = config.Launcher.SetupCompleted
            }
        };
    }
}