using ClaudeLauncher.Providers;

namespace ClaudeLauncher.Services.Access;

public interface IProviderAccessService
{
    /// <summary>
    /// True when a credential probe makes sense for this provider: it talks to
    /// a remote host over http(s) and has a key to verify. Local gateways
    /// (OmniRoute on localhost) are excluded — before launch they are not even
    /// running yet, so a probe would always be inconclusive.
    /// </summary>
    bool CanCheck(IProvider provider);

    Task<AccessCheckResult> CheckAsync(
        IProvider provider,
        CancellationToken cancellationToken = default);
}
