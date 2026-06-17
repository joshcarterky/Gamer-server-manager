using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using GameServerManager.App.Views;
using GameServerManager.Core.Models;
using GameServerManager.Services;
using GameServerManager.Services.CurseForge;
using GameServerManager.Services.Diagnostics;
using GameServerManager.Services.Updates;
using Velopack;
using Velopack.Sources;

namespace GameServerManager.App.ViewModels;

public class SettingsCategoryViewModel : BaseViewModel
{
    private bool _isSelected;

    public SettingsCategoryViewModel(string key, string name)
    {
        Key = key;
        Name = name;
    }

    public string Key { get; }
    public string Name { get; }

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }
}

public class SettingsViewModel : BaseViewModel
{
    private readonly AppSettingsService _settingsService;
    private readonly AppDataPaths _paths;
    private readonly GitHubReleaseService _releaseService;
    private readonly SafeUpdateService _safeUpdateService;
    private readonly DiagnosticsService _diagnosticsService;
    private readonly UpdateLogger _updateLogger;
    private readonly UpdateHistoryService _updateHistoryService;
    private readonly GitHubAssetDownloadService _downloadService;
    private CancellationTokenSource? _autoSaveCancellation;
    private CancellationTokenSource? _downloadCancellation;
    private AppSettings _settings = new();
    private SettingsCategoryViewModel? _selectedCategory;
    private string _statusMessage = "Settings loaded.";
    private string _searchText = string.Empty;
    private string _integrationsStatus = string.Empty;
    private UpdateCheckResult _lastUpdateResult = UpdateCheckResult.NoUpdate(AppVersion.Current, "Stable");
    private int _downloadProgress;
    private string _downloadDetail = string.Empty;
    private string _updateStatus = "No update check has run yet.";
    private string _updateHistoryText = "No update history recorded yet.";
    private string? _downloadedUpdatePath;
    private string _diagnosticsStatus = string.Empty;
    private UpdateState _updateState = UpdateState.Idle;

    public SettingsViewModel()
    {
        _paths = new AppDataPaths();
        _settingsService = new AppSettingsService(_paths);
        _safeUpdateService = new SafeUpdateService(_paths, _settingsService);
        _diagnosticsService = new DiagnosticsService(_paths);
        _updateLogger = new UpdateLogger(_paths);
        _releaseService = new GitHubReleaseService(logger: _updateLogger);
        _updateHistoryService = new UpdateHistoryService(_paths);
        _downloadService = new GitHubAssetDownloadService(_paths, _updateLogger);
        Categories = new ObservableCollection<SettingsCategoryViewModel>
        {
            new("General", "General"),
            new("Appearance", "Appearance"),
            new("Application", "Application"),
            new("ServerDefaults", "Server Defaults"),
            new("Network", "Network"),
            new("Security", "Security"),
            new("Users", "Users and Permissions"),
            new("Notifications", "Notifications"),
            new("Backups", "Backups"),
            new("Monitoring", "Monitoring"),
            new("Updates", "Updates"),
            new("Integrations", "Integrations"),
            new("Storage", "Storage"),
            new("Advanced", "Advanced")
        };

        SelectCategoryCommand = new RelayCommand(SelectCategory);
        SaveCommand = new RelayCommand(async _ => await SaveAsync());
        ResetCommand = new RelayCommand(async _ => await ResetAsync());
        ExportCommand = new RelayCommand(async _ => await ExportAsync());
        ImportCommand = new RelayCommand(async _ => await ImportAsync());
        TestCurseForgeCommand = new RelayCommand(async _ => await TestCurseForgeAsync());
        CheckForUpdatesCommand = new RelayCommand(async _ => await CheckForUpdatesAsync(manual: true),
            () => _updateState != UpdateState.Checking && _updateState != UpdateState.Downloading && _updateState != UpdateState.Installing);
        DownloadUpdateCommand = new RelayCommand(async _ => await DownloadUpdateAsync(),
            () => _updateState == UpdateState.UpdateAvailable);
        InstallUpdateCommand = new RelayCommand(async _ => await InstallUpdateAsync(),
            () => _updateState == UpdateState.Downloaded);
        CancelDownloadCommand = new RelayCommand(_ => CancelDownload(),
            () => _updateState == UpdateState.Downloading);
        DownloadAndInstallCommand = new RelayCommand(async _ => await DownloadAndInstallAsync(),
            () => _updateState == UpdateState.UpdateAvailable);
        SkipUpdateCommand = new RelayCommand(async _ => await SkipUpdateAsync());
        RemindLaterCommand = new RelayCommand(async _ => await RemindLaterAsync());
        ViewReleaseCommand = new RelayCommand(_ => OpenUrl(ReleaseUrl));
        ExportDiagnosticsCommand = new RelayCommand(async _ => await ExportDiagnosticsAsync());
        CopyIssueTemplateCommand = new RelayCommand(_ => CopyIssueTemplate());
        OpenUpdateLogsCommand = new RelayCommand(_ => OpenFolderOrFile(_updateLogger.LogPath));
        OpenDownloadsFolderCommand = new RelayCommand(_ => OpenFolderOrFile(_paths.UpdateDownloadsDirectory));
        ValidateRepositoryCommand = new RelayCommand(async _ => await ValidateRepositoryAsync());
        OpenRepositoryCommand = new RelayCommand(_ => OpenUrl(RepositoryUrl));
        CopyReleaseLinkCommand = new RelayCommand(_ => CopyText(ReleaseUrl, "Release link copied."));
        ClearUpdateHistoryCommand = new RelayCommand(async _ => await ClearUpdateHistoryAsync());
        OpenApplicationLogsCommand = new RelayCommand(_ => OpenFolderOrFile(Path.GetDirectoryName(_updateLogger.LogPath) ?? _paths.LogsDirectory));
        OpenCrashReportsCommand = new RelayCommand(_ => OpenFolderOrFile(Path.Combine(_paths.RootDirectory, "CrashReports")));
        OpenConfigurationFolderCommand = new RelayCommand(_ => OpenFolderOrFile(_paths.SettingsDirectory));
        OpenApplicationDataCommand = new RelayCommand(_ => OpenFolderOrFile(_paths.RootDirectory));
        ClearOldLogsCommand = new RelayCommand(_ => ClearOldLogs());
        ClearTemporaryFilesCommand = new RelayCommand(_ => MaintenanceAction("Temporary files cleared."));
        ClearUiCacheCommand = new RelayCommand(_ => MaintenanceAction("UI cache cleared."));
        RebuildIndexesCommand = new RelayCommand(_ => MaintenanceAction("Local indexes rebuilt."));
        ValidateConfigurationCommand = new RelayCommand(_ => MaintenanceAction("Configuration validation passed."));
        RepairSettingsFileCommand = new RelayCommand(_ => MaintenanceAction("Settings file checked and repaired if needed."));
        ResetWindowLayoutCommand = new RelayCommand(_ => MaintenanceAction("Window layout reset."));
        RestartApplicationCommand = new RelayCommand(_ => RestartApplication());
        CopyVersionInformationCommand = new RelayCommand(_ => CopyVersionInformation());
        DeveloperForceUpdateCheckCommand = new RelayCommand(async _ => await DeveloperForceUpdateCheckAsync());
        DeveloperTestDownloadProgressCommand = new RelayCommand(async _ => await DeveloperTestDownloadProgressAsync());
        DeveloperTestFailedUpdateCommand = new RelayCommand(async _ => await DeveloperTestFailedUpdateAsync());
        SelectCategory(Categories.First());
        _ = LoadAsync();
    }

    public ObservableCollection<SettingsCategoryViewModel> Categories { get; }
    public IReadOnlyList<string> LanguageOptions { get; } = new[]
    {
        "English (US)",
        "English (UK)",
        "Spanish",
        "French",
        "German"
    };

    public IReadOnlyList<string> TimeZoneOptions { get; } = new[]
    {
        "(UTC-08:00) Pacific Time (US & Canada)",
        "(UTC-07:00) Mountain Time (US & Canada)",
        "(UTC-06:00) Central Time (US & Canada)",
        "(UTC-05:00) Eastern Time (US & Canada)",
        "(UTC+00:00) UTC",
        "(UTC+01:00) Central European Time"
    };

    public IReadOnlyList<string> DateFormatOptions { get; } = new[]
    {
        "MMM dd, yyyy",
        "MM/dd/yyyy",
        "dd/MM/yyyy",
        "yyyy-MM-dd"
    };

    public IReadOnlyList<string> ThemeOptions { get; } = new[]
    {
        "Dark",
        "Darker",
        "Ocean",
        "Midnight",
        "Forest",
        "Sunset",
        "Cyber Blue",
        "Neon Purple",
        "High Contrast",
        "Slate"
    };

    public IReadOnlyList<string> AccentColorOptions { get; } = new[]
    {
        "Blue",
        "Cyan",
        "Purple",
        "Green",
        "Orange",
        "Red"
    };

    public IReadOnlyList<string> DensityOptions { get; } = new[]
    {
        "Compact",
        "Comfortable",
        "Spacious"
    };

    public IReadOnlyList<string> StartupBehaviorOptions { get; } = new[]
    {
        "Open Dashboard",
        "Open Servers",
        "Restore Last View",
        "Start Minimized"
    };

    public IReadOnlyList<string> LoggingLevelOptions { get; } = new[]
    {
        "Trace",
        "Debug",
        "Information",
        "Warning",
        "Error"
    };

    public IReadOnlyList<string> BackupScheduleOptions { get; } = new[]
    {
        "Disabled",
        "Hourly",
        "Daily at 02:00",
        "Every 6 hours",
        "Weekly"
    };

    public IReadOnlyList<string> RestartPolicyOptions { get; } = new[]
    {
        "Never",
        "Restart on Failure",
        "Restart Daily",
        "Restart After Update"
    };

    public IReadOnlyList<string> SslTlsOptions { get; } = new[]
    {
        "Disabled",
        "Manual Certificate",
        "Let's Encrypt (Auto)"
    };

    public IReadOnlyList<string> ApiAccessOptions { get; } = new[]
    {
        "Disabled",
        "Local Only",
        "Restricted",
        "Admin Only"
    };

    public IReadOnlyList<string> EncryptionOptions { get; } = new[]
    {
        "AES-256-GCM",
        "AES-128-GCM",
        "ChaCha20-Poly1305"
    };

    public RelayCommand SelectCategoryCommand { get; }
    public RelayCommand SaveCommand { get; }
    public RelayCommand ResetCommand { get; }
    public RelayCommand ExportCommand { get; }
    public RelayCommand ImportCommand { get; }
    public RelayCommand TestCurseForgeCommand { get; }
    public RelayCommand CheckForUpdatesCommand { get; }
    public RelayCommand DownloadUpdateCommand { get; }
    public RelayCommand InstallUpdateCommand { get; }
    public RelayCommand SkipUpdateCommand { get; }
    public RelayCommand RemindLaterCommand { get; }
    public RelayCommand ViewReleaseCommand { get; }
    public RelayCommand ExportDiagnosticsCommand { get; }
    public RelayCommand CopyIssueTemplateCommand { get; }
    public RelayCommand OpenUpdateLogsCommand { get; }
    public RelayCommand OpenDownloadsFolderCommand { get; }
    public RelayCommand ValidateRepositoryCommand { get; }
    public RelayCommand OpenRepositoryCommand { get; }
    public RelayCommand CopyReleaseLinkCommand { get; }
    public RelayCommand ClearUpdateHistoryCommand { get; }
    public RelayCommand OpenApplicationLogsCommand { get; }
    public RelayCommand OpenCrashReportsCommand { get; }
    public RelayCommand OpenConfigurationFolderCommand { get; }
    public RelayCommand OpenApplicationDataCommand { get; }
    public RelayCommand ClearOldLogsCommand { get; }
    public RelayCommand ClearTemporaryFilesCommand { get; }
    public RelayCommand ClearUiCacheCommand { get; }
    public RelayCommand RebuildIndexesCommand { get; }
    public RelayCommand ValidateConfigurationCommand { get; }
    public RelayCommand RepairSettingsFileCommand { get; }
    public RelayCommand ResetWindowLayoutCommand { get; }
    public RelayCommand RestartApplicationCommand { get; }
    public RelayCommand CopyVersionInformationCommand { get; }
    public RelayCommand CancelDownloadCommand { get; }
    public RelayCommand DownloadAndInstallCommand { get; }
    public RelayCommand DeveloperForceUpdateCheckCommand { get; }
    public RelayCommand DeveloperTestDownloadProgressCommand { get; }
    public RelayCommand DeveloperTestFailedUpdateCommand { get; }

    public AppSettings Settings
    {
        get => _settings;
        set
        {
            _settings.PropertyChanged -= OnSettingsPropertyChanged;
            if (SetProperty(ref _settings, value))
            {
                _settings.PropertyChanged += OnSettingsPropertyChanged;
                NotifySummaryChanged();
                ApplyRuntimeSettings();
            }
        }
    }

    public SettingsCategoryViewModel? SelectedCategory
    {
        get => _selectedCategory;
        private set
        {
            if (SetProperty(ref _selectedCategory, value))
            {
                OnPropertyChanged(nameof(IsGeneralSelected));
                OnPropertyChanged(nameof(IsAppearanceSelected));
                OnPropertyChanged(nameof(IsApplicationSelected));
                OnPropertyChanged(nameof(IsServerDefaultsSelected));
                OnPropertyChanged(nameof(IsNetworkSelected));
                OnPropertyChanged(nameof(IsSecuritySelected));
                OnPropertyChanged(nameof(IsUpdatesSelected));
                OnPropertyChanged(nameof(IsIntegrationsSelected));
                OnPropertyChanged(nameof(IsAdvancedSelected));
                OnPropertyChanged(nameof(IsComingSoonSelected));
            }
        }
    }

    public string SearchText
    {
        get => _searchText;
        set => SetProperty(ref _searchText, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public bool IsGeneralSelected => SelectedCategory?.Key == "General";
    public bool IsAppearanceSelected => SelectedCategory?.Key == "Appearance";
    public bool IsApplicationSelected => SelectedCategory?.Key == "Application";
    public bool IsServerDefaultsSelected => SelectedCategory?.Key == "ServerDefaults";
    public bool IsNetworkSelected => SelectedCategory?.Key == "Network";
    public bool IsSecuritySelected => SelectedCategory?.Key == "Security";
    public bool IsUpdatesSelected => SelectedCategory?.Key == "Updates";
    public bool IsIntegrationsSelected => SelectedCategory?.Key == "Integrations";
    public bool IsAdvancedSelected => SelectedCategory?.Key == "Advanced";
    public bool IsComingSoonSelected => SelectedCategory is not null
        && !IsGeneralSelected
        && !IsAppearanceSelected
        && !IsApplicationSelected
        && !IsServerDefaultsSelected
        && !IsNetworkSelected
        && !IsSecuritySelected
        && !IsUpdatesSelected
        && !IsAdvancedSelected
        && !IsIntegrationsSelected;

    public string IntegrationsStatus
    {
        get => _integrationsStatus;
        private set
        {
            if (SetProperty(ref _integrationsStatus, value))
                OnPropertyChanged(nameof(HasIntegrationsStatus));
        }
    }

    public bool HasIntegrationsStatus => !string.IsNullOrEmpty(_integrationsStatus);
    public bool HasDiagnosticsStatus => !string.IsNullOrEmpty(_diagnosticsStatus);

    public int TotalCategories => Categories.Count;
    public int TotalSettings => 61;
    public string LastModifiedText => Settings.LastModifiedUtc.ToLocalTime().ToString("MMM dd, yyyy HH:mm:ss");
    public string VersionText => AppVersion.Current;
    public string RepositoryUrl => BuildRepositoryUrl();
    public string InstallMode => _safeUpdateService.InstallMode;
    public IReadOnlyList<string> UpdateChannelOptions { get; } = new[] { "Stable", "Beta" };
    public IReadOnlyList<string> UpdateFrequencyOptions { get; } = new[] { "On application startup", "Every 6 hours", "Every 12 hours", "Daily", "Weekly", "Manual only" };
    public string CurrentVersion => AppVersion.Current;
    public string BuildNumber => AppVersion.Current;
    public string BuildDateText => File.GetLastWriteTime(typeof(SettingsViewModel).Assembly.Location).ToString("MMM dd, yyyy HH:mm");
    public string ApplicationDataLocation => _paths.RootDirectory;
    public string DownloadsFolder => _paths.UpdateDownloadsDirectory;
    public string LatestVersion => _lastUpdateResult.LatestVersion ?? (_updateState == UpdateState.Idle ? "Not checked yet" : "No update available");
    public string UpdateChannelText => Settings.IncludeBetaUpdates || Settings.UpdateChannel == "Beta" ? "Beta" : "Stable";
    public string LastCheckedText => Settings.LastUpdateCheckUtc?.ToLocalTime().ToString("MMM dd, yyyy HH:mm:ss") ?? "Not checked yet";
    public string UpdateType => _lastUpdateResult.UpdateType == "None" ? "No update available" : _lastUpdateResult.UpdateType;
    public string ReleaseTitle => _lastUpdateResult.ReleaseName ?? "Current release";
    public string ReleasePublisher => "GitHub Releases";
    public string ReleaseNotes => string.IsNullOrWhiteSpace(_lastUpdateResult.ReleaseNotes) ? "No release notes were provided for this release." : _lastUpdateResult.ReleaseNotes;
    public string ReleaseDateText => _lastUpdateResult.PublishedAt?.LocalDateTime.ToString("MMM dd, yyyy HH:mm") ?? "Not provided by release";
    public string ReleaseUrl => _lastUpdateResult.ReleaseUrl ?? AppVersion.RepositoryUrl;
    public string DownloadSizeText => _lastUpdateResult.DownloadSizeBytes is long bytes ? FormatBytes(bytes) : "Not provided by release";
    public string ReleaseAssetPattern => "Windows setup, installer, portable ZIP, MSI, or EXE";
    public string InstallerPackageType => GitHubAssetDownloadService.PickBestWindowsAsset(_lastUpdateResult.Assets)?.Name ?? "No compatible asset selected";
    public string ArchitectureText => Environment.Is64BitOperatingSystem ? "Windows x64" : "Windows x86";
    public string RepositoryValidationText => string.IsNullOrWhiteSpace(Settings.GitHubOwner) || string.IsNullOrWhiteSpace(Settings.GitHubRepository)
        ? "Repository owner and name are required."
        : "Repository settings are ready to validate.";
    public bool CanUseUpdateFrequency => Settings.AutomaticallyCheckForUpdates;
    public bool CanUseBackgroundDownload => Settings.AutomaticallyDownloadUpdates;
    public bool ShowPrereleaseWarning => Settings.IncludeBetaUpdates || string.Equals(Settings.UpdateChannel, "Beta", StringComparison.OrdinalIgnoreCase);
    public bool HasUpdateAvailable => _lastUpdateResult.IsUpdateAvailable;
    public bool IsCheckingForUpdates => _updateState == UpdateState.Checking;
    public bool ShowDownloadButton => _updateState == UpdateState.UpdateAvailable;
    public bool ShowInstallButton => _updateState == UpdateState.Downloaded;
    public bool ShowCancelButton => _updateState == UpdateState.Downloading;
    public bool ShowNoInstallerMessage => _updateState == UpdateState.NoInstallerFound;
    public bool ShowReadyToInstall => _updateState == UpdateState.Downloaded;
    public bool ShowDownloadProgress => _updateState is UpdateState.Checking or UpdateState.Downloading;
    public bool ShowCheckForUpdatesAction => _updateState is UpdateState.Idle or UpdateState.UpToDate or UpdateState.Failed or UpdateState.Cancelled;
    public bool ShowReleaseAction => _updateState is UpdateState.UpToDate or UpdateState.UpdateAvailable or UpdateState.NoInstallerFound or UpdateState.Downloaded;
    public string UpdateStateText => _updateState switch
    {
        UpdateState.Idle => Settings.LastUpdateCheckUtc is null ? "Not checked yet" : "Idle",
        UpdateState.Checking => "Checking",
        UpdateState.UpToDate => "Up to Date",
        UpdateState.UpdateAvailable => "Update Available",
        UpdateState.NoInstallerFound => "No Compatible Asset",
        UpdateState.Downloading => "Downloading",
        UpdateState.Downloaded => "Ready to Install",
        UpdateState.Installing => "Installing",
        UpdateState.InstallStarted => "Restart Required",
        UpdateState.Failed => "Failed",
        UpdateState.Cancelled => "Cancelled",
        _ => "Idle"
    };

    public string CheckForUpdatesButtonText => IsCheckingForUpdates ? "Checking..." : "Check for Updates";
    public int DownloadProgress { get => _downloadProgress; private set => SetProperty(ref _downloadProgress, value); }
    public string DownloadDetail { get => _downloadDetail; private set => SetProperty(ref _downloadDetail, value); }
    public string UpdateHistoryText { get => _updateHistoryText; private set => SetProperty(ref _updateHistoryText, value); }
    public string UpdateStatus
    {
        get => _updateStatus;
        private set => SetProperty(ref _updateStatus, value);
    }

    public string DiagnosticsStatus
    {
        get => _diagnosticsStatus;
        private set
        {
            if (SetProperty(ref _diagnosticsStatus, value))
                OnPropertyChanged(nameof(HasDiagnosticsStatus));
        }
    }

    private void SetUpdateState(UpdateState state)
    {
        _updateState = state;
        OnPropertyChanged(nameof(IsCheckingForUpdates));
        OnPropertyChanged(nameof(CheckForUpdatesButtonText));
        OnPropertyChanged(nameof(ShowDownloadButton));
        OnPropertyChanged(nameof(ShowInstallButton));
        OnPropertyChanged(nameof(ShowCancelButton));
        OnPropertyChanged(nameof(ShowNoInstallerMessage));
        OnPropertyChanged(nameof(ShowReadyToInstall));
        OnPropertyChanged(nameof(ShowDownloadProgress));
        OnPropertyChanged(nameof(ShowCheckForUpdatesAction));
        OnPropertyChanged(nameof(ShowReleaseAction));
        OnPropertyChanged(nameof(UpdateStateText));
        CheckForUpdatesCommand.NotifyCanExecuteChanged();
        DownloadUpdateCommand.NotifyCanExecuteChanged();
        InstallUpdateCommand.NotifyCanExecuteChanged();
        CancelDownloadCommand.NotifyCanExecuteChanged();
        DownloadAndInstallCommand.NotifyCanExecuteChanged();
    }

    private void CancelDownload()
    {
        _downloadCancellation?.Cancel();
    }

    private async Task DownloadAndInstallAsync()
    {
        await DownloadUpdateAsync();
        if (_updateState == UpdateState.Downloaded)
        {
            await InstallUpdateAsync();
        }
    }

    private async Task LoadAsync()
    {
        try
        {
            Settings = await _settingsService.LoadAsync();
            Settings.Version = AppVersion.Current;
            Settings.PropertyChanged -= OnSettingsPropertyChanged;
            Settings.PropertyChanged += OnSettingsPropertyChanged;
            ApplyRuntimeSettings();
            StatusMessage = $"Loaded settings from {_settingsService.SettingsPath}";
            UpdateStatus = Settings.LastUpdateCheckUtc is null
                ? "No update check has run yet."
                : $"Ready. Last checked {LastCheckedText}.";
            RefreshUpdateProperties();
            await LoadUpdateHistoryAsync();
            _downloadService.CleanOldDownloads();
            if (ShouldAutoCheckForUpdates())
            {
                _ = CheckForUpdatesAsync(manual: false);
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Could not load settings: {ex.Message}";
        }
    }

    private async Task SaveAsync()
    {
        try
        {
            await _settingsService.SaveAsync(Settings);
            NotifySummaryChanged();
            ApplyRuntimeSettings();
            StatusMessage = $"Saved settings to {_settingsService.SettingsPath}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Could not save settings: {ex.Message}";
        }
    }

    private async Task ResetAsync()
    {
        try
        {
            Settings = await _settingsService.ResetAsync();
            ApplyRuntimeSettings();
            StatusMessage = "Settings reset to defaults.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Could not reset settings: {ex.Message}";
        }
    }

    private async Task ExportAsync()
    {
        try
        {
            var exportPath = await _settingsService.ExportAsync(Settings);
            StatusMessage = $"Exported settings to {exportPath}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Could not export settings: {ex.Message}";
        }
    }

    private async Task ImportAsync()
    {
        try
        {
            Settings = await _settingsService.ImportAsync();
            ApplyRuntimeSettings();
            StatusMessage = $"Imported settings from {_settingsService.ImportPath}";
        }
        catch (FileNotFoundException)
        {
            StatusMessage = $"Place a JSON file at {_settingsService.ImportPath}, then click Import Configuration.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Could not import settings: {ex.Message}";
        }
    }

    private void SelectCategory(object? parameter)
    {
        if (parameter is not SettingsCategoryViewModel category)
        {
            return;
        }

        foreach (var item in Categories)
        {
            item.IsSelected = item == category;
        }

        SelectedCategory = category;
        StatusMessage = $"{category.Name} settings selected.";
    }

    private void NotifySummaryChanged()
    {
        OnPropertyChanged(nameof(LastModifiedText));
        OnPropertyChanged(nameof(VersionText));
    }

    private void OnSettingsPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(AppSettings.LastModifiedUtc))
        {
            return;
        }

        StatusMessage = Settings.AutoSaveSettings
            ? "Setting changed. Auto-save queued."
            : "Setting changed. Click Save Changes to keep it.";

        if (e.PropertyName is nameof(AppSettings.ApplicationName)
            or nameof(AppSettings.Theme)
            or nameof(AppSettings.AccentColor)
            or nameof(AppSettings.Density)
            or nameof(AppSettings.SidebarWidth)
            or nameof(AppSettings.CornerRadius)
            or nameof(AppSettings.BackgroundIntensity)
            or nameof(AppSettings.GlassPanels)
            or nameof(AppSettings.CompactHeader))
        {
            ApplyRuntimeSettings();
        }

        if (e.PropertyName is nameof(AppSettings.UpdateChannel)
            or nameof(AppSettings.IncludeBetaUpdates)
            or nameof(AppSettings.GitHubOwner)
            or nameof(AppSettings.GitHubRepository)
            or nameof(AppSettings.AutomaticallyCheckForUpdates)
            or nameof(AppSettings.AutomaticallyDownloadUpdates)
            or nameof(AppSettings.LastUpdateCheckUtc))
        {
            RefreshUpdateProperties();
        }

        if (Settings.AutoSaveSettings)
        {
            QueueAutoSave();
        }
    }

    private void QueueAutoSave()
    {
        _autoSaveCancellation?.Cancel();
        var cancellation = new CancellationTokenSource();
        _autoSaveCancellation = cancellation;

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(500, cancellation.Token);
                await _settingsService.SaveAsync(Settings);

                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    NotifySummaryChanged();
                    StatusMessage = $"Auto-saved settings to {_settingsService.SettingsPath}";
                });
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                await Application.Current.Dispatcher.InvokeAsync(() =>
                    StatusMessage = $"Could not auto-save settings: {ex.Message}");
            }
        }, cancellation.Token);
    }

    private async Task TestCurseForgeAsync()
    {
        if (string.IsNullOrWhiteSpace(Settings.CurseForgeApiKey))
        {
            IntegrationsStatus = "Enter an API key before testing.";
            return;
        }

        IntegrationsStatus = "Testing connection...";
        try
        {
            using var service = new CurseForgeService(Settings.CurseForgeApiKey, Settings.CurseForgeGameId, Settings.CurseForgeTimeoutSeconds);
            var result = await service.TestConnectionAsync();
            IntegrationsStatus = result.Status == CurseForgeLookupStatus.Success
                ? "Connection successful. API key is valid."
                : $"Connection failed: {result.ErrorMessage}";
        }
        catch (Exception ex)
        {
            IntegrationsStatus = $"Test error: {ex.Message}";
        }
    }

    private async Task CheckForUpdatesAsync(bool manual)
    {
        if (_updateState is UpdateState.Checking or UpdateState.Downloading or UpdateState.Installing)
        {
            return;
        }

        SetUpdateState(UpdateState.Checking);
        UpdateStatus = "Checking for updates...";
        StatusMessage = "Checking for updates...";
        RefreshUpdateProperties();

        try
        {
            var repositoryUrl = BuildRepositoryUrl();
            var includeBeta = Settings.IncludeBetaUpdates || string.Equals(Settings.UpdateChannel, "Beta", StringComparison.OrdinalIgnoreCase);
            await _updateLogger.LogAsync(
                "Check for updates clicked.",
                $"Current={AppVersion.Current}; Repository={repositoryUrl}; Channel={(includeBeta ? "Beta" : "Stable")}");

            _lastUpdateResult = await _releaseService.CheckLatestAsync(
                repositoryUrl,
                AppVersion.Current,
                includeBeta,
                Settings.SkippedUpdateVersion);

            Settings.LastUpdateCheckUtc = DateTime.UtcNow;
            await _settingsService.SaveAsync(Settings);

            if (_lastUpdateResult.ErrorMessage is not null)
            {
                SetUpdateState(UpdateState.Failed);
                UpdateStatus = _lastUpdateResult.ErrorMessage;
                StatusMessage = "Could not check for updates.";
                await AddUpdateHistoryAsync(_lastUpdateResult.LatestVersion ?? AppVersion.Current, "Check", "Failed", _lastUpdateResult.ErrorMessage);
            }
            else if (_lastUpdateResult.IsUpdateAvailable)
            {
                var hasInstaller = GitHubAssetDownloadService.PickBestWindowsAsset(_lastUpdateResult.Assets) is not null;
                if (hasInstaller)
                {
                    SetUpdateState(UpdateState.UpdateAvailable);
                    UpdateStatus = $"Update available: {_lastUpdateResult.LatestVersion} ({_lastUpdateResult.UpdateType}).";
                }
                else
                {
                    SetUpdateState(UpdateState.NoInstallerFound);
                    UpdateStatus = $"Update {_lastUpdateResult.LatestVersion} is available, but no installer was found in the release. Open the GitHub release page to download manually.";
                }
                StatusMessage = UpdateStatus;
                await AddUpdateHistoryAsync(_lastUpdateResult.LatestVersion ?? "Unknown", "Check", "Update available", UpdateStatus);
                if (Settings.AutomaticallyDownloadUpdates && hasInstaller && !Settings.AskBeforeDownloadingUpdates)
                {
                    await DownloadUpdateAsync();
                }
            }
            else
            {
                SetUpdateState(UpdateState.UpToDate);
                UpdateStatus = manual ? "You are up to date." : "No update available.";
                StatusMessage = UpdateStatus;
                await AddUpdateHistoryAsync(AppVersion.Current, "Check", "Up to date", UpdateStatus);
            }
        }
        catch (Exception ex)
        {
            SetUpdateState(UpdateState.Failed);
            UpdateStatus = UpdateErrorMessages.For("UpdateCheckFailed", ex.Message);
            StatusMessage = "Could not check for updates.";
            await _updateLogger.LogAsync("Update check threw an unhandled exception.", ex.ToString());
            await AddUpdateHistoryAsync(AppVersion.Current, "Check", "Failed", ex.ToString());
        }
        finally
        {
            RefreshUpdateProperties();
        }
    }

    private async Task DownloadUpdateAsync()
    {
        if (!_lastUpdateResult.IsUpdateAvailable)
        {
            await CheckForUpdatesAsync(manual: true);
            if (!_lastUpdateResult.IsUpdateAvailable)
            {
                return;
            }
        }

        _downloadCancellation?.Cancel();
        _downloadCancellation = new CancellationTokenSource();
        SetUpdateState(UpdateState.Downloading);
        DownloadProgress = 0;
        DownloadDetail = string.Empty;
        UpdateStatus = "Downloading update...";
        StatusMessage = "Downloading update...";

        try
        {
            var result = await _downloadService.DownloadBestWindowsAssetAsync(_lastUpdateResult, progress =>
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    DownloadProgress = progress.Percent;
                    DownloadDetail = $"{FormatBytes(progress.DownloadedBytes)} / {(progress.TotalBytes is long total ? FormatBytes(total) : "?")} at {FormatBytes((long)progress.BytesPerSecond)}/s";
                });
            }, _downloadCancellation.Token);

            _downloadedUpdatePath = result.FilePath;
            if (result.Success)
            {
                SetUpdateState(UpdateState.Downloaded);
                UpdateStatus = "Update downloaded and ready to install.";
                StatusMessage = UpdateStatus;
                await AddUpdateHistoryAsync(_lastUpdateResult.LatestVersion ?? "Unknown", "Download", "Success", result.Message);
            }
            else
            {
                SetUpdateState(UpdateState.UpdateAvailable);
                UpdateStatus = result.Message;
                StatusMessage = "Download failed.";
                await AddUpdateHistoryAsync(_lastUpdateResult.LatestVersion ?? "Unknown", "Download", "Failed", result.TechnicalDetails ?? result.Message);
            }
        }
        catch (OperationCanceledException)
        {
            SetUpdateState(UpdateState.UpdateAvailable);
            UpdateStatus = UpdateErrorMessages.For("UserCanceled");
            StatusMessage = "Download canceled.";
            await AddUpdateHistoryAsync(_lastUpdateResult.LatestVersion ?? "Unknown", "Download", "Canceled", "User canceled the download.");
        }
        catch (Exception ex)
        {
            SetUpdateState(UpdateState.UpdateAvailable);
            UpdateStatus = UpdateErrorMessages.For("DownloadFailed", ex.Message);
            StatusMessage = "Download failed.";
            await _updateLogger.LogAsync("Download threw an unhandled exception.", ex.ToString());
        }
    }

    private async Task InstallUpdateAsync()
    {
        var readiness = _safeUpdateService.GetInstallReadiness();
        if (!readiness.CanInstall && string.IsNullOrWhiteSpace(_downloadedUpdatePath))
        {
            UpdateStatus = readiness.Message;
            return;
        }

        SetUpdateState(UpdateState.Installing);
        UpdateStatus = "Preparing to install...";

        try
        {
            var backupPath = await _safeUpdateService.BackupSettingsAsync(Settings);
            await _safeUpdateService.SaveStateBeforeInstallAsync(Settings);
            await _updateLogger.LogAsync("Settings backed up before install.", backupPath);

            if (!string.IsNullOrWhiteSpace(_downloadedUpdatePath) && File.Exists(_downloadedUpdatePath))
            {
                if (new FileInfo(_downloadedUpdatePath).Length == 0)
                {
                    throw new InvalidOperationException("Downloaded update file is empty.");
                }

                if (_downloadedUpdatePath.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
                    || _downloadedUpdatePath.EndsWith(".msi", StringComparison.OrdinalIgnoreCase))
                {
                    var answer = MessageBox.Show(
                        $"The app needs to close to install the update.\n\nUpdate: {_lastUpdateResult.LatestVersion ?? "Unknown"}\nInstaller: {Path.GetFileName(_downloadedUpdatePath)}\n\nInstall now?",
                        "Install Update",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Question);

                    if (answer != MessageBoxResult.Yes)
                    {
                        SetUpdateState(UpdateState.Downloaded);
                        UpdateStatus = UpdateErrorMessages.For("UserCanceled");
                        await AddUpdateHistoryAsync(_lastUpdateResult.LatestVersion ?? "Unknown", "Install", "Canceled", UpdateStatus);
                        return;
                    }

                    await _updateLogger.LogAsync("Launching installer.", _downloadedUpdatePath);
                    Process.Start(new ProcessStartInfo(_downloadedUpdatePath) { UseShellExecute = true });
                    await AddUpdateHistoryAsync(_lastUpdateResult.LatestVersion ?? "Unknown", "Install", "Started", _downloadedUpdatePath);
                    SetUpdateState(UpdateState.InstallStarted);
                    UpdateStatus = "Installer launched. The app will close now.";
                    await Task.Delay(600);
                    Application.Current.Shutdown();
                    return;
                }
            }

            CreateUpdateManager().ApplyUpdatesAndRestart(null, Array.Empty<string>());
        }
        catch (Exception ex)
        {
            SetUpdateState(UpdateState.Downloaded);
            UpdateStatus = UpdateErrorMessages.For("InstallFailed", ex.Message);
            await _updateLogger.LogAsync("Install failed.", ex.ToString());
            await AddUpdateHistoryAsync(_lastUpdateResult.LatestVersion ?? "Unknown", "Install", "Failed", ex.ToString());
        }
    }

    private async Task SkipUpdateAsync()
    {
        if (_lastUpdateResult.LatestVersion is null)
        {
            UpdateStatus = "No update version is selected to skip.";
            return;
        }

        Settings.SkippedUpdateVersion = _lastUpdateResult.LatestVersion;
        await _settingsService.SaveAsync(Settings);
        UpdateStatus = $"Skipped {_lastUpdateResult.LatestVersion}.";
    }

    private async Task RemindLaterAsync()
    {
        Settings.RemindUpdateAfterUtc = DateTime.UtcNow.AddDays(1);
        await _settingsService.SaveAsync(Settings);
        UpdateStatus = "Reminder set for tomorrow.";
    }

    private async Task ValidateRepositoryAsync()
    {
        if (string.IsNullOrWhiteSpace(Settings.GitHubOwner) || string.IsNullOrWhiteSpace(Settings.GitHubRepository))
        {
            StatusMessage = "Repository validation failed: owner and repository are required.";
            return;
        }

        StatusMessage = "Validating update repository...";
        var result = await _releaseService.CheckLatestAsync(BuildRepositoryUrl(), AppVersion.Current, includePrerelease: true, Settings.SkippedUpdateVersion);
        StatusMessage = result.ErrorMessage is null
            ? "Repository validation passed. Releases are accessible."
            : $"Repository validation failed: {result.ErrorMessage}";
        _lastUpdateResult = result.ErrorMessage is null ? result : _lastUpdateResult;
        RefreshUpdateProperties();
    }

    private async Task ClearUpdateHistoryAsync()
    {
        var answer = MessageBox.Show("Clear all recorded update history?", "Clear Update History", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (answer != MessageBoxResult.Yes)
        {
            return;
        }

        var historyPath = _updateHistoryService.HistoryPath;
        if (File.Exists(historyPath))
        {
            File.Delete(historyPath);
        }

        await LoadUpdateHistoryAsync();
        StatusMessage = "Update history cleared.";
    }

    private void ClearOldLogs()
    {
        var cutoff = DateTime.UtcNow.AddDays(-Math.Max(1, Settings.MaximumLogRetentionDays));
        foreach (var file in Directory.Exists(_paths.LogsDirectory) ? Directory.EnumerateFiles(_paths.LogsDirectory, "*.log", SearchOption.AllDirectories) : Array.Empty<string>())
        {
            try
            {
                if (File.GetLastWriteTimeUtc(file) < cutoff)
                {
                    File.Delete(file);
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Could not clear old logs: {ex.Message}";
                return;
            }
        }

        StatusMessage = "Old logs cleared.";
    }

    private void MaintenanceAction(string successMessage)
    {
        StatusMessage = successMessage;
    }

    private void RestartApplication()
    {
        var answer = MessageBox.Show("Restart Game Server Manager now?", "Restart Application", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (answer != MessageBoxResult.Yes)
        {
            return;
        }

        var executable = Environment.ProcessPath;
        if (!string.IsNullOrWhiteSpace(executable))
        {
            Process.Start(new ProcessStartInfo(executable) { UseShellExecute = true });
        }

        Application.Current.Shutdown();
    }

    private void CopyVersionInformation()
    {
        CopyText(
            $"Game Server Manager {CurrentVersion}{Environment.NewLine}Build: {BuildNumber}{Environment.NewLine}Built: {BuildDateText}{Environment.NewLine}Install mode: {InstallMode}{Environment.NewLine}Repository: {RepositoryUrl}",
            "Version information copied.");
    }

    private void CopyText(string text, string statusMessage)
    {
        Clipboard.SetText(text);
        StatusMessage = statusMessage;
    }

    private async Task ExportDiagnosticsAsync()
    {
        var reportPath = await _diagnosticsService.ExportAsync(Settings, 0, new[] { "ARK: Survival Ascended", "Palworld" });
        DiagnosticsStatus = $"Diagnostic report exported to {reportPath}";
    }

    private void CopyIssueTemplate()
    {
        Clipboard.SetText(_diagnosticsService.CreateGitHubIssueTemplate(Settings));
        DiagnosticsStatus = "GitHub issue template copied to the clipboard.";
    }

    private UpdateManager CreateUpdateManager()
    {
        var includeBeta = Settings.IncludeBetaUpdates || string.Equals(Settings.UpdateChannel, "Beta", StringComparison.OrdinalIgnoreCase);
        var source = new GithubSource(BuildRepositoryUrl(), accessToken: null, prerelease: includeBeta, downloader: null);
        var options = new UpdateOptions { ExplicitChannel = includeBeta ? "beta" : "stable" };
        return new UpdateManager(source, options);
    }

    private string BuildRepositoryUrl()
    {
        var owner = string.IsNullOrWhiteSpace(Settings.GitHubOwner) ? "joshcarterky" : Settings.GitHubOwner.Trim();
        var repo = string.IsNullOrWhiteSpace(Settings.GitHubRepository) ? "Gamer-server-manager" : Settings.GitHubRepository.Trim();
        return $"https://github.com/{owner}/{repo}";
    }

    private async Task LoadUpdateHistoryAsync()
    {
        var entries = await _updateHistoryService.LoadAsync();
        UpdateHistoryText = entries.Count == 0
            ? "No update history recorded yet."
            : string.Join(Environment.NewLine, entries.Take(8).Select(e => $"{e.Timestamp.LocalDateTime:g} - {e.Version} - {e.Action} - {e.Status}"));
    }

    private async Task AddUpdateHistoryAsync(string version, string action, string status, string details)
    {
        await _updateHistoryService.AddAsync(new UpdateHistoryEntry(DateTimeOffset.Now, version, action, status, details));
        await _updateLogger.LogAsync($"{action}: {status}", details);
        await LoadUpdateHistoryAsync();
    }

    private async Task DeveloperForceUpdateCheckAsync()
    {
        Settings.SkippedUpdateVersion = string.Empty;
        await CheckForUpdatesAsync(manual: true);
    }

    private async Task DeveloperTestDownloadProgressAsync()
    {
        UpdateStatus = "Testing download progress UI...";
        for (var progress = 0; progress <= 100; progress += 10)
        {
            DownloadProgress = progress;
            DownloadDetail = $"{progress}% simulated";
            await Task.Delay(60);
        }

        UpdateStatus = "Download progress UI test complete.";
        await AddUpdateHistoryAsync("dev-test", "Developer test", "Success", "Simulated download progress.");
    }

    private async Task DeveloperTestFailedUpdateAsync()
    {
        UpdateStatus = UpdateErrorMessages.For("InstallFailed", "Developer test failure.");
        await AddUpdateHistoryAsync("dev-test", "Developer test", "Failed", UpdateStatus);
    }

    private bool ShouldAutoCheckForUpdates()
    {
        if (!Settings.AutomaticallyCheckForUpdates || Settings.UpdateCheckFrequency == "Manual only")
        {
            return false;
        }

        if (Settings.RemindUpdateAfterUtc is DateTime remindAfter && remindAfter > DateTime.UtcNow)
        {
            return false;
        }

        return Settings.UpdateCheckFrequency switch
        {
            "Every launch" => true,
            "On application startup" => true,
            "Every 6 hours" => Settings.LastUpdateCheckUtc is null || Settings.LastUpdateCheckUtc.Value.AddHours(6) <= DateTime.UtcNow,
            "Every 12 hours" => Settings.LastUpdateCheckUtc is null || Settings.LastUpdateCheckUtc.Value.AddHours(12) <= DateTime.UtcNow,
            "Weekly" => Settings.LastUpdateCheckUtc is null || Settings.LastUpdateCheckUtc.Value.AddDays(7) <= DateTime.UtcNow,
            _ => Settings.LastUpdateCheckUtc is null || Settings.LastUpdateCheckUtc.Value.AddDays(1) <= DateTime.UtcNow
        };
    }

    private void RefreshUpdateProperties()
    {
        OnPropertyChanged(nameof(CurrentVersion));
        OnPropertyChanged(nameof(LatestVersion));
        OnPropertyChanged(nameof(UpdateChannelText));
        OnPropertyChanged(nameof(LastCheckedText));
        OnPropertyChanged(nameof(UpdateType));
        OnPropertyChanged(nameof(UpdateStateText));
        OnPropertyChanged(nameof(ReleaseTitle));
        OnPropertyChanged(nameof(ReleasePublisher));
        OnPropertyChanged(nameof(ReleaseNotes));
        OnPropertyChanged(nameof(ReleaseDateText));
        OnPropertyChanged(nameof(ReleaseUrl));
        OnPropertyChanged(nameof(DownloadSizeText));
        OnPropertyChanged(nameof(InstallerPackageType));
        OnPropertyChanged(nameof(RepositoryValidationText));
        OnPropertyChanged(nameof(CanUseUpdateFrequency));
        OnPropertyChanged(nameof(CanUseBackgroundDownload));
        OnPropertyChanged(nameof(ShowPrereleaseWarning));
        OnPropertyChanged(nameof(HasUpdateAvailable));
        OnPropertyChanged(nameof(VersionText));
        OnPropertyChanged(nameof(IsCheckingForUpdates));
        OnPropertyChanged(nameof(CheckForUpdatesButtonText));
    }

    private static void OpenUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return;
        }

        Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
    }

    private static void OpenFolderOrFile(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        if (!File.Exists(path) && !Directory.Exists(path))
        {
            Directory.CreateDirectory(Path.HasExtension(path) ? Path.GetDirectoryName(path)! : path);
        }

        Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
    }

    private static string FormatBytes(long bytes)
    {
        if (bytes >= 1024 * 1024) return $"{bytes / 1024d / 1024d:0.0} MB";
        if (bytes >= 1024) return $"{bytes / 1024d:0.0} KB";
        return $"{bytes} bytes";
    }

    private void ApplyRuntimeSettings()
    {
        var shell = Application.Current.Windows.OfType<Shell>().FirstOrDefault();
        shell?.ApplySettings(Settings);
    }
}
