using System.IO;

namespace ClaudeLauncher.Services.Process;

/// <summary>
/// Decides whether an executable needs to be launched through cmd.exe
/// (via "cmd.exe /s /c ...") instead of directly through CreateProcess, and
/// builds a correctly quoted command line for that case.
///
/// Windows' CreateProcess can only launch true PE binaries (.exe) directly.
/// Script-style executables — .cmd/.bat batch files — must go through cmd.exe
/// or the OS refuses to start them ("is not a valid Win32 application").
/// Bare command names (e.g. "node") are resolved to a full path by
/// <see cref="ExecutableResolver"/> before we get here, so they normally do
/// NOT need the shell at all.
///
/// Bug history (two separate ones, both surfaced as "gateway failed to start"):
///
/// 1. An earlier version checked <see cref="Path.IsPathRooted"/> before the
///    extension and returned false for a rooted ".cmd" file (OmniRoute's
///    default install path) — the extension must be checked first.
///
/// 2. The cmd.exe command line was built as
///    <c>/c "node" "C:\...\server.js" --no-launch</c>. Per `cmd /?`, quotes
///    are preserved only when there are EXACTLY TWO quote characters and the
///    text between them names an executable; otherwise cmd strips the first
///    and the last quote character. With four quotes it became
///    <c>node" "C:\...\server.js --no-launch</c>, which cmd cannot run — it
///    exited with code 1 after ~12 ms, so the Completions proxy never came
///    up while Kiro (exactly two quotes) worked fine. The fix is the
///    canonical form <c>cmd.exe /s /c "..."</c>: with /s, cmd removes the
///    first and last character when the first character is a quote, so the
///    whole command must be wrapped in one extra pair of quotes.
/// </summary>
public static class CommandShellHelper
{
    public static bool RequiresCommandShell(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            return false;

        var extension = Path.GetExtension(fileName);

        if (extension.Equals(".cmd", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".bat", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (Path.IsPathRooted(fileName))
            return false;

        // A bare, extension-less name can only be found through PATH+PATHEXT,
        // which CreateProcess does not do for scripts. ExecutableResolver
        // normally resolves these first; the shell stays as the fallback.
        return string.IsNullOrWhiteSpace(extension);
    }

    /// <summary>
    /// Builds the full argument string for cmd.exe so that both the executable
    /// path and its arguments survive cmd's quote stripping intact.
    /// </summary>
    public static string BuildShellArguments(string fileName, string arguments)
    {
        var command = string.IsNullOrWhiteSpace(arguments)
            ? $"\"{fileName}\""
            : $"\"{fileName}\" {arguments}";

        return $"/s /c \"{command}\"";
    }
}
