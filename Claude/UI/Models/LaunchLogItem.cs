namespace ClaudeLauncher.UI.Models;

public sealed class LaunchLogItem
{
    public DateTime Timestamp { get; }

    public LaunchLogLevel Level { get; }

    public string Message { get; }

    public bool IsSuccess => Level is LaunchLogLevel.Info or LaunchLogLevel.Success;

    public bool IsWarning => Level == LaunchLogLevel.Warning;

    public bool IsError => Level == LaunchLogLevel.Error;

    public LaunchLogItem(
        LaunchLogLevel level,
        string message)
    {
        Timestamp = DateTime.Now;
        Level = level;
        Message = message;
    }
}
