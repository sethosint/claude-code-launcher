using ClaudeLauncher.Core.Models;
using ClaudeLauncher.Providers;

public interface IModelService
{
    Task<IReadOnlyList<AiModel>> GetModelsAsync(
        IProvider provider,
        CancellationToken cancellationToken = default);
}