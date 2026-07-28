using ClaudeLauncher.Core.Results;

namespace ClaudeLauncher.Services.Process;

public interface IProcessRunner
{
    /// <summary>
    /// Starts a background process and returns a live handle to it, so the
    /// caller can react to an immediate crash instead of waiting out a
    /// health-check timeout.
    /// </summary>
    Task<Result<StartedProcess>> StartAsync(
        string fileName,
        string arguments,
        string? workingDirectory = null,
        IReadOnlyDictionary<string, string>? environmentVariables = null,
        CancellationToken cancellationToken = default);
}
