namespace ClaudeLauncher.Core.Models;

public sealed class LaunchProgress
{
    public LaunchStage Stage { get; }

    public string Message { get; }

    public LaunchProgress(LaunchStage stage, string message)
    {
        Stage = stage;
        Message = message;
    }
}