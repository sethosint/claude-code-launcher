using ClaudeLauncher.Providers;

namespace ClaudeLauncher.Services.Launcher;

public interface ILauncherEngine
{
    LauncherStatus Status { get; }

    IProvider? ActiveProvider { get; }

    /// <summary>Short result of the most recent credential pre-flight, or null if it never ran.</summary>
    string? LastAccessSummary { get; }

    event EventHandler<LauncherStatusChangedEventArgs>? StatusChanged;

    Task InitializeAsync();

    void Cancel();
}