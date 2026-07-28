using ClaudeLauncher.Config;
using ClaudeLauncher.Core.Results;
using ClaudeLauncher.Providers;
using System.IO;

namespace ClaudeLauncher.Services.Launcher;

public sealed class EnvironmentValidationService : IEnvironmentValidationService
{
    public Result Validate(AppConfig configuration, IProvider provider)
    {
        if (string.IsNullOrWhiteSpace(configuration.Claude.Executable))
        {
            return Result.Failure("Claude executable path is not configured.");
        }

        if (!File.Exists(configuration.Claude.Executable))
        {
            return Result.Failure(
                $"Claude executable not found:{Environment.NewLine}{configuration.Claude.Executable}");
        }

        return provider.Id == "kiro"
            ? ValidateKiro(configuration)
            : ValidateCompletions(configuration);
    }

    private static Result ValidateKiro(AppConfig configuration)
    {
        if (string.IsNullOrWhiteSpace(configuration.Claude.Token))
        {
            return Result.Failure("Anthropic token is not configured for Kiro.");
        }

        if (string.IsNullOrWhiteSpace(configuration.Gateway.BaseUrl))
        {
            return Result.Failure("Gateway URL is not configured for Kiro.");
        }

        if (!Uri.TryCreate(configuration.Gateway.BaseUrl, UriKind.Absolute, out var gatewayUri))
        {
            return Result.Failure("Gateway URL is not a valid absolute URI.");
        }

        if (gatewayUri.Scheme != Uri.UriSchemeHttp &&
            gatewayUri.Scheme != Uri.UriSchemeHttps)
        {
            return Result.Failure("Gateway URL must use HTTP or HTTPS.");
        }

        return Result.Success();
    }

    private static Result ValidateCompletions(AppConfig configuration)
    {
        if (string.IsNullOrWhiteSpace(configuration.Completions.ApiKey))
        {
            return Result.Failure("API key is not configured for the Custom API provider.");
        }

        if (string.IsNullOrWhiteSpace(configuration.Completions.BaseUrl))
        {
            return Result.Failure("Base URL is not configured for the Custom API provider.");
        }

        if (!Uri.TryCreate(configuration.Completions.BaseUrl, UriKind.Absolute, out var baseUri))
        {
            return Result.Failure("Base URL is not a valid absolute URI.");
        }

        if (baseUri.Scheme != Uri.UriSchemeHttp &&
            baseUri.Scheme != Uri.UriSchemeHttps)
        {
            return Result.Failure("Base URL must use HTTP or HTTPS.");
        }

        // The Custom API provider can only work through the bundled Node
        // proxy. If the build didn't copy it next to the executable (or the
        // folder was deleted/blocked by antivirus), say so here instead of
        // letting the gateway step fail 25 seconds later with a vague timeout.
        if (!File.Exists(CompletionsProvider.ProxyScriptPath))
        {
            return Result.Failure(
                "The bundled API proxy is missing:" +
                $"{Environment.NewLine}{CompletionsProvider.ProxyScriptPath}" +
                $"{Environment.NewLine}Reinstall the launcher (or rebuild it) so the " +
                "\"CompletionsProxy\" folder sits next to the executable.");
        }

        if (!Directory.Exists(CompletionsProvider.ProxyModulesPath))
        {
            return Result.Failure(
                "The API proxy is missing its dependencies " +
                $"({CompletionsProvider.ProxyModulesPath})." +
                $"{Environment.NewLine}Run \"npm install\" inside the " +
                "\"CompletionsProxy\" folder, or reinstall the launcher.");
        }

        return Result.Success();
    }
}
