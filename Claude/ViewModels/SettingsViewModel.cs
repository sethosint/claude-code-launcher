using ClaudeLauncher.Config;
using ClaudeLauncher.Core.Commands;
using ClaudeLauncher.Core.Enums;
using ClaudeLauncher.UI.Helpers;
using ClaudeLauncher.Providers;
using ClaudeLauncher.Services.Configuration;
using ClaudeLauncher.UI.Helpers;
using System.Diagnostics;
using System.IO;
using System.Windows;

namespace ClaudeLauncher.ViewModels;

/// <summary>
/// Settings window: connection details, executable path, provider presets.
/// Provider switching, model picking and launch options live on the main screen.
/// </summary>
public sealed class SettingsViewModel : ViewModelBase
{
    private readonly IConfigurationService _configurationService;
    private readonly IProviderFactory _providerFactory;

    private AppConfig _configuration = new();

    public AppConfig Configuration
    {
        get => _configuration;
        set => SetProperty(ref _configuration, value);
    }

    public RelayCommand SaveCommand { get; }
    public RelayCommand CancelCommand { get; }
    public RelayCommand ToggleKiroTokenVisibilityCommand { get; }
    public RelayCommand ToggleCompletionsApiKeyVisibilityCommand { get; }
    public RelayCommand AutoDetectClaudeCommand { get; }
    public RelayCommand ApplyOpenRouterPresetCommand { get; }

    private bool _isKiroTokenVisible;
    public bool IsKiroTokenVisible
    {
        get => _isKiroTokenVisible;
        set => SetProperty(ref _isKiroTokenVisible, value);
    }

    private bool _isCompletionsApiKeyVisible;
    public bool IsCompletionsApiKeyVisible
    {
        get => _isCompletionsApiKeyVisible;
        set => SetProperty(ref _isCompletionsApiKeyVisible, value);
    }

    private string _claudeDetectStatus = "";
    public string ClaudeDetectStatus
    {
        get => _claudeDetectStatus;
        set => SetProperty(ref _claudeDetectStatus, value);
    }

    private bool _createDesktopShortcut = true;
    public bool CreateDesktopShortcut
    {
        get => _createDesktopShortcut;
        set => SetProperty(ref _createDesktopShortcut, value);
    }

    public event Action<bool>? RequestClose;

    private bool _isFirstRun;
    public bool IsFirstRun
    {
        get => _isFirstRun;
        set
        {
            if (!SetProperty(ref _isFirstRun, value))
                return;

            OnPropertyChanged(nameof(HeaderTitle));
            OnPropertyChanged(nameof(HeaderSubtitle));
            OnPropertyChanged(nameof(SaveButtonText));
            OnPropertyChanged(nameof(ShowCancelButton));
        }
    }

    public string HeaderTitle =>
        IsFirstRun ? "Welcome to Claude Code Launcher" : "Settings";

    public string HeaderSubtitle =>
        IsFirstRun
            ? "Set up at least one provider to get started. Kiro gives you free AI credits right away — just paste your token."
            : "API keys, endpoints and executable paths. Provider and model are chosen on the main screen.";

    public string SaveButtonText =>
        IsFirstRun ? "Get Started" : "Save";

    public bool ShowCancelButton =>
        !IsFirstRun;

    private string _validationMessage = "";
    public string ValidationMessage
    {
        get => _validationMessage;
        set
        {
            if (!SetProperty(ref _validationMessage, value))
                return;
            OnPropertyChanged(nameof(HasValidationMessage));
        }
    }

    public bool HasValidationMessage =>
        !string.IsNullOrWhiteSpace(ValidationMessage);

    public SettingsViewModel(
        IConfigurationService configurationService,
        IProviderFactory providerFactory)
    {
        _configurationService = configurationService;
        _providerFactory = providerFactory;

        var result = _configurationService.GetConfiguration();

        if (result.IsSuccess && result.Value is not null)
        {
            Configuration = ConfigCloner.Clone(result.Value);
        }

        // Auto-fill "claude" as default executable if empty
        if (string.IsNullOrWhiteSpace(Configuration.Claude.Executable))
        {
            Configuration.Claude.Executable = "claude";
        }

        SaveCommand = new RelayCommand(Save);
        CancelCommand = new RelayCommand(Cancel);
        ToggleKiroTokenVisibilityCommand = new RelayCommand(() => IsKiroTokenVisible = !IsKiroTokenVisible);
        ToggleCompletionsApiKeyVisibilityCommand = new RelayCommand(() => IsCompletionsApiKeyVisible = !IsCompletionsApiKeyVisible);
        AutoDetectClaudeCommand = new RelayCommand(AutoDetectClaude);
        ApplyOpenRouterPresetCommand = new RelayCommand(ApplyOpenRouterPreset);
    }

    private void AutoDetectClaude()
    {
        var candidates = new List<string>();

        // Try 'where claude' on Windows
        try
        {
            var psi = new ProcessStartInfo("where", "claude")
            {
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            var process = Process.Start(psi);
            if (process is not null)
            {
                var output = process.StandardOutput.ReadToEnd().Trim();
                process.WaitForExit(5000);
                if (process.ExitCode == 0 && !string.IsNullOrWhiteSpace(output))
                {
                    foreach (var line in output.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
                    {
                        candidates.Add(line.Trim());
                    }
                }
            }
        }
        catch { /* not found */ }

        // Check common npm global paths
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        if (!string.IsNullOrEmpty(appData))
        {
            candidates.Add(Path.Combine(appData, "npm", "claude.cmd"));
        }

        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (!string.IsNullOrEmpty(localAppData))
        {
            candidates.Add(Path.Combine(localAppData, "npm", "claude.cmd"));
        }

        // Check user profile
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (!string.IsNullOrEmpty(userProfile))
        {
            candidates.Add(Path.Combine(userProfile, ".npm-global", "claude.cmd"));
        }

        // Pick the first one that exists
        foreach (var candidate in candidates)
        {
            if (File.Exists(candidate))
            {
                Configuration.Claude.Executable = candidate;
                OnPropertyChanged(nameof(Configuration));
                ClaudeDetectStatus = $"✓ Found: {candidate}";
                return;
            }
        }

        // If 'where' found something, use the simple name
        if (candidates.Count > 0 && candidates[0] == "claude")
        {
            Configuration.Claude.Executable = "claude";
            OnPropertyChanged(nameof(Configuration));
            ClaudeDetectStatus = "✓ 'claude' is available in PATH";
            return;
        }

        // Just default to "claude" and hope PATH has it
        Configuration.Claude.Executable = "claude";
        OnPropertyChanged(nameof(Configuration));
        ClaudeDetectStatus = "⚠ Could not find Claude Code. Make sure it's installed: npm install -g @anthropic-ai/claude-code";
    }

    private void ApplyOpenRouterPreset()
    {
        Configuration.Completions.BaseUrl = "https://openrouter.ai/api/v1";
        OnPropertyChanged(nameof(Configuration));
        ValidationMessage = "";
    }


    private void Save()
    {
        if (IsFirstRun && !IsAnyProviderConfigured())
        {
            ValidationMessage =
                "Enter credentials for at least one provider to continue.";
            return;
        }

        Configuration.Launcher.SetupCompleted = true;

        var result = _configurationService.SaveConfiguration(Configuration);

        if (result.IsSuccess)
        {
            ValidationMessage = "";

            if (IsFirstRun && CreateDesktopShortcut)
            {
                TryCreateDesktopShortcut();
            }

            RequestClose?.Invoke(true);
        }
        else
        {
            ValidationMessage = result.Error ?? "Failed to save settings.";
        }
    }

    private bool IsAnyProviderConfigured()
    {
        try
        {
            return _providerFactory.Create(Configuration).IsConfigured ||
                   _providerFactory.CreateAlternate(Configuration).IsConfigured;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Creates a Windows shortcut (.lnk) on the desktop pointing to the
    /// launcher executable. Uses a PowerShell one-liner to create the COM
    /// WScript.Shell shortcut object — the only dependency-free way to do
    /// this from .NET without P/Invoke or NuGet packages.
    /// </summary>
    private static void TryCreateDesktopShortcut()
    {
        try
        {
            var desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            var exePath = Environment.ProcessPath ?? Process.GetCurrentProcess().MainModule?.FileName;

            if (string.IsNullOrEmpty(desktop) || string.IsNullOrEmpty(exePath))
                return;

            var shortcutPath = Path.Combine(desktop, "Claude Code Launcher.lnk");

            if (File.Exists(shortcutPath))
                return;

            var iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "Icons", "app.ico");

            // PowerShell WScript.Shell shortcut creation
            var ps = $"""
                $ws = New-Object -ComObject WScript.Shell;
                $sc = $ws.CreateShortcut('{shortcutPath.Replace("'", "''")}');
                $sc.TargetPath = '{exePath.Replace("'", "''")}';
                $sc.WorkingDirectory = '{Path.GetDirectoryName(exePath)?.Replace("'", "''") ?? ""}';
                $sc.Description = 'Claude Code Launcher';
                {(File.Exists(iconPath) ? $"$sc.IconLocation = '{iconPath.Replace("'", "''")}'" : "")};
                $sc.Save()
                """;

            var psi = new ProcessStartInfo("powershell", $"-NoProfile -Command \"{ps}\"")
            {
                CreateNoWindow = true,
                UseShellExecute = false
            };

            Process.Start(psi)?.WaitForExit(10000);
        }
        catch
        {
            // Non-critical — silently skip if shortcut creation fails.
        }
    }

    private void Cancel()
    {
        RequestClose?.Invoke(false);
    }
}
