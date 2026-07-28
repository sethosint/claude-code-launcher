using DiagnosticsProcess = System.Diagnostics.Process;
using System.Net.Http;
using System.Text.Json;
using ClaudeLauncher.Config;
using ClaudeLauncher.Core.Models;
using ClaudeLauncher.Core.Results;
using ClaudeLauncher.Services.Logging;
using ClaudeLauncher.Services.Process;

namespace ClaudeLauncher.Services.Gateway;

public sealed class GatewayService : IGatewayService
{
    private readonly HttpClient _httpClient;
    private readonly IProcessRunner _processRunner;
    private readonly ILogger _logger;

    public GatewayService(
        HttpClient httpClient,
        IProcessRunner processRunner,
        ILogger logger)
    {
        _httpClient = httpClient;
        _processRunner = processRunner;
        _logger = logger;
    }

    public async Task<Result> EnsureStartedAsync(
        LaunchContext context)
    {
        try
        {
            var gateway = context.Provider.Gateway;
            var authToken = context.Provider.AuthToken;

            var health = await ProbeAsync(gateway, authToken, context.CancellationToken);

            if (health.IsHealthy)
            {
                // A gateway that is already up is normally reused as-is. But a
                // proxy left running from an earlier session still serves the
                // endpoint, key and model it was started with, so reusing it
                // silently discarded every change the user had made in
                // Settings since ("I changed the Base URL and nothing
                // happened"). If it no longer matches the current
                // configuration, retire it and start a fresh one.
                if (MatchesConfiguration(gateway, health.Body))
                    return Result.Success();

                _logger.Warning(
                    "A gateway is already running on " + gateway.BaseUrl +
                    " but does not match the current settings — restarting it.");

                var retired = await RetireStaleGatewayAsync(
                    gateway,
                    authToken,
                    health.Body,
                    context.CancellationToken);

                if (!retired)
                {
                    return Result.Failure(
                        $"Another gateway is already listening on {gateway.BaseUrl} and it is " +
                        "configured for a different endpoint or model than the one selected " +
                        "here. Close that process (or change the gateway port in Settings) " +
                        "and try again — otherwise Claude would keep talking to the old one.");
                }
            }

            if (gateway.AutoStart)
            {
                if (!IsExecutableAvailable(gateway.Executable))
                {
                    return Result.Failure(
                        $"Could not find \"{gateway.Executable}\" on this machine. " +
                        BuildMissingExecutableHint(gateway.Executable));
                }

                var startResult = await StartGatewayAsync(
                    gateway,
                    context.Provider.GatewayEnvironmentVariables,
                    context.CancellationToken);

                if (startResult.IsFailure || startResult.Value is null)
                {
                    return Result.Failure(
                        startResult.Error ?? "Failed to start the gateway process.");
                }

                var process = startResult.Value;

                if (await WaitForGatewayAsync(
                        gateway,
                        authToken,
                        process,
                        context.CancellationToken))
                {
                    return Result.Success();
                }

                // Two very different failures, two very different messages:
                // the process died on us (quote its own last words — that is
                // the actual reason), or it is alive but never answered.
                if (process.HasExited)
                {
                    var output = process.GetRecentOutput();

                    return Result.Failure(
                        $"The gateway process exited immediately (exit code {process.ExitCode}, " +
                        $"after {process.Lifetime.TotalMilliseconds:F0} ms)." +
                        (string.IsNullOrWhiteSpace(output)
                            ? " It printed nothing — see \"Open Logs\" for the full command line that was used."
                            : $"{Environment.NewLine}{output}"));
                }

                return Result.Failure(
                    "Gateway process is running but never responded as healthy " +
                    $"on {gateway.BaseUrl}{gateway.HealthEndpoint} within " +
                    $"{gateway.StartupTimeoutSeconds}s. Open \"Open Logs\" — the " +
                    "gateway's own output (Node stack trace, port already in " +
                    "use, invalid API key, …) is logged there.");
            }

            return Result.Failure(
                "Gateway is not running and auto-start is disabled for this provider.");
        }
        catch (OperationCanceledException)
        {
            return Result.Failure("Gateway startup was cancelled.");
        }
    }

