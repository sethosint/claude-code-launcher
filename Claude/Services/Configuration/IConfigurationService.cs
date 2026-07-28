using ClaudeLauncher.Config;
using ClaudeLauncher.Core.Results;

namespace ClaudeLauncher.Services.Configuration;

public interface IConfigurationService
{
    Result<AppConfig> GetConfiguration();

    Result SaveConfiguration(AppConfig configuration);
}