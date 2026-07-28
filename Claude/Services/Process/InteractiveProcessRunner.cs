using DiagnosticsProcess = System.Diagnostics.Process;
using System.IO;
using System.Diagnostics;
using ClaudeLauncher.Core.Results;
using ClaudeLauncher.Services.Logging;

namespace ClaudeLauncher.Services.Process;

public sealed class InteractiveProcessRunner : IInteractiveProcessRunner
{
    private readonly ILogger _logger;

    public InteractiveProcessRunner(ILogger logger)
    {
        _logger = logger;
    }

    public Task<Result> StartAsync(
        string fileName,
        string arguments,
        IReadOnlyDictionary<string, string>? environmentVariables = null,
        string? workingDirectory = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            var previousValues = new Dictionary<string, string?>();

            if (environmentVariables is not null)
            {
                foreach (var pair in environmentVariables)
                {
                    previousValues[pair.Key] =
                        Environment.GetEnvironmentVariable(pair.Key);

                    Environment.SetEnvironmentVariable(pair.Key, pair.Value);
                }
            }

            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = fileName,
                    Arguments = arguments,
                    UseShellExecute = true
                };

                // Claude Code adopts its working directory as the project it
                // operates on. Leaving it unset inherited the launcher's own
                // folder, so every session opened in bin\\Debug instead of the
                // user's repository. A configured folder that no longer exists
                // must not abort the launch, so fall back to inheriting.
                if (!string.IsNullOrWhiteSpace(workingDirectory) &&
                    Directory.Exists(workingDirectory))
                {
                    startInfo.WorkingDirectory = workingDirectory;

                    _logger.Info($"Working directory: {workingDirectory}");
                }
                else if (!string.IsNullOrWhiteSpace(workingDirectory))
                {
                    _logger.Warning(
                        $"Configured project folder does not exist: {workingDirectory}");
                }

                var process = DiagnosticsProcess.Start(startInfo);

                if (process is null)
                {
                    return Task.FromResult(
                        Result.Failure("Failed to start interactive process."));
                }

                _logger.Info(
                    $"Interactive process started. PID={process.Id}");

                return Task.FromResult(Result.Success());
            }
            finally
            {
                foreach (var pair in previousValues)
                {
                    Environment.SetEnvironmentVariable(
                        pair.Key,
                        pair.Value);
                }
            }
        }
        catch (OperationCanceledException)
        {
            return Task.FromResult(
                Result.Failure("Interactive process start was cancelled."));
        }
        catch (Exception ex)
        {
            _logger.Error(ex);

            return Task.FromResult(
                Result.Failure(ex.ToString()));
        }
    }
}