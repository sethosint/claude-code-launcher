using System.IO;

namespace ClaudeLauncher.Services.Process;

/// <summary>
/// Resolves a command name (e.g. "node") to a full, launchable path.
///
/// Why this exists: <see cref="System.Diagnostics.Process"/> can start a bare
/// command name only by going through cmd.exe, and cmd.exe's quoting rules are
/// hostile — with more than two quote characters on the command line it strips
/// the first and the last quote (see <see cref="CommandShellHelper"/>), which
/// silently mangled the Completions proxy launch
/// (<c>cmd /c "node" "C:\...\server.js" --no-launch</c> became
/// <c>node" "C:\...\server.js --no-launch</c> and died with exit code 1 after
/// a few milliseconds). Resolving the executable ourselves lets us start
/// node.exe directly — no shell, no quoting surprises.
/// </summary>
public static class ExecutableResolver
{
    /// <summary>
    /// Extra directories probed for Node.js when it is not on PATH (a very
    /// common case for GUI apps launched from Explorer: the installer adds
    /// node to the *user* PATH, and an already-running Explorer session can
    /// hand a stale environment block to its children).
    /// </summary>
    private static IEnumerable<string> WellKnownNodeDirectories()
    {
        var candidates = new List<string?>
        {
            Environment.GetEnvironmentVariable("ProgramFiles") is { } programFiles
                ? Path.Combine(programFiles, "nodejs")
                : null,
            Environment.GetEnvironmentVariable("ProgramFiles(x86)") is { } programFilesX86
                ? Path.Combine(programFilesX86, "nodejs")
                : null,
            Environment.GetEnvironmentVariable("APPDATA") is { } appData
                ? Path.Combine(appData, "npm")
                : null,
            Environment.GetEnvironmentVariable("LOCALAPPDATA") is { } localAppData
                ? Path.Combine(localAppData, "Programs", "nodejs")
                : null,
            Environment.GetEnvironmentVariable("LOCALAPPDATA") is { } localAppDataFnm
                ? Path.Combine(localAppDataFnm, "fnm_multishells")
                : null,
            Environment.GetEnvironmentVariable("NVM_SYMLINK")
        };

        foreach (var candidate in candidates)
        {
            if (!string.IsNullOrWhiteSpace(candidate))
                yield return candidate!;
        }
    }

    /// <summary>
    /// Returns the full path of <paramref name="fileName"/>, or null when it
    /// cannot be found. Rooted paths are returned as-is when they exist.
    /// </summary>
    public static string? Resolve(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            return null;

        var command = fileName.Trim().Trim('"');

        if (Path.IsPathRooted(command))
            return File.Exists(command) ? command : null;

        var extensions = GetExecutableExtensions(command);

        foreach (var directory in GetSearchDirectories())
        {
            foreach (var extension in extensions)
            {
                string candidate;

                try
                {
                    candidate = Path.Combine(directory, command + extension);
                }
                catch
                {
                    // Malformed PATH entries (illegal characters) must never
                    // take the whole resolution down.
                    continue;
                }

                if (File.Exists(candidate))
                    return candidate;
            }
        }

        return null;
    }

    public static bool Exists(string fileName)
        => Resolve(fileName) is not null;

    private static IEnumerable<string> GetSearchDirectories()
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var path = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;

        foreach (var entry in path.Split(Path.PathSeparator))
        {
            var directory = entry.Trim().Trim('"');

            if (directory.Length == 0)
                continue;

            if (seen.Add(directory))
                yield return directory;
        }

        foreach (var directory in WellKnownNodeDirectories())
        {
            if (seen.Add(directory))
                yield return directory;
        }
    }

    private static IReadOnlyList<string> GetExecutableExtensions(string command)
    {
        // A command that already carries an extension (e.g. "omniroute.cmd")
        // must be looked up exactly as written.
        if (!string.IsNullOrEmpty(Path.GetExtension(command)))
            return [string.Empty];

        var pathExt = Environment.GetEnvironmentVariable("PATHEXT");

        string[] extensions = string.IsNullOrWhiteSpace(pathExt)
            ? new[] { ".EXE", ".CMD", ".BAT", ".COM" }
            : pathExt.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        // ".exe" first: a real PE binary can be started without any shell,
        // which is exactly what we want for node. The empty extension goes
        // last so an extension-less file (a Git-Bash style shell script) is
        // only ever picked when nothing launchable was found before it.
        return extensions
            .OrderBy(extension =>
                extension.Equals(".EXE", StringComparison.OrdinalIgnoreCase) ? 0 : 1)
            .Append(string.Empty)
            .ToList();
    }
}
