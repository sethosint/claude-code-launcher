namespace ClaudeLauncher.Services.Launcher;

public static class LauncherStateMapper
{
    public static LauncherUiState Map(LauncherState state)
    {
        return state switch
        {
            LauncherState.Completed => LauncherUiState.Completed,
            LauncherState.Failed => LauncherUiState.Failed,
            LauncherState.Cancelled => LauncherUiState.Cancelled,
            _ => LauncherUiState.Starting
        };
    }
}