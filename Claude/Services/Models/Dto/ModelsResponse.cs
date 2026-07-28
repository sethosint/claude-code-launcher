using System.Text.Json.Serialization;

namespace ClaudeLauncher.Services.Models.Dto;

public sealed class ModelsResponse
{
    [JsonPropertyName("data")]
    public List<ModelItem> Data { get; init; } = [];
}