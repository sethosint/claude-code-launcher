using ClaudeLauncher.Core.Models;
using ClaudeLauncher.Core.Results;

namespace ClaudeLauncher.Services.Launcher;

public interface ILaunchPipeline
{
    Task<Result> RunAsync(
        LaunchContext context);
}