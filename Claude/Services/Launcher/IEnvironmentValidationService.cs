using ClaudeLauncher.Config;
using ClaudeLauncher.Core.Results;
using ClaudeLauncher.Providers;

namespace ClaudeLauncher.Services.Launcher;

public interface IEnvironmentValidationService
{
    Result Validate(AppConfig configuration, IProvider provider);
}