    /// <summary>Health probe result: whether the gateway answered, plus its raw payload for identity checks.</summary>
    private readonly record struct HealthProbe(bool IsHealthy, string Body);

    private async Task<HealthProbe> ProbeAsync(
        GatewayConfig gateway,
        string? authToken,
        CancellationToken cancellationToken)
    {
        try
        {
            using var request = new HttpRequestMessage(
                HttpMethod.Get,
                gateway.BaseUrl + gateway.HealthEndpoint);

            // Some local gateways (e.g. Kiro's OmniRoute, whose health
            // endpoint is the real "/v1/models" API route rather than a
            // dedicated unauthenticated "/health") reject unauthenticated
            // requests with 401/403, which would otherwise make a perfectly
            // healthy gateway look like it never started. Send the same
            // token used for real requests so the probe matches reality.
            if (!string.IsNullOrWhiteSpace(authToken))
            {
                request.Headers.TryAddWithoutValidation(
                    "Authorization", $"Bearer {authToken}");
            }

            var response = await _httpClient.SendAsync(request, cancellationToken);

            if (!response.IsSuccessStatusCode)
                return new HealthProbe(false, string.Empty);

            var body = await response.Content.ReadAsStringAsync(cancellationToken);

            if (string.IsNullOrWhiteSpace(gateway.HealthResponseMustContain))
                return new HealthProbe(true, body);

            var isOurs = body.Contains(
                gateway.HealthResponseMustContain,
                StringComparison.OrdinalIgnoreCase);

            return new HealthProbe(isOurs, body);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            return new HealthProbe(false, string.Empty);
        }
    }

    private async Task<bool> CheckHealthAsync(
        GatewayConfig gateway,
        string? authToken,
        CancellationToken cancellationToken)
    {
        var probe = await ProbeAsync(gateway, authToken, cancellationToken);

        return probe.IsHealthy;
    }

    /// <summary>
    /// True when the gateway that is already running reports the same upstream
    /// and model the current configuration asks for. Values the gateway does
    /// not report (or that are not configured) are treated as matching, so
    /// gateways without an identity payload keep the old "reuse it" behaviour.
    /// </summary>
    private static bool MatchesConfiguration(GatewayConfig gateway, string body)
    {
        if (string.IsNullOrWhiteSpace(body))
            return true;

        return FieldMatches(body, "saasUrl", gateway.ExpectedUpstream)
            && FieldMatches(body, "model", gateway.ExpectedModel);
    }

    private static bool FieldMatches(string body, string field, string expected)
    {
        if (string.IsNullOrWhiteSpace(expected))
            return true;

        try
        {
            using var document = JsonDocument.Parse(body);

            if (!document.RootElement.TryGetProperty(field, out var element) ||
                element.ValueKind != JsonValueKind.String)
            {
                return true;
            }

            var actual = element.GetString() ?? string.Empty;

            return string.Equals(
                actual.TrimEnd('/'),
                expected.TrimEnd('/'),
                StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return true;
        }
    }

    /// <summary>
    /// Asks a stale gateway to exit and waits for the port to go quiet.
    /// Returns false if it is still answering afterwards — in that case the
    /// port belongs to something the launcher must not fight over.
    /// </summary>
    private async Task<bool> RetireStaleGatewayAsync(
        GatewayConfig gateway,
        string? authToken,
        string healthBody,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(gateway.ShutdownEndpoint))
            return false;

        try
        {
            using var content = new StringContent(string.Empty);

            using var response = await _httpClient.PostAsync(
                gateway.BaseUrl + gateway.ShutdownEndpoint,
                content,
                cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            // The gateway may drop the connection while shutting down; the
            // loop below decides whether it actually went away.
        }

        if (await WaitUntilGoneAsync(gateway, authToken, 12, cancellationToken))
            return true;

        // The graceful route failed (an older proxy build has no /shutdown
        // endpoint at all). If it told us its process id, end it directly
        // rather than leaving the user to hunt for it in Task Manager.
        var pid = ReadPid(healthBody);

        if (pid is null)
            return false;

        try
        {
            using var process = DiagnosticsProcess.GetProcessById(pid.Value);

            _logger.Warning($"Stale gateway did not stop on request; terminating PID {pid}.");

            process.Kill(entireProcessTree: true);
        }
        catch (Exception ex)
        {
            _logger.Warning($"Could not terminate the stale gateway (PID {pid}): {ex.Message}");

            return false;
        }

        return await WaitUntilGoneAsync(gateway, authToken, 8, cancellationToken);
    }

    private async Task<bool> WaitUntilGoneAsync(
        GatewayConfig gateway,
        string? authToken,
        int attempts,
        CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < attempts; attempt++)
        {
            await Task.Delay(250, cancellationToken);

            var probe = await ProbeAsync(gateway, authToken, cancellationToken);

            if (!probe.IsHealthy)
            {
                _logger.Info("Stale gateway stopped; starting a new one.");

                return true;
            }
        }

        return false;
    }

