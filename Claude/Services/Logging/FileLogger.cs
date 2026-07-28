using System.IO;
using System.Text;

namespace ClaudeLauncher.Services.Logging;

public sealed class FileLogger : ILogger
{
    private readonly string _logDirectory;
    private readonly string _logFile;
    private readonly Lock _lock = new();

    public FileLogger()
    {
        _logDirectory = Path.Combine(
            AppContext.BaseDirectory,
            "logs");

        Directory.CreateDirectory(_logDirectory);

        _logFile = Path.Combine(
            _logDirectory,
            $"launcher-{DateTime.Now:yyyy-MM-dd}.log");
    }

    public void Info(string message)
        => Write("INFO", message);

    public void Warning(string message)
        => Write("WARN", message);

    public void Error(string message)
        => Write("ERROR", message);

    public void Error(Exception exception)
        => Write("ERROR", exception.ToString());

    private void Write(
        string level,
        string message)
    {
        var line =
            $"[{DateTime.Now:HH:mm:ss}] [{level}] {message}";

        lock (_lock)
        {
            File.AppendAllText(
                _logFile,
                line + Environment.NewLine,
                Encoding.UTF8);
        }
    }
}