using ClaudeLauncher.UI.Models;

namespace ClaudeLauncher.Services.Launcher;

public sealed class LaunchLogEventArgs : EventArgs
{
    public LaunchLogItem Item { get; }

    public LaunchLogEventArgs(LaunchLogItem item)
    {
        Item = item ?? throw new ArgumentNullException(nameof(item));
    }
}