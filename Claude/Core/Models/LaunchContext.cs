using ClaudeLauncher.Config;
using ClaudeLauncher.Providers;

namespace ClaudeLauncher.Core.Models;

public sealed class LaunchContext
{
    public AppConfig Configuration { get; }

    public IProvider Provider { get; set; }

    public CancellationToken CancellationToken { get; }

    public DateTime StartedAtUtc { get; }

    public IProgress<LaunchProgress>? Progress { get; }

    public LaunchContext(
    AppConfig configuration,
    IProvider provider,
    CancellationToken cancellationToken,
    IProgress<LaunchProgress>? progress = null)
    {
        Configuration = configuration
            ?? throw new ArgumentNullException(nameof(configuration));

        Provider = provider
            ?? throw new ArgumentNullException(nameof(provider));

        CancellationToken = cancellationToken;
        Progress = progress;
        StartedAtUtc = DateTime.UtcNow;
    }
}