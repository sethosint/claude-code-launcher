namespace ClaudeLauncher.Services.Launcher;

public sealed class LauncherStatusChangedEventArgs : EventArgs
{
    public LauncherStatusChangedEventArgs(
        LauncherStatus status)
    {
        Status = status;
    }

    public LauncherStatus Status { get; }

}