    private static int? ReadPid(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
            return null;

        try
        {
            using var document = JsonDocument.Parse(body);

            return document.RootElement.TryGetProperty("pid", out var pid) &&
                   pid.ValueKind == JsonValueKind.Number
                ? pid.GetInt32()
                : null;
        }
        catch
        {
            return null;
        }
    }

    private static string BuildMissingExecutableHint(string executable)
    {
        if (executable.Contains("omniroute", StringComparison.OrdinalIgnoreCase))
        {
            return "The Kiro provider needs the OmniRoute CLI installed at that exact path — run \"npm install -g omniroute\" (or reinstall/update it), then restart the launcher and try again.";
        }

        if (executable.Contains("node", StringComparison.OrdinalIgnoreCase))
        {
            return "The Custom API provider needs Node.js to run its local proxy — install it from https://nodejs.org (any LTS version), then restart the launcher (a fresh start is required so it picks up the new PATH) and try again.";
        }

        return "Make sure it is installed and available on PATH, then try again.";
    }

    /// <summary>
    /// Quick lookup so a missing gateway executable (most commonly a machine
    /// without Node.js installed) fails immediately with a clear, actionable
    /// message instead of silently timing out after the full
    /// StartupTimeoutSeconds with a generic "failed to start" error.
    ///
    /// This deliberately uses the same resolver ProcessRunner uses to launch
    /// the process, so "available" here always means "launchable there".
    /// The previous implementation actually executed the candidate through
    /// cmd.exe and inspected its exit code, which was both slow and — for
    /// bare names like "node" — wrong in the same way the launch path was.
    /// </summary>
    private static bool IsExecutableAvailable(string executable)
        => ExecutableResolver.Exists(executable);

    private Task<Result<StartedProcess>> StartGatewayAsync(
        GatewayConfig gateway,
        IReadOnlyDictionary<string, string>? environmentVariables,
        CancellationToken cancellationToken)
    {
        return _processRunner.StartAsync(
            gateway.Executable,
            gateway.Arguments,
            workingDirectory: string.IsNullOrWhiteSpace(gateway.WorkingDirectory)
                ? null
                : gateway.WorkingDirectory,
            environmentVariables: environmentVariables,
            cancellationToken: cancellationToken);
    }

    private async Task<bool> WaitForGatewayAsync(
        GatewayConfig gateway,
        string? authToken,
        StartedProcess process,
        CancellationToken cancellationToken)
    {
        var timeout = TimeSpan.FromSeconds(
            gateway.StartupTimeoutSeconds > 0
                ? gateway.StartupTimeoutSeconds
                : 25);

        var startTime = DateTime.UtcNow;

        while (DateTime.UtcNow - startTime < timeout)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (await CheckHealthAsync(gateway, authToken, cancellationToken))
                return true;

            // No point waiting out the full timeout for a process that is
            // already dead — this is what used to turn a 12 ms crash into a
            // 25 second stare at a progress bar.
            if (process.HasExited)
            {
                // One last probe: a gateway that hands off to a detached
                // child could legitimately exit after the port is up.
                await Task.Delay(250, cancellationToken);

                return await CheckHealthAsync(gateway, authToken, cancellationToken);
            }

            await Task.Delay(500, cancellationToken);
        }

        return false;
    }
}
