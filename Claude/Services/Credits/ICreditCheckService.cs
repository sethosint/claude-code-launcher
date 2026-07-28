using ClaudeLauncher.Providers;

namespace ClaudeLauncher.Services.Credits;

public interface ICreditCheckService
{
    /// <summary>
    /// Performs a lightweight call against the provider to determine whether
    /// it currently has usable credits/quota.
    /// </summary>
    Task<CreditStatus> CheckAsync(
        IProvider provider,
        CancellationToken cancellationToken = default);
}
