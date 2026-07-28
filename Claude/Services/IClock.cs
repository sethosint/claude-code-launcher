namespace ClaudeLauncher.Services;

public interface IClock
{
    DateTime Now { get; }

    DateTime UtcNow { get; }
}