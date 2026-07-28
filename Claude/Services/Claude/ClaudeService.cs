using ClaudeLauncher.Core.Models;
using ClaudeLauncher.Core.Results;
using ClaudeLauncher.Services.Process;

namespace ClaudeLauncher.Services.Claude;

public sealed class ClaudeService : IClaudeService
{
    private readonly IInteractiveProcessRunner _processRunner;

    public ClaudeService(
    IInteractiveProcessRunner processRunner)
    {
        _processRunner = processRunner;
    }

    public Task<Result> LaunchAsync(LaunchContext context)
    {
        // When "Launch without project" is on, start Claude in the user's
        // home folder so it opens with a clean slate — no project context.
        var workingDirectory = context.Configuration.Launcher.LaunchWithoutProject
            ? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)
            : context.Configuration.Launcher.WorkingDirectory;

        return _processRunner.StartAsync(
            context.Provider.Executable,
            string.Empty,
            context.Provider.GetEnvironmentVariables(),
            workingDirectory,
            context.CancellationToken);
    }
}