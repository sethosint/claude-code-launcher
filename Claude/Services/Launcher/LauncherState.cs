namespace ClaudeLauncher.Services.Launcher;

public enum LauncherState
{
    Initializing,
    LoadingConfiguration,
    CheckingProvider,
    SwitchingProvider,
    CheckingGateway,
    StartingGateway,
    WaitingForGateway,
    LaunchingClaude,
    Completed,
    Failed,
    Cancelled
}