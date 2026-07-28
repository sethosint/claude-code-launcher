using System.Text.Json.Serialization;

namespace ClaudeLauncher.Services.Models.Dto;

public sealed class ModelItem
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = "";
}