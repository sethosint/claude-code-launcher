using ClaudeLauncher.Services.Models.Dto;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;

namespace ClaudeLauncher.Services.Models;

public sealed class ModelsApiClient : IModelsApiClient
{
    private readonly HttpClient _httpClient;

    public ModelsApiClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<ModelsResponse?> GetModelsAsync(
    string baseUrl,
    string? authToken,
    CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"{baseUrl.TrimEnd('/')}/models");

        if (!string.IsNullOrWhiteSpace(authToken))
        {
            request.Headers.Authorization =
                new AuthenticationHeaderValue("Bearer", authToken);
        }

        using var response = await _httpClient.SendAsync(
            request,
            cancellationToken);

        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(cancellationToken);

        return JsonSerializer.Deserialize<ModelsResponse>(
            json,
            new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
    }
}