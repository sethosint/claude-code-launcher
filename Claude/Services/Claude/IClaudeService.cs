using ClaudeLauncher.Core.Models;
using ClaudeLauncher.Core.Results;

namespace ClaudeLauncher.Services.Claude;

public interface IClaudeService
{
    Task<Result> LaunchAsync(LaunchContext context);
}