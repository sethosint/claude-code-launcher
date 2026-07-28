using ClaudeLauncher.Core.Results;

namespace ClaudeLauncher.Services.Process;

public interface IInteractiveProcessRunner
{
    /// <summary>
    /// Starts an interactive (console/UI) process.
    /// </summary>
    /// <param name="workingDirectory">
    /// Folder the process starts in. Claude Code treats this as the project
    /// it works on, so it must be the user's project folder, not the
    /// launcher's own directory.
    /// </param>
    Task<Result> StartAsync(
        string fileName,
        string arguments,
        IReadOnlyDictionary<string, string>? environmentVariables = null,
        string? workingDirectory = null,
        CancellationToken cancellationToken = default);
}