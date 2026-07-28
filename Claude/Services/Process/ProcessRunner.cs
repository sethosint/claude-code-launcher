using System;
using System.Diagnostics;
using System.IO;
using ClaudeLauncher.Core.Results;
using ClaudeLauncher.Services.Logging;
using DiagnosticsProcess = System.Diagnostics.Process;

namespace ClaudeLauncher.Services.Process;

public sealed class ProcessRunner : IProcessRunner
{
    private readonly ILogger _logger;

    public ProcessRunner(ILogger logger)
    {
        _logger = logger;
    }

    public Task<Result<StartedProcess>> StartAsync(
        string fileName,
        string arguments,
        string? workingDirectory = null,
        IReadOnlyDictionary<string, string>? environmentVariables = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            var requestedFileName = fileName;

            // Resolve bare command names ("node") to a real path ourselves so
            // we can start the .exe directly. Going through cmd.exe for these
            // used to corrupt the command line (see CommandShellHelper) and is
            // exactly why the Completions proxy never started.
            var resolved = ExecutableResolver.Resolve(fileName);

            if (resolved is not null)
            {
                fileName = resolved;
            }

            if (RequiresCommandShell(fileName))
            {
                arguments = CommandShellHelper.BuildShellArguments(fileName, arguments);

                fileName = "cmd.exe";
            }

            if (!string.Equals(requestedFileName, fileName, StringComparison.OrdinalIgnoreCase))
            {
                _logger.Info($"Resolved \"{requestedFileName}\" to \"{fileName}\".");
            }

            _logger.Info($"Starting process: {fileName} {arguments}".Trim());

            var startInfo = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                UseShellExecute = false,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden,
                // This runner is only used for background/non-interactive
                // processes (e.g. the gateway); Claude Code itself runs
                // through IInteractiveProcessRunner instead. Capturing
                // stdout/stderr here is safe and lets us log — and show —
                // a crashing gateway's own error output instead of only ever
                // seeing a generic "failed to start" timeout.
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                RedirectStandardInput = true
            };

            if (!string.IsNullOrWhiteSpace(workingDirectory) && Directory.Exists(workingDirectory))
            {
                startInfo.WorkingDirectory = workingDirectory;

                _logger.Info($"Working directory: {workingDirectory}");
            }

            if (environmentVariables is not null)
            {
                foreach (var pair in environmentVariables)
                {
                    startInfo.Environment[pair.Key] = pair.Value;
                }
            }

            var process = DiagnosticsProcess.Start(startInfo);

            if (process is null)
            {
                _logger.Error("Failed to start process.");

                return Task.FromResult(
                    Result<StartedProcess>.Failure("Failed to start process."));
            }

            var startedProcess = process;
            var startedPid = startedProcess.Id;

            var handle = new StartedProcess(startedPid);

            // Stream the child's own stdout/stderr into our log (and into the
            // handle, so the UI can quote it) as it happens. This is what
            // actually reveals *why* something like the CompletionsProxy or
            // OmniRoute crashed immediately — e.g. a Node stack trace or
            // "port already in use".
            startedProcess.OutputDataReceived += (_, args) =>
            {
                if (string.IsNullOrEmpty(args.Data))
                    return;

                handle.AppendOutput(args.Data);
                _logger.Info($"[PID={startedPid}] {args.Data}");
            };

            startedProcess.ErrorDataReceived += (_, args) =>
            {
                if (string.IsNullOrEmpty(args.Data))
                    return;

                handle.AppendOutput(args.Data);
                _logger.Error($"[PID={startedPid}] {args.Data}");
            };

            startedProcess.BeginOutputReadLine();
            startedProcess.BeginErrorReadLine();

            startedProcess.EnableRaisingEvents = true;

            var startedAt = DateTime.UtcNow;

            startedProcess.Exited += (_, _) =>
            {
                var lifetime = DateTime.UtcNow - startedAt;

                try
                {
                    // Read ExitCode before Dispose() invalidates the handle —
                    // reading it afterwards throws "No process is associated
                    // with this object."
                    var exitCode = startedProcess.ExitCode;

                    handle.MarkExited(exitCode, lifetime);

                    _logger.Info(
                        $"Process exited. PID={startedPid}, ExitCode={exitCode}, Lifetime={lifetime.TotalMilliseconds:F0} ms");
                }
                catch (Exception ex)
                {
                    handle.MarkExited(-1, lifetime);

                    _logger.Error(ex);
                }
                finally
                {
                    // Give the async stdout/stderr readers a moment to flush
                    // the process's last words (that is usually the actual
                    // error message) before the handle goes away.
                    try
                    {
                        startedProcess.WaitForExit(1000);
                    }
                    catch
                    {
                        // Nothing useful to do — the process is already gone.
                    }

                    try
                    {
                        startedProcess.Dispose();
                    }
                    catch
                    {
                        // Disposing a process that Windows already reaped is
                        // harmless; never let it surface as a crash.
                    }
                }
            };

            _logger.Info($"Process started successfully. PID={startedPid}");

            return Task.FromResult(Result<StartedProcess>.Success(handle));
        }
        catch (OperationCanceledException)
        {
            _logger.Info("Process start cancelled.");

            return Task.FromResult(
                Result<StartedProcess>.Failure("Process start was cancelled."));
        }
        catch (Exception ex)
        {
            _logger.Error(ex);

            return Task.FromResult(
                Result<StartedProcess>.Failure(ex.Message));
        }
    }

    /// <summary>
    /// Delegates to the shared helper (see <see cref="CommandShellHelper"/>)
    /// so ProcessRunner and GatewayService's executable-availability check
    /// always agree on which executables need the cmd.exe wrapper.
    /// </summary>
    private static bool RequiresCommandShell(string fileName) =>
        CommandShellHelper.RequiresCommandShell(fileName);
}
