using ClaudeLauncher.Core.Models;
using ClaudeLauncher.Providers;
using ClaudeLauncher.Services.Models.Dto;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Windows;

namespace ClaudeLauncher.Services.Models;

public sealed class ModelService : IModelService
{
    private readonly IModelsApiClient _apiClient;

    public ModelService(IModelsApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    /// <summary>
    /// Kiro's own gateway (OmniRoute) /models endpoint returns dozens of
    /// duplicate/alias entries for the same handful of underlying models
    /// (kr/, kiro/, aug/ prefixes, snake_case and ALL_CAPS ids, "latest"/
    /// "fast" variants, etc.), most of which are not guaranteed to work or
    /// be free. Kiro's own site documents exactly these two ids as the
    /// free, always-available defaults, so we show only them for Kiro
    /// instead of the raw noisy catalog.
    /// </summary>
    private static readonly IReadOnlyList<AiModel> KiroCuratedModels =
    [
        new AiModel { Id = "kr/claude-sonnet-4.5", Name = "Claude Sonnet 4.5" },
        new AiModel { Id = "kr/claude-haiku-4.5", Name = "Claude Haiku 4.5" }
    ];

    public async Task<IReadOnlyList<AiModel>> GetModelsAsync(
    IProvider provider,
    CancellationToken cancellationToken = default)
    {
        if (provider.Id == "kiro")
            return KiroCuratedModels;

        var dto = await _apiClient.GetModelsAsync(
            provider.BaseUrl,
            provider.AuthToken,
            cancellationToken);

        if (dto is null)
            return [];

        return dto.Data
            .Where(x => IsClaudeModel(x.Id))
            .Select(x => new AiModel
            {
                Id = x.Id,
                Name = FormatClaudeName(x.Id)
            })
            .GroupBy(x => x.Name)
            .Select(g => g
                .OrderByDescending(x =>
                    x.Id.StartsWith("aug/", StringComparison.OrdinalIgnoreCase))
                .First())
            .OrderByDescending(x => GetVersion(x.Id))
            .ThenBy(x => GetPriority(x.Name))
            .ToList();
    }

    /// <summary>
    /// The launcher only ever runs Claude Code, so gateways that proxy many
    /// vendors (OmniRoute, OpenRouter, etc.) must not offer non-Claude
    /// models in the picker — even though their /models endpoint may list
    /// GPT, Gemini, DeepSeek, and so on alongside Claude.
    /// </summary>
    private static bool IsClaudeModel(string id)
    {
        return id.Contains("claude", StringComparison.OrdinalIgnoreCase);
    }

    private static string FormatClaudeName(string id)
    {
        var slash = id.IndexOf('/');

        if (slash >= 0)
        {
            id = id[(slash + 1)..];
        }

        return id
            .Replace("claude-", "Claude ")
            .Replace("haiku", "Haiku")
            .Replace("sonnet", "Sonnet")
            .Replace("opus", "Opus")
            .Replace('-', ' ');
    }

    private static double GetVersion(string id)
    {
        var match = Regex.Match(id, @"(\d+(\.\d+)?)$");

        if (!match.Success)
            return 0;

        return double.Parse(
            match.Value,
            CultureInfo.InvariantCulture);
    }

    private static int GetPriority(string name)
    {
        if (name.Contains("Opus", StringComparison.OrdinalIgnoreCase))
            return 0;

        if (name.Contains("Sonnet", StringComparison.OrdinalIgnoreCase))
            return 1;

        if (name.Contains("Haiku", StringComparison.OrdinalIgnoreCase))
            return 2;

        return 3;
    }
}