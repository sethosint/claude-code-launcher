using ClaudeLauncher.Config;
using ClaudeLauncher.Core.Models;
using ClaudeLauncher.Providers;

namespace ClaudeLauncher.Services.Models;

public sealed class ProviderModelService : IProviderModelService
{
    private readonly IProviderFactory _providerFactory;
    private readonly IModelService _modelService;

    public ProviderModelService(
        IProviderFactory providerFactory,
        IModelService modelService)
    {
        _providerFactory = providerFactory;
        _modelService = modelService;
    }

    public Task<IReadOnlyList<AiModel>> GetModelsAsync(
        AppConfig configuration,
        CancellationToken cancellationToken = default)
    {
        var provider = _providerFactory.Create(configuration);

        return _modelService.GetModelsAsync(
            provider,
            cancellationToken);
    }
}