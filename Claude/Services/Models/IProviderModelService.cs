using ClaudeLauncher.Config;
using ClaudeLauncher.Core.Models;

namespace ClaudeLauncher.Services.Models;

public interface IProviderModelService
{
    Task<IReadOnlyList<AiModel>> GetModelsAsync(
        AppConfig configuration,
        CancellationToken cancellationToken = default);
}