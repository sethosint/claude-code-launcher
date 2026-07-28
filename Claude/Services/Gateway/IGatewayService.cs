using ClaudeLauncher.Core.Models;
using ClaudeLauncher.Core.Results;

namespace ClaudeLauncher.Services.Gateway;

public interface IGatewayService
{
    Task<Result> EnsureStartedAsync(
        LaunchContext context);
}