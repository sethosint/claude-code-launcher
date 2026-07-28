namespace ClaudeLauncher.Services.Launcher;

public sealed class LauncherStatus
{
    public LauncherState State { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public double Progress { get; set; }
}