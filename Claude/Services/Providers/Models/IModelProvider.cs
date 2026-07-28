using ClaudeLauncher.Config;
using ClaudeLauncher.Core.Models;

namespace ClaudeLauncher.Providers;

public interface IModelProvider
{
    Task<IReadOnlyList<AiModel>> GetModelsAsync(
        AppConfig configuration,
        CancellationToken cancellationToken = default);
}