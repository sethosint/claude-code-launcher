namespace ClaudeLauncher.Core.Models;

public sealed class AiModel
{
    public required string Id { get; init; }

    public required string Name { get; init; }

    public override string ToString() => Name;
}