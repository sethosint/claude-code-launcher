using ClaudeLauncher.Config;
using ClaudeLauncher.Core.Enums;
using ClaudeLauncher.Core.Models;
using ClaudeLauncher.Providers;
using ClaudeLauncher.Services.Claude;
using ClaudeLauncher.Services.Configuration;
using ClaudeLauncher.Services.Access;
using ClaudeLauncher.Services.Credits;
using ClaudeLauncher.Services.Gateway;
using ClaudeLauncher.Services.Logging;

namespace ClaudeLauncher.Services.Launcher;

public sealed class LauncherEngine : ILauncherEngine
{
    private readonly IConfigurationService _configurationService;
    private readonly IEnvironmentValidationService _environmentValidationService;
    private readonly IGatewayService _gatewayService;
    private readonly IClaudeService _claudeService;
    private readonly IProviderFactory _providerFactory;
    private readonly ICreditCheckService _creditCheckService;
    private readonly IProviderAccessService _providerAccessService;
    private readonly ILogger _logger;

    private bool _isRunning;
    private CancellationTokenSource? _cancellationTokenSource;

    public LauncherStatus Status { get; } = new();
    public event EventHandler<LauncherStatusChangedEventArgs>? StatusChanged;

    /// <summary>The provider that ended up being used for the most recent launch attempt.</summary>
    public IProvider? ActiveProvider { get; private set; }

    /// <summary>Short result of the most recent credential pre-flight ("Key valid · $4.21 left"), shown in the UI.</summary>
    public string? LastAccessSummary { get; private set; }

    public LauncherEngine(
        IConfigurationService configurationService,
        IEnvironmentValidationService environmentValidationService,
        IGatewayService gatewayService,
        IClaudeService claudeService,
        IProviderFactory providerFactory,
        ICreditCheckService creditCheckService,
        IProviderAccessService providerAccessService,
        ILogger logger)
    {
        _configurationService = configurationService;
        _environmentValidationService = environmentValidationService;
        _gatewayService = gatewayService;
        _claudeService = claudeService;
        _providerFactory = providerFactory;
        _creditCheckService = creditCheckService;
        _providerAccessService = providerAccessService;
        _logger = logger;
    }

    public async Task InitializeAsync()
    {
        if (_isRunning)
            return;

        _isRunning = true;

        try
        {
            _logger.Info("Launcher initialization started.");

            await SetStateAsync(
                LauncherState.Initializing,
                "Initializing",
                "Preparing launcher...",
                5);

            await SetStateAsync(
                LauncherState.LoadingConfiguration,
                "Configuration",
                "Loading configuration...",
                15);

            _logger.Info("Loading configuration...");

            var configurationResult = _configurationService.GetConfiguration();

            if (configurationResult.IsFailure)
            {
                await FailAsync(configurationResult.Error ?? "Failed to load configuration.");
                return;
            }

            var configuration = configurationResult.Value
                ?? throw new InvalidOperationException("Configuration is null.");

            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = new CancellationTokenSource();

            var cancellationToken = _cancellationTokenSource.Token;

            var provider = _providerFactory.Create(configuration);

            _logger.Info("Configuration loaded.");

            // --- Credit / quota probe with automatic fallback ---

            if (configuration.Launcher.AutoFallback)
            {
                await SetStateAsync(
                    LauncherState.CheckingProvider,
                    provider.DisplayName,
                    $"Checking {provider.DisplayName} availability...",
                    25);

                var creditStatus = await _creditCheckService.CheckAsync(provider, cancellationToken);

                if (creditStatus == CreditStatus.Exhausted)
                {
                    var alternate = _providerFactory.CreateAlternate(configuration);

                    if (alternate.IsConfigured)
                    {
                        _logger.Warning(
                            $"{provider.DisplayName} appears to be out of credits. Switching to {alternate.DisplayName}.");

                        await SetStateAsync(
                            LauncherState.SwitchingProvider,
                            "Switching provider",
                            $"{provider.DisplayName} is out of credits — switching to {alternate.DisplayName}...",
                            30);

                        configuration.Provider = alternate.Id == "kiro"
                            ? ProviderType.Kiro
                            : ProviderType.Completions;

                        provider = alternate;
                    }
                    else
                    {
                        _logger.Warning(
                            $"{provider.DisplayName} appears to be out of credits, but no alternate provider is configured.");
                    }
                }
            }

            // --- Credential pre-flight ---
            //
            // /models is public on both supported catalogs, so the credit probe
            // above cannot tell a valid key from a foreign one. This does: it
            // performs a real authenticated call, and turns the provider's own
            // 401 into a sentence the user can act on *before* Claude starts —
            // instead of a raw JSON error surfacing inside the Claude session
            // minutes later.

            if (configuration.Launcher.PreflightAuthCheck &&
                _providerAccessService.CanCheck(provider))
            {
                await SetStateAsync(
                    LauncherState.CheckingProvider,
                    provider.DisplayName,
                    $"Verifying {provider.DisplayName} credentials...",
                    35);

                var access = await _providerAccessService.CheckAsync(provider, cancellationToken);

                LastAccessSummary = access.Summary;

                if (access.IsBlocking)
                {
                    _logger.Error(new InvalidOperationException(access.Hint ?? access.Summary));

                    await FailAsync(access.Hint ?? access.Summary);
                    return;
                }

                _logger.Info($"Access check: {access.Summary}");

                await SetStateAsync(
                    LauncherState.CheckingProvider,
                    provider.DisplayName,
                    $"{provider.DisplayName}: {access.Summary}",
                    40,
                    minDisplayMilliseconds: 150);
            }

            ActiveProvider = provider;

            var context = new LaunchContext(configuration, provider, cancellationToken);

            _logger.Info($"Using provider: {provider.DisplayName}.");
            _logger.Info("Validating environment...");

            var validationResult = _environmentValidationService.Validate(configuration, provider);

            if (validationResult.IsFailure)
            {
                await FailAsync(validationResult.Error ?? "Environment validation failed.");
                return;
            }

            _logger.Info("Environment validation completed.");

            if (provider.RequiresGateway)
            {
                _logger.Info("Checking gateway...");

                await SetStateAsync(
                    LauncherState.CheckingGateway,
                    "Gateway",
                    provider.Id == "completions"
                        ? "Starting Completions proxy..."
                        : "Checking OmniRoute...",
                    45);

                var gatewayResult = await _gatewayService.EnsureStartedAsync(context);

                if (gatewayResult.IsFailure)
                {
                    if (context.CancellationToken.IsCancellationRequested)
                    {
                        await CancelledAsync("Launch cancelled by user.");
                        return;
                    }

                    await FailAsync(gatewayResult.Error ?? "Failed to start gateway.");
                    return;
                }

                _logger.Info("Gateway is ready.");
            }

            await SetStateAsync(
                LauncherState.LaunchingClaude,
                provider.DisplayName,
                $"Launching Claude via {provider.DisplayName}...",
                80);

            _logger.Info("Launching Claude...");

            var claudeResult = await _claudeService.LaunchAsync(context);

            if (claudeResult.IsFailure)
            {
                if (context.CancellationToken.IsCancellationRequested)
                {
                    await CancelledAsync("Launch cancelled by user.");
                    return;
                }

                await FailAsync(claudeResult.Error ?? "Failed to launch Claude.");
                return;
            }

            _logger.Info("Claude launched successfully.");

            _logger.Info("Launcher completed.");

            await SetStateAsync(
                LauncherState.Completed,
                "Ready",
                $"Claude Code launched successfully via {provider.DisplayName}.",
                100);

            if (configuration.Launcher.CloseAfterLaunch)
            {
                await Task.Delay(500);

                App.Current.Shutdown();
            }
        }
        finally
        {
            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = null;

            _isRunning = false;
        }
    }

    public void Cancel()
    {
        if (!_isRunning)
            return;

        _logger.Info("Launcher initialization cancelled.");

        _cancellationTokenSource?.Cancel();
    }

    private Task FailAsync(string message)
    {
        return SetStateAsync(
            LauncherState.Failed,
            "Failed",
            message,
            Status.Progress,
            minDisplayMilliseconds: 0);
    }

    private Task CancelledAsync(string message)
    {
        return SetStateAsync(
            LauncherState.Cancelled,
            "Cancelled",
            message,
            Status.Progress,
            minDisplayMilliseconds: 0);
    }

    private async Task SetStateAsync(
        LauncherState state,
        string title,
        string description,
        double progress,
        int minDisplayMilliseconds = 350)
    {
        Status.State = state;
        Status.Title = title;
        Status.Description = description;
        Status.Progress = progress;

        StatusChanged?.Invoke(
            this,
            new LauncherStatusChangedEventArgs(Status));

        // Most individual steps (credit probe, validation, etc.) complete in a
        // handful of milliseconds on a fast connection. Without a minimum
        // display time, each step's progress-bar animation (300ms) gets
        // interrupted by the next step's BeginAnimation call before it can
        // visually finish, which reads as "the bar never fills / doesn't
        // update". Holding each step on screen briefly lets the animation
        // actually complete and lets the user read the step label.
        if (minDisplayMilliseconds > 0)
        {
            await Task.Delay(minDisplayMilliseconds);
        }
    }
}
