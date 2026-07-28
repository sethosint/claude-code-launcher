using ClaudeLauncher.Config;
using ClaudeLauncher.Core.Results;
using Microsoft.Extensions.Configuration;
using System.IO;
using System.Text.Json;
using System.Windows;

namespace ClaudeLauncher.Services.Configuration;

public sealed class ConfigurationService : IConfigurationService
{
    public ConfigurationService()
    {
    }


    public Result<AppConfig> GetConfiguration()
    {
        try
        {
            IConfigurationRoot configuration = new ConfigurationBuilder()
                .SetBasePath(AppContext.BaseDirectory)
                .AddJsonFile(
                    "config.json",
                    optional: false,
                    reloadOnChange: false)
                .Build();

            var config = configuration.Get<AppConfig>()
                         ?? new AppConfig();

            return Result<AppConfig>.Success(config);
        }
        catch (Exception ex)
        {
            return Result<AppConfig>.Failure(ex.Message);
        }
    }


    public Result SaveConfiguration(AppConfig configuration)
    {
        try
        {
            var path = Path.Combine(
                AppContext.BaseDirectory,
                "config.json");

            var json = JsonSerializer.Serialize(
                configuration,
                new JsonSerializerOptions
                {
                    WriteIndented = true
                });

            File.WriteAllText(path, json);

            return Result.Success();
        }
        catch (Exception ex)
        {
            return Result.Failure(ex.Message);
        }
    }
}