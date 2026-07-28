using ClaudeLauncher.Services;
using ClaudeLauncher.Services.Claude;
using ClaudeLauncher.Services.Configuration;
using ClaudeLauncher.Services.Access;
using ClaudeLauncher.Services.Credits;
using ClaudeLauncher.Services.Gateway;
using ClaudeLauncher.Services.Launcher;
using ClaudeLauncher.Services.Logging;
using ClaudeLauncher.Services.Models;
using ClaudeLauncher.Services.Process;
using ClaudeLauncher.Providers;
using ClaudeLauncher.ViewModels;
using ClaudeLauncher.Views;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Windows;

namespace ClaudeLauncher;

public partial class App : Application
{
    public static IHost Host { get; private set; } = null!;

    protected override async void OnStartup(StartupEventArgs e)
    {
        Host = Microsoft.Extensions.Hosting.Host
            .CreateDefaultBuilder()
            .ConfigureServices((context, services) =>
            {
                ConfigureServices(services);
            })
            .Build();

        await Host.StartAsync();

        if (NeedsFirstRunSetup())
        {
            var setupCompleted = ShowFirstRunSetup();

            if (!setupCompleted)
            {
                Shutdown();
                return;
            }
        }

        var window = Host.Services.GetRequiredService<MainWindow>();

        MainWindow = window;
        window.Show();

        base.OnStartup(e);
    }

    /// <summary>
    /// True only when nothing usable is configured yet: neither provider
    /// has all its required fields filled in, and the wizard hasn't been
    /// completed before. Existing, already-configured installs are never
    /// interrupted by this, even after an update.
    /// </summary>
    private static bool NeedsFirstRunSetup()
    {
        var configurationService = Host.Services.GetRequiredService<IConfigurationService>();
        var providerFactory = Host.Services.GetRequiredService<IProviderFactory>();

        var result = configurationService.GetConfiguration();

        if (result.IsFailure || result.Value is null)
            return true;

        var configuration = result.Value;

        if (configuration.Launcher.SetupCompleted)
            return false;

        var primary = providerFactory.Create(configuration);
        var alternate = providerFactory.CreateAlternate(configuration);

        return !primary.IsConfigured && !alternate.IsConfigured;
    }

    /// <summary>Shows the Settings window as a blocking first-run wizard. Returns true if the user saved valid settings.</summary>
    private static bool ShowFirstRunSetup()
    {
        var setupWindow = Host.Services.GetRequiredService<SettingsWindow>();

        if (setupWindow.DataContext is SettingsViewModel viewModel)
        {
            viewModel.IsFirstRun = true;
        }

        return setupWindow.ShowDialog() == true;
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        await Host.StopAsync();

        Host.Dispose();

        base.OnExit(e);
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<IConfigurationService, ConfigurationService>();

        services.AddSingleton<ILogger, FileLogger>();

        services.AddSingleton<IClock, SystemClock>();

        services.AddSingleton<IProcessRunner, ProcessRunner>();

        services.AddSingleton<IInteractiveProcessRunner, InteractiveProcessRunner>();

        services.AddHttpClient<IGatewayService, GatewayService>();

        services.AddHttpClient<ICreditCheckService, CreditCheckService>();

        services.AddHttpClient<IProviderAccessService, ProviderAccessService>();

        services.AddSingleton<IClaudeService, ClaudeService>();

        services.AddSingleton<IEnvironmentValidationService, EnvironmentValidationService>();

        services.AddSingleton<ILauncherEngine, LauncherEngine>();

        services.AddSingleton<LauncherViewModel>();

        services.AddSingleton<MainWindow>();

        services.AddTransient<SettingsViewModel>();

        services.AddTransient<SettingsWindow>();

        services.AddSingleton<IProviderFactory, ProviderFactory>();

        services.AddHttpClient<IModelsApiClient, ModelsApiClient>();

        services.AddSingleton<IModelService, ModelService>();

        services.AddSingleton<IProviderModelService, ProviderModelService>();
    }
}