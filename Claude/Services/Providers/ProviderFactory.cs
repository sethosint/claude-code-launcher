using ClaudeLauncher.Config;
using ClaudeLauncher.Core.Enums;
using ClaudeLauncher.Providers;

namespace ClaudeLauncher.Providers;

public sealed class ProviderFactory : IProviderFactory
{
    public IProvider Create(AppConfig config)
    {
        return config.Provider switch
        {
            ProviderType.Kiro => new KiroProvider(config),

            ProviderType.Completions => new CompletionsProvider(config),

            _ => throw new NotSupportedException(
                $"Unsupported provider: {config.Provider}")
        };
    }

    public IProvider CreateAlternate(AppConfig config)
    {
        var alternateType = config.Provider switch
        {
            ProviderType.Kiro => ProviderType.Completions,
            ProviderType.Completions => ProviderType.Kiro,
            _ => throw new NotSupportedException(
                $"Unsupported provider: {config.Provider}")
        };

        // CompletionsGateway must be carried over too: without it the
        // alternate provider silently fell back to a brand-new GatewayConfig,
        // discarding a user-tuned proxy port/timeout during auto-fallback.
        return Create(new AppConfig
        {
            Provider = alternateType,
            Gateway = config.Gateway,
            CompletionsGateway = config.CompletionsGateway,
            Claude = config.Claude,
            Completions = config.Completions,
            Launcher = config.Launcher
        });
    }
}