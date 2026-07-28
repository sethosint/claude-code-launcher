using System.Text;

namespace ClaudeLauncher.Services.Process;

/// <summary>
/// Live handle to a background process started by <see cref="ProcessRunner"/>.
///
/// Why this exists: the gateway pipeline used to only learn "the process was
/// started", then poll the health endpoint for the full startup timeout. When
/// the process died instantly (the classic case: a mangled command line, a
/// port already in use, an invalid API key) the user still waited 25 seconds
/// and then got a generic "never responded as healthy" message, while the
/// real reason sat in a log file nobody opens. With this handle the caller can
/// notice the exit immediately and quote the process's own output back to the
/// user.
/// </summary>
public sealed class StartedProcess
{
    private const int MaxCapturedLines = 12;

    private readonly object _sync = new();
    private readonly List<string> _output = [];

    public StartedProcess(int processId)
    {
        ProcessId = processId;
    }

    public int ProcessId { get; }

    public bool HasExited { get; private set; }

    public int ExitCode { get; private set; }

    public TimeSpan Lifetime { get; private set; }

    internal void AppendOutput(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
            return;

        lock (_sync)
        {
            _output.Add(line.Trim());

            if (_output.Count > MaxCapturedLines)
                _output.RemoveAt(0);
        }
    }

    internal void MarkExited(int exitCode, TimeSpan lifetime)
    {
        lock (_sync)
        {
            HasExited = true;
            ExitCode = exitCode;
            Lifetime = lifetime;
        }
    }

    /// <summary>The last few lines the process printed, ready to show in the UI.</summary>
    public string GetRecentOutput(int maxLines = 6)
    {
        lock (_sync)
        {
            if (_output.Count == 0)
                return string.Empty;

            var lines = _output
                .Skip(Math.Max(0, _output.Count - maxLines))
                .Select(StripAnsi)
                .Where(line => !string.IsNullOrWhiteSpace(line));

            return string.Join(Environment.NewLine, lines);
        }
    }

    /// <summary>
    /// Console tools colour their output with ANSI escape sequences, which a
    /// WPF TextBlock renders as garbage like "[36m". Strip them before the
    /// text reaches the UI.
    /// </summary>
    private static string StripAnsi(string line)
    {
        var builder = new StringBuilder(line.Length);

        for (var i = 0; i < line.Length; i++)
        {
            if (line[i] == '\u001b')
            {
                while (i < line.Length && !char.IsLetter(line[i]))
                    i++;

                continue;
            }

            builder.Append(line[i]);
        }

        return builder.ToString().Trim();
    }
}
