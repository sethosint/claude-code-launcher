namespace ClaudeLauncher.Core.Models;

public enum LaunchStage
{
    Idle,
    CheckingGateway,
    StartingGateway,
    WaitingForGateway,
    LaunchingClaude,
    Completed,
    Failed
}