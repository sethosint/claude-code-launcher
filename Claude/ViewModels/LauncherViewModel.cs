using ClaudeLauncher.Config;
using ClaudeLauncher.Core.Commands;
using ClaudeLauncher.Core.Enums;
using ClaudeLauncher.Core.Models;
using ClaudeLauncher.Services.Access;
using ClaudeLauncher.Services.Configuration;
using ClaudeLauncher.Services.Launcher;
using ClaudeLauncher.Services.Models;
using ClaudeLauncher.UI.Models;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows;

namespace ClaudeLauncher.ViewModels;

public sealed class LauncherViewModel : ViewModelBase
{
    private readonly ILauncherEngine _launcher;
    private readonly IConfigurationService _configurationService;
    private readonly IProviderModelService _providerModelService;
    private readonly IProviderAccessService _providerAccessService;
    private readonly Providers.IProviderFactory _providerFactory;

    /// <summary>
    /// The live configuration this window edits. The main window is now a real
    /// launcher surface — project folder, provider and model are changed here
    /// and persisted immediately — so it keeps its own copy instead of asking
    /// the Settings dialog for one.
    /// </summary>
    private AppConfig _configuration = new();

    private bool _isSuppressingPersistence;

    private string _title = "Ready";
    public string Title
    {
        get => _title;
        set => SetProperty(ref _title, value);
    }

    private string _description = "Waiting for launch...";
    public string Description
    {
        get => _description;
        set => SetProperty(ref _description, value);
    }

    private double _progress;
    public double Progress
    {
        get => _progress;
        set => SetProperty(ref _progress, value);
    }

    private string _activeProviderName = "";
    public string ActiveProviderName
    {
        get => _activeProviderName;
        set => SetProperty(ref _activeProviderName, value);
    }

    private LauncherUiState _uiState = LauncherUiState.Idle;

    public LauncherUiState UiState
    {
        get => _uiState;
        set
        {
            if (!SetProperty(ref _uiState, value))
                return;

            OnPropertyChanged(nameof(IsIdle));
            OnPropertyChanged(nameof(IsStarting));
            OnPropertyChanged(nameof(IsFailed));
            OnPropertyChanged(nameof(IsCompleted));
            OnPropertyChanged(nameof(IsNotStarting));
            OnPropertyChanged(nameof(CanShowLogs));

            RetryCommand.RaiseCanExecuteChanged();
            CancelCommand.RaiseCanExecuteChanged();
            LaunchCommand.RaiseCanExecuteChanged();
        }
    }

    public bool IsIdle =>
    UiState == LauncherUiState.Idle;

    public bool IsStarting =>
        UiState == LauncherUiState.Starting;

    public bool IsFailed =>
        UiState == LauncherUiState.Failed;

    public bool IsCompleted =>
        UiState == LauncherUiState.Completed;

    public bool IsNotStarting =>
        UiState != LauncherUiState.Starting;

    public bool CanShowLogs =>
        UiState != LauncherUiState.Starting;

    public ObservableCollection<LaunchLogItem> LaunchLog { get; } = [];

    public bool HasLogs => LaunchLog.Count > 0;

    /// <summary>
    /// Fixed, ordered checklist of launch steps. Some steps (switching
    /// provider, starting the gateway) are conditional and end up marked
    /// "Skipped" for runs where they never happen.
    /// </summary>
    public ObservableCollection<LaunchStepItem> LaunchSteps { get; } =
    [
        new("Initialize"),
        new("Load configuration"),
        new("Check provider access"),
        new("Switch provider"),
        new("Start gateway"),
        new("Launch Claude"),
        new("Ready")
    ];

    private int _lastStepIndex = -1;

    private int _activeStepIndex = -1;

    /// <summary>
    /// Index of the step the user should currently be looking at (the active
    /// one, or the failed one after an error). The view watches this and
    /// scrolls that row into view, so the checklist never *looks* truncated
    /// even when it is taller than the card.
    /// </summary>
    public int ActiveStepIndex
    {
        get => _activeStepIndex;
        private set => SetProperty(ref _activeStepIndex, value);
    }

    // ── Session: project folder ──────────────────────────────────────────

    private string _projectPath = "";

    /// <summary>
    /// Folder Claude Code will open. This is the single most important thing
    /// a launcher decides: without it Claude started in the launcher's own
    /// bin\Debug directory and saw no project at all.
    /// </summary>
    public string ProjectPath
    {
        get => _projectPath;
        private set
        {
            if (!SetProperty(ref _projectPath, value))
                return;

            OnPropertyChanged(nameof(ProjectName));
            OnPropertyChanged(nameof(ProjectPathDisplay));
        }
    }

    public string ProjectName =>
        string.IsNullOrWhiteSpace(ProjectPath)
            ? "No project selected"
            : new DirectoryInfo(ProjectPath.TrimEnd(Path.DirectorySeparatorChar)).Name;

    /// <summary>The full path with the user profile collapsed to "~", the way a shell prompt shows it.</summary>
    public string ProjectPathDisplay
    {
        get
        {
            if (string.IsNullOrWhiteSpace(ProjectPath))
                return "Claude will start in the launcher's own folder";

            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

            return !string.IsNullOrEmpty(home) &&
                   ProjectPath.StartsWith(home, StringComparison.OrdinalIgnoreCase)
                ? "~" + ProjectPath[home.Length..]
                : ProjectPath;
        }
    }

    public ObservableCollection<string> RecentProjects { get; } = [];

    private string? _selectedRecentProject;

    /// <summary>Bound to the "recent" dropdown; selecting an entry switches the project.</summary>
    public string? SelectedRecentProject
    {
        get => _selectedRecentProject;
        set
        {
            if (!SetProperty(ref _selectedRecentProject, value))
                return;

            if (!string.IsNullOrWhiteSpace(value))
            {
                SetProject(value);
            }
        }
    }

    public bool HasRecentProjects => RecentProjects.Count > 0;

    // ── Session: provider ────────────────────────────────────────────────

    public bool IsKiroSelected => _configuration.Provider == ProviderType.Kiro;

    public bool IsCompletionsSelected => _configuration.Provider == ProviderType.Completions;

    /// <summary>
    /// The whole request path on one line — where Claude connects and where
    /// that hop forwards to. A wrong endpoint (the single most common cause of
    /// a 401 inside the Claude session) is then visible before launching.
    /// </summary>
    public string RouteDisplay =>
        IsKiroSelected
            ? $"claude \u2192 OmniRoute {Shorten(_configuration.Gateway.BaseUrl, "not configured")}"
            : $"claude \u2192 proxy {Shorten(_configuration.CompletionsGateway.BaseUrl, "localhost:4242")}" +
              $" \u2192 {Shorten(_configuration.Completions.BaseUrl, "not configured")}";

    // ── Session: model ───────────────────────────────────────────────────

    public ObservableCollection<AiModel> AvailableModels { get; } = [];

    private AiModel? _selectedModel;

    public AiModel? SelectedModel
    {
        get => _selectedModel;
        set
        {
            if (!SetProperty(ref _selectedModel, value))
                return;

            if (value is null || _isSuppressingPersistence)
                return;

            if (IsKiroSelected)
            {
                _configuration.Claude.Model = value.Id;
            }
            else
            {
                _configuration.Completions.Model = value.Id;
            }

            Persist();
        }
    }

    private bool _isLoadingModels;
    public bool IsLoadingModels
    {
        get => _isLoadingModels;
        private set => SetProperty(ref _isLoadingModels, value);
    }

    private string _modelsStatus = "";
    public string ModelsStatus
    {
        get => _modelsStatus;
        private set
        {
            if (SetProperty(ref _modelsStatus, value))
            {
                OnPropertyChanged(nameof(HasModelsStatus));
            }
        }
    }

    public bool HasModelsStatus => !string.IsNullOrWhiteSpace(ModelsStatus);

    // ── Session: credentials ─────────────────────────────────────────────

    private string _accessSummary = "Not checked";
    public string AccessSummary
    {
        get => _accessSummary;
        private set => SetProperty(ref _accessSummary, value);
    }

    private AccessState _accessState = AccessState.Unknown;

    /// <summary>Verdict of the last credential probe; drives the colour of the access chip.</summary>
    public AccessState AccessStatus
    {
        get => _accessState;
        private set
        {
            if (!SetProperty(ref _accessState, value))
                return;

            OnPropertyChanged(nameof(AccessIsOk));
            OnPropertyChanged(nameof(AccessIsBad));
        }
    }

    public bool AccessIsOk => AccessStatus == AccessState.Ok;

    public bool AccessIsBad => AccessStatus is AccessState.Invalid or AccessState.Exhausted;

    private bool _isCheckingAccess;
    public bool IsCheckingAccess
    {
        get => _isCheckingAccess;
        private set => SetProperty(ref _isCheckingAccess, value);
    }

    // ── Session: behaviour ───────────────────────────────────────────────

    public bool LaunchWithoutProject
    {
        get => _configuration.Launcher.LaunchWithoutProject;
        set
        {
            if (_configuration.Launcher.LaunchWithoutProject == value)
                return;

            _configuration.Launcher.LaunchWithoutProject = value;

            OnPropertyChanged();
            Persist();
        }
    }

    public bool CloseAfterLaunch
    {
        get => _configuration.Launcher.CloseAfterLaunch;
        set
        {
            if (_configuration.Launcher.CloseAfterLaunch == value)
                return;

            _configuration.Launcher.CloseAfterLaunch = value;

            OnPropertyChanged();
            Persist();
        }
    }

    /// <summary>
    /// Switches off Claude Code's telemetry, error reporting, update checks
    /// and terminal-title updates for the launched session.
    /// </summary>
    public bool PrivacyMode
    {
        get => _configuration.Launcher.PrivacyMode;
        set
        {
            if (_configuration.Launcher.PrivacyMode == value)
                return;

            _configuration.Launcher.PrivacyMode = value;

            OnPropertyChanged();
            Persist();
        }
    }

    public string VersionText
    {
        get
        {
            var version = typeof(LauncherViewModel).Assembly.GetName().Version;

            return version is null ? "" : $"v{version.Major}.{version.Minor}.{version.Build}";
        }
    }

    // ── Commands ─────────────────────────────────────────────────────────

    public RelayCommand LaunchCommand { get; }

    public RelayCommand OpenLogsCommand { get; }

    public RelayCommand OpenTelegramCommand { get; }

    public RelayCommand RetryCommand { get; }

    public RelayCommand CancelCommand { get; }

    public RelayCommand RelaunchCommand { get; }

    public RelayCommand ExitCommand { get; }

    public RelayCommand BrowseProjectCommand { get; }

    public RelayCommand OpenProjectFolderCommand { get; }

    public RelayCommand SelectKiroCommand { get; }

    public RelayCommand SelectCompletionsCommand { get; }

    public RelayCommand RefreshModelsCommand { get; }

    public RelayCommand CheckAccessCommand { get; }

    public RelayCommand CopyLogCommand { get; }

    public LauncherViewModel(
        ILauncherEngine launcher,
        IConfigurationService configurationService,
        IProviderModelService providerModelService,
        IProviderAccessService providerAccessService,
        Providers.IProviderFactory providerFactory)
    {
        _launcher = launcher;
        _configurationService = configurationService;
        _providerModelService = providerModelService;
        _providerAccessService = providerAccessService;
        _providerFactory = providerFactory;

        var configResult = configurationService.GetConfiguration();

        if (configResult.IsSuccess && configResult.Value is not null)
        {
            _configuration = configResult.Value;
        }

        ActiveProviderName = _configuration.Provider == ProviderType.Kiro
            ? "Kiro"
            : "Custom API";

        _projectPath = _configuration.Launcher.WorkingDirectory ?? "";

        foreach (var recent in _configuration.Launcher.RecentProjects)
        {
            RecentProjects.Add(recent);
        }

        _launcher.StatusChanged += OnStatusChanged;

        LaunchCommand = new RelayCommand(
            () => _ = InitializeAsync(),
            () => UiState != LauncherUiState.Starting);

        OpenLogsCommand = new RelayCommand(OpenLogs);

        OpenTelegramCommand = new RelayCommand(OpenTelegram);

        RetryCommand = new RelayCommand(
            () => _ = InitializeAsync(),
            () =>
                UiState == LauncherUiState.Failed ||
                UiState == LauncherUiState.Cancelled);

        CancelCommand = new RelayCommand(
            Cancel,
            () => UiState == LauncherUiState.Starting);

        RelaunchCommand = new RelayCommand(RelaunchApplication);

        ExitCommand = new RelayCommand(() => Application.Current.Shutdown());

        BrowseProjectCommand = new RelayCommand(BrowseProject);

        OpenProjectFolderCommand = new RelayCommand(
            OpenProjectFolder,
            () => Directory.Exists(ProjectPath));

        SelectKiroCommand = new RelayCommand(
            () => SelectProvider(ProviderType.Kiro),
            () => UiState != LauncherUiState.Starting);

        SelectCompletionsCommand = new RelayCommand(
            () => SelectProvider(ProviderType.Completions),
            () => UiState != LauncherUiState.Starting);

        RefreshModelsCommand = new RelayCommand(
            () => _ = LoadModelsAsync(),
            () => !IsLoadingModels);

        CheckAccessCommand = new RelayCommand(
            () => _ = CheckAccessAsync(),
            () => !IsCheckingAccess);

        CopyLogCommand = new RelayCommand(CopyLog, () => LaunchLog.Count > 0);

        _ = LoadModelsAsync();
    }

    public async Task InitializeAsync()
    {
        if (UiState == LauncherUiState.Starting)
            return;

        LaunchLog.Clear();
        ResetSteps();

        Title = "Starting...";
        Description = "Initializing...";
        Progress = 0;

        UiState = LauncherUiState.Starting;

        // The engine reads config.json from disk, so anything changed on this
        // screen must already be on disk by now — it is, every setter persists
        // immediately — and the chosen project is remembered for next time.
        RememberProject(ProjectPath);

        await _launcher.InitializeAsync();

        if (!string.IsNullOrWhiteSpace(_launcher.LastAccessSummary))
        {
            AccessSummary = _launcher.LastAccessSummary!;

            AccessStatus = UiState == LauncherUiState.Failed
                ? AccessState.Invalid
                : AccessState.Ok;
        }

        if (UiState != LauncherUiState.Failed &&
            UiState != LauncherUiState.Cancelled)
        {
            UiState = LauncherUiState.Completed;
        }
    }

    /// <summary>
    /// Re-reads config.json. Called after the Settings dialog closes: this
    /// window keeps the configuration in memory and writes it back whenever a
    /// field changes here, so without a reload the next toggle on this screen
    /// would happily overwrite whatever Settings just saved.
    /// </summary>
    public void ReloadConfiguration()
    {
        var result = _configurationService.GetConfiguration();

        if (result.IsFailure || result.Value is null)
            return;

        _configuration = result.Value;

        ProjectPath = _configuration.Launcher.WorkingDirectory ?? "";

        RecentProjects.Clear();

        foreach (var recent in _configuration.Launcher.RecentProjects)
        {
            RecentProjects.Add(recent);
        }

        ActiveProviderName = _configuration.Provider == ProviderType.Kiro
            ? "Kiro"
            : "Custom API";

        AccessSummary = "Not checked";
        AccessStatus = AccessState.Unknown;

        OnPropertyChanged(nameof(IsKiroSelected));
        OnPropertyChanged(nameof(IsCompletionsSelected));
        OnPropertyChanged(nameof(RouteDisplay));
        OnPropertyChanged(nameof(LaunchWithoutProject));
        OnPropertyChanged(nameof(CloseAfterLaunch));
        OnPropertyChanged(nameof(PrivacyMode));
        OnPropertyChanged(nameof(HasRecentProjects));

        _ = LoadModelsAsync();
    }

    // ── Project folder ───────────────────────────────────────────────────

    private void BrowseProject()
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog
        {
            Title = "Choose the project folder Claude Code should open",
            Multiselect = false
        };

        if (Directory.Exists(ProjectPath))
        {
            dialog.InitialDirectory = ProjectPath;
        }

        if (dialog.ShowDialog() == true)
        {
            SetProject(dialog.FolderName);
        }
    }

    private void SetProject(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return;

        ProjectPath = path;

        _configuration.Launcher.WorkingDirectory = path;

        RememberProject(path);

        Persist();

        OpenProjectFolderCommand.RaiseCanExecuteChanged();
    }

    /// <summary>Moves a folder to the top of the recent list, de-duplicated and capped.</summary>
    private void RememberProject(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return;

        var existing = RecentProjects
            .FirstOrDefault(x => string.Equals(x, path, StringComparison.OrdinalIgnoreCase));

        if (existing is not null)
        {
            RecentProjects.Remove(existing);
        }

        RecentProjects.Insert(0, path);

        while (RecentProjects.Count > LauncherConfig.RecentProjectsLimit)
        {
            RecentProjects.RemoveAt(RecentProjects.Count - 1);
        }

        _configuration.Launcher.RecentProjects = RecentProjects.ToList();

        OnPropertyChanged(nameof(HasRecentProjects));
    }

    private void OpenProjectFolder()
    {
        if (!Directory.Exists(ProjectPath))
            return;

        Process.Start(new ProcessStartInfo
        {
            FileName = ProjectPath,
            UseShellExecute = true
        });
    }

    // ── Provider / model ─────────────────────────────────────────────────

    private void SelectProvider(ProviderType provider)
    {
        if (_configuration.Provider == provider)
            return;

        _configuration.Provider = provider;

        Persist();

        ActiveProviderName = provider == ProviderType.Kiro ? "Kiro" : "Custom API";

        AccessSummary = "Not checked";
        AccessStatus = AccessState.Unknown;

        OnPropertyChanged(nameof(IsKiroSelected));
        OnPropertyChanged(nameof(IsCompletionsSelected));
        OnPropertyChanged(nameof(RouteDisplay));

        _ = LoadModelsAsync();
    }

    private async Task LoadModelsAsync()
    {
        if (IsLoadingModels)
            return;

        IsLoadingModels = true;
        ModelsStatus = "Loading models...";

        try
        {
            var models = await _providerModelService.GetModelsAsync(_configuration);

            var currentId = IsKiroSelected
                ? _configuration.Claude.Model
                : _configuration.Completions.Model;

            _isSuppressingPersistence = true;

            AvailableModels.Clear();

            foreach (var model in models)
            {
                AvailableModels.Add(model);
            }

            // Keep a model that is configured but missing from the catalog
            // visible instead of silently resetting the user's choice.
            if (!string.IsNullOrWhiteSpace(currentId) &&
                AvailableModels.All(x => x.Id != currentId))
            {
                AvailableModels.Insert(0, new AiModel { Id = currentId, Name = currentId });
            }

            SelectedModel = AvailableModels.FirstOrDefault(x => x.Id == currentId)
                ?? AvailableModels.FirstOrDefault();

            _isSuppressingPersistence = false;

            ModelsStatus = AvailableModels.Count == 0
                ? "No models returned by the provider"
                : "";
        }
        catch (Exception ex)
        {
            _isSuppressingPersistence = false;

            ModelsStatus = $"Could not load models: {ex.Message}";
        }
        finally
        {
            IsLoadingModels = false;

            RefreshModelsCommand.RaiseCanExecuteChanged();
        }
    }

    private async Task CheckAccessAsync()
    {
        if (IsCheckingAccess)
            return;

        IsCheckingAccess = true;
        AccessSummary = "Checking...";
        AccessStatus = AccessState.Unknown;

        try
        {
            var provider = _providerFactory.Create(_configuration);

            if (!_providerAccessService.CanCheck(provider))
            {
                AccessSummary = IsKiroSelected
                    ? "Local gateway — checked at launch"
                    : "Nothing to check yet";

                AccessStatus = AccessState.Unknown;
                return;
            }

            var result = await _providerAccessService.CheckAsync(provider);

            AccessSummary = result.Summary;
            AccessStatus = result.State;
        }
        catch (Exception ex)
        {
            AccessSummary = ex.Message;
            AccessStatus = AccessState.Unknown;
        }
        finally
        {
            IsCheckingAccess = false;

            CheckAccessCommand.RaiseCanExecuteChanged();
        }
    }

    private void Persist()
    {
        var result = _configurationService.SaveConfiguration(_configuration);

        if (result.IsFailure)
        {
            MessageBox.Show(
                $"Could not save settings:{Environment.NewLine}{result.Error}",
                "Save failed",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    private static string Shorten(string? url, string fallback)
    {
        if (string.IsNullOrWhiteSpace(url))
            return fallback;

        return Uri.TryCreate(url, UriKind.Absolute, out var uri)
            ? uri.IsDefaultPort ? uri.Host : $"{uri.Host}:{uri.Port}"
            : url;
    }

    private void CopyLog()
    {
        if (LaunchLog.Count == 0)
            return;

        var builder = new StringBuilder();

        foreach (var entry in LaunchLog)
        {
            builder.AppendLine($"{entry.Timestamp:HH:mm:ss}  {entry.Level,-7}  {entry.Message}");
        }

        try
        {
            Clipboard.SetText(builder.ToString());
        }
        catch
        {
            // Clipboard access can fail if another app holds it open; not worth
            // interrupting the user over.
        }
    }

    private void ResetSteps()
    {
        _lastStepIndex = -1;
        ActiveStepIndex = -1;

        foreach (var step in LaunchSteps)
        {
            step.Reset();
        }
    }

    /// <summary>Maps a launcher state to its index in <see cref="LaunchSteps"/>, or -1 if it has no dedicated row.</summary>
    private static int MapStepIndex(LauncherState state)
    {
        return state switch
        {
            LauncherState.Initializing => 0,
            LauncherState.LoadingConfiguration => 1,
            LauncherState.CheckingProvider => 2,
            LauncherState.SwitchingProvider => 3,
            LauncherState.CheckingGateway
                or LauncherState.StartingGateway
                or LauncherState.WaitingForGateway => 4,
            LauncherState.LaunchingClaude => 5,
            LauncherState.Completed => 6,
            _ => -1
        };
    }

    private void AdvanceSteps(LauncherState state, string detail)
    {
        var index = MapStepIndex(state);

        if (index < 0)
            return;

        // Anything strictly between the last active step and this one was
        // conditional and never happened this run (e.g. no provider switch
        // was needed, or the gateway step doesn't apply to this provider).
        for (var i = _lastStepIndex + 1; i < index; i++)
        {
            if (LaunchSteps[i].IsPending)
            {
                LaunchSteps[i].Status = LaunchStepStatus.Skipped;
            }
        }

        if (_lastStepIndex >= 0 && _lastStepIndex != index)
        {
            LaunchSteps[_lastStepIndex].Status = LaunchStepStatus.Done;
        }

        var current = LaunchSteps[index];

        if (state == LauncherState.Completed)
        {
            current.Status = LaunchStepStatus.Done;
        }
        else
        {
            current.Status = LaunchStepStatus.Active;
            current.Detail = detail;
        }

        _lastStepIndex = index;

        ActiveStepIndex = index;
    }

    private void FailCurrentStep(string message)
    {
        if (_lastStepIndex < 0)
            return;

        var step = LaunchSteps[_lastStepIndex];

        step.Status = LaunchStepStatus.Failed;

        // Keep the reason on the row itself: the status card at the top gets
        // overwritten by the next run, and the user's first instinct is to
        // read the step that has the red mark next to it.
        if (!string.IsNullOrWhiteSpace(message))
        {
            step.Detail = message;
        }

        ActiveStepIndex = _lastStepIndex;
    }

    private void OnStatusChanged(
        object? sender,
        LauncherStatusChangedEventArgs e)
    {
        Title = e.Status.Title;
        Description = e.Status.Description;
        Progress = e.Status.Progress;

        UiState = LauncherStateMapper.Map(e.Status.State);

        if (_launcher.ActiveProvider is not null)
        {
            ActiveProviderName = _launcher.ActiveProvider.DisplayName;
        }

        if (e.Status.State is LauncherState.Failed or LauncherState.Cancelled)
        {
            FailCurrentStep(e.Status.Description);
        }
        else
        {
            AdvanceSteps(e.Status.State, e.Status.Description);
        }

        LaunchLog.Add(new LaunchLogItem(
            MapLogLevel(e.Status.State),
            e.Status.Description));

        OnPropertyChanged(nameof(HasLogs));
    }

    private static LaunchLogLevel MapLogLevel(LauncherState state)
    {
        return state switch
        {
            LauncherState.Failed => LaunchLogLevel.Error,
            LauncherState.Cancelled => LaunchLogLevel.Warning,
            LauncherState.SwitchingProvider => LaunchLogLevel.Warning,
            LauncherState.Completed => LaunchLogLevel.Success,
            _ => LaunchLogLevel.Info
        };
    }

    private void Cancel()
    {
        _launcher.Cancel();
    }

    /// <summary>
    /// Restarts the launcher: starts a fresh instance of the current
    /// executable and shuts this one down.
    /// </summary>
    private static void RelaunchApplication()
    {
        try
        {
            var executablePath = Environment.ProcessPath
                ?? Process.GetCurrentProcess().MainModule?.FileName;

            if (string.IsNullOrWhiteSpace(executablePath))
            {
                MessageBox.Show(
                    "Could not determine the launcher's executable path.",
                    "Relaunch failed",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                return;
            }

            Process.Start(new ProcessStartInfo
            {
                FileName = executablePath,
                UseShellExecute = true
            });

            Application.Current.Shutdown();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Could not relaunch the application:{Environment.NewLine}{ex.Message}",
                "Relaunch failed",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private static void OpenTelegram()
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "https://t.me/sethosint",
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Could not open Telegram:{Environment.NewLine}{ex.Message}",
                "Open Telegram failed",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private void OpenLogs()
    {
        var logsDirectory = Path.Combine(
            AppContext.BaseDirectory,
            "logs");

        Directory.CreateDirectory(logsDirectory);

        Process.Start(new ProcessStartInfo
        {
            FileName = logsDirectory,
            UseShellExecute = true
        });
    }
}
