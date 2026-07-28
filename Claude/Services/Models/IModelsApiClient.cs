using ClaudeLauncher.Services.Models.Dto;

namespace ClaudeLauncher.Services.Models;

public interface IModelsApiClient
{
    Task<ModelsResponse?> GetModelsAsync(
        string baseUrl,
        string? authToken,
        CancellationToken cancellationToken = default);
}