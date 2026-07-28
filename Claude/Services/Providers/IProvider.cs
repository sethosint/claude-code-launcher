using ClaudeLauncher.Config;
using ClaudeLauncher.Providers;

namespace ClaudeLauncher.Providers;

public interface IProvider
{
    string Id { get; }

    string DisplayName { get; }

    string Executable { get; }

    string BaseUrl { get; }

    string Model { get; }

    string? AuthToken { get; }

    IReadOnlyDictionary<string, string> GetEnvironmentVariables();

    GatewayConfig Gateway { get; }

    /// <summary>
    /// Whether this provider needs a local gateway (e.g. Kiro's OmniRoute)
    /// started and health-checked before launching Claude.
    /// </summary>
    bool RequiresGateway { get; }

    /// <summary>
    /// Whether the provider has the minimum settings required to be used
    /// (endpoint + credentials), independent of whether it is selected.
    /// </summary>
    bool IsConfigured { get; }

    /// <summary>
    /// Extra environment variables to pass to the gateway process itself
    /// (not to Claude Code) when it is started. Null when the provider's
    /// gateway needs nothing beyond what is already on the arguments/PATH
    /// (e.g. Kiro's OmniRoute).
    /// </summary>
    IReadOnlyDictionary<string, string>? GatewayEnvironmentVariables { get; }
}