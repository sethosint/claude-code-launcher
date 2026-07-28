using ClaudeLauncher.Config;
using ClaudeLauncher.Core.Enums;

namespace ClaudeLauncher.Providers;

public interface IProviderFactory
{
    IProvider Create(AppConfig config);

    /// <summary>
    /// Creates the provider that is NOT currently selected in <paramref name="config"/>,
    /// i.e. the fallback candidate.
    /// </summary>
    IProvider CreateAlternate(AppConfig config);
}