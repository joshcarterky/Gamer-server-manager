using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using GameServerManager.Core.Models;
using GameServerManager.GameProviders;
using GameServerManager.Services;
using GameServerManager.Services.ArkSurvivalAscended;
using GameServerManager.Services.Configuration;
using GameServerManager.Services.CurseForge;
using Microsoft.Win32;

namespace GameServerManager.App.ViewModels;

public sealed class ArkAsaSettingsViewModel : BaseViewModel
{
    private readonly ServersJsonService _serversJsonService = new(new AppDataPaths());
    private readonly ArkAsaProfileMapper _mapper = new();
    private readonly ArkAsaLaunchBuilder _launchBuilder = new();
    private readonly ArkAsaConfigurationStateService _configurationStateService = new();
    private ArkSurvivalAscendedServerProfile _profile = new();
    private ArkServerConfigurationState? _configurationState;
    private string _searchText = string.Empty;
    private bool _showAdvanced;
    private string _selectedTab = "Overview";
    private string _message = "Load or create an ARK ASA server profile to edit live settings.";
    private string _rawGameUserSettings = string.Empty;
    private string _rawGameIni = string.Empty;
    private string _loadedRawGameUserSettings = string.Empty;
    private string _loadedRawGameIni = string.Empty;
    private readonly string? _requestedProfileId;
    private string? _loadedServerProfileId;
    private bool _savedClusterEnabled;
    private string _savedClusterId = string.Empty;
    private string _savedClusterDirectoryOverride = string.Empty;
    private int _validationErrorCount;
    private int _validationWarningCount;
    private bool _revealSensitiveRawValues;
    private bool _syncingRawEditor;
    private FileSystemWatcher? _configurationWatcher;
    private DateTime _lastConfigurationWatcherEventUtc = DateTime.MinValue;
    private bool _suppressConfigurationWatcher;

    public ArkAsaSettingsViewModel(ServerProfile? profile = null)
    {
        _requestedProfileId = profile?.Id;
        Tabs = new ObservableCollection<string>
        {
            "Overview", "Install / Update", "Startup", "Maps", "Network / Ports", "Admin / Passwords",
            "Rates", "Player Settings", "Dino Settings", "Breeding / Imprinting", "Harvesting / Resources",
            "PvE / PvP Rules", "Structures", "Engrams / Levels", "Mods", "Cluster", "Backups",
            "RCON / Console", "Logs", "Advanced", "Raw INI Editor"
        };
        NavigationGroups = new ObservableCollection<ArkNavigationGroupViewModel>
        {
            new("OVERVIEW", new[] { "Overview", "Health and Validation" }),
            new("SERVER", new[] { "Install / Update", "Startup", "Maps", "Network / Ports", "Admin / Passwords" }),
            new("GAMEPLAY", new[] { "Rates", "Player Settings", "Dino Settings", "Breeding / Imprinting", "Harvesting / Resources", "PvE / PvP Rules", "Structures", "Engrams / Levels" }),
            new("MANAGEMENT", new[] { "Mods", "Cluster", "Backups", "RCON / Console", "Logs" }),
            new("ADVANCED", new[] { "Advanced", "Raw INI Editor" })
        };
        OfficialMaps = new ObservableCollection<ArkAsaKnownMap>(
            ArkAsaClusterManager.KnownMaps.Where(map => !map.InternalName.Equals("Custom", StringComparison.OrdinalIgnoreCase)));
        Settings = new ObservableCollection<ArkSettingDefinitionViewModel>();
        FilteredSettings = new ObservableCollection<ArkSettingDefinitionViewModel>();
        FilteredSections = new ObservableCollection<ArkSettingsSectionViewModel>();
        Cluster = new ArkAsaClusterViewModel();
        Mods = new ArkAsaModManagerViewModel();
        SaveCommand = new RelayCommand(async _ => await SaveAsync());
        RevertCommand = new RelayCommand(async _ => await LoadAsync());
        ReviewChangesCommand = new RelayCommand(_ => ReviewChanges());
        ResetCategoryCommand = new RelayCommand(_ => ResetSelectedCategory());
        SelectCategoryCommand = new RelayCommand(SelectCategory);
        CopyCommandCommand = new RelayCommand(_ => ClipboardService.SetText(CommandPreview));
        ViewCommandCommand = new RelayCommand(_ => SelectedTab = "Raw INI Editor");
        ValidateCommand = new RelayCommand(_ => ValidateCurrentProfile());
        BrowseInstallPathCommand = new RelayCommand(_ => BrowseInstallPath());
        OpenServerFolderCommand = new RelayCommand(_ => OpenFolder(InstallPath));
        OpenConfigFolderCommand = new RelayCommand(_ => OpenFolder(_profile.Paths.ConfigPath));
        OpenLogsFolderCommand = new RelayCommand(_ => OpenFolder(_profile.Paths.LogsPath));
        CreateBackupCommand = new RelayCommand(async _ => await CreateBackupAsync());
        ExportConfigurationCommand = new RelayCommand(_ => ExportConfiguration());
        ManageModsCommand = new RelayCommand(_ => SelectedTab = "Mods");
        GenerateClusterIdCommand = new RelayCommand(_ => GenerateClusterId());
        BrowseClusterDirectoryCommand = new RelayCommand(_ => BrowseClusterDirectory());
        CreateClusterDirectoryCommand = new RelayCommand(_ => CreateClusterDirectory());
        OpenClusterFolderCommand = new RelayCommand(_ => OpenClusterFolder());
        ResetClusterCommand = new RelayCommand(_ => ResetClusterSettings());

        LoadRegistry();
        SelectCategory("Overview");
        _ = LoadAsync();
    }

    public ObservableCollection<string> Tabs { get; }
    public ObservableCollection<ArkNavigationGroupViewModel> NavigationGroups { get; }
    public ObservableCollection<ArkAsaKnownMap> OfficialMaps { get; }
    public ObservableCollection<ArkSettingDefinitionViewModel> Settings { get; }
    public ObservableCollection<ArkSettingDefinitionViewModel> FilteredSettings { get; }
    public ObservableCollection<ArkSettingsSectionViewModel> FilteredSections { get; }
    public ArkAsaClusterViewModel Cluster { get; }
    public ArkAsaModManagerViewModel Mods { get; }
    public RelayCommand SaveCommand { get; }
    public RelayCommand RevertCommand { get; }
    public RelayCommand ReviewChangesCommand { get; }
    public RelayCommand ResetCategoryCommand { get; }
    public RelayCommand SelectCategoryCommand { get; }
    public RelayCommand CopyCommandCommand { get; }
    public RelayCommand ViewCommandCommand { get; }
    public RelayCommand ValidateCommand { get; }
    public RelayCommand BrowseInstallPathCommand { get; }
    public RelayCommand OpenServerFolderCommand { get; }
    public RelayCommand OpenConfigFolderCommand { get; }
    public RelayCommand OpenLogsFolderCommand { get; }
    public RelayCommand CreateBackupCommand { get; }
    public RelayCommand ExportConfigurationCommand { get; }
    public RelayCommand ManageModsCommand { get; }
    public RelayCommand GenerateClusterIdCommand { get; }
    public RelayCommand BrowseClusterDirectoryCommand { get; }
    public RelayCommand CreateClusterDirectoryCommand { get; }
    public RelayCommand OpenClusterFolderCommand { get; }
    public RelayCommand ResetClusterCommand { get; }

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetProperty(ref _searchText, value))
            {
                ApplyFilters();
                RefreshNavigationState();
                RefreshChangeState();
            }
        }
    }

    public bool ShowAdvanced
    {
        get => _showAdvanced;
        set
        {
            if (SetProperty(ref _showAdvanced, value))
            {
                ApplyFilters();
                OnPropertyChanged(nameof(IsBasicMode));
                OnPropertyChanged(nameof(IsAdvancedMode));
                OnPropertyChanged(nameof(ModeDescription));
            }
        }
    }

    public bool IsBasicMode
    {
        get => !ShowAdvanced;
        set
        {
            if (value)
            {
                ShowAdvanced = false;
            }
        }
    }

    public bool IsAdvancedMode
    {
        get => ShowAdvanced;
        set
        {
            if (value)
            {
                ShowAdvanced = true;
            }
        }
    }

    public string SelectedTab
    {
        get => _selectedTab;
        set
        {
            if (SetProperty(ref _selectedTab, value))
            {
                ApplyFilters();
                OnPropertyChanged(nameof(IsClusterTab));
                OnPropertyChanged(nameof(IsModsTab));
                OnPropertyChanged(nameof(IsOverviewTab));
                OnPropertyChanged(nameof(IsHealthValidationTab));
                OnPropertyChanged(nameof(IsInstallUpdateTab));
                OnPropertyChanged(nameof(IsStartupTab));
                OnPropertyChanged(nameof(IsRawEditorTab));
                OnPropertyChanged(nameof(IsSettingsTab));
                OnPropertyChanged(nameof(CategoryDescription));
                if (!IsRawEditorTab)
                {
                    RevealSensitiveRawValues = false;
                }
                RefreshNavigationState();
            }
        }
    }

    public bool IsOverviewTab => SelectedTab.Equals("Overview", StringComparison.OrdinalIgnoreCase);
    public bool IsHealthValidationTab => SelectedTab.Equals("Health and Validation", StringComparison.OrdinalIgnoreCase);
    public bool IsInstallUpdateTab => SelectedTab.Equals("Install / Update", StringComparison.OrdinalIgnoreCase);
    public bool IsStartupTab => SelectedTab.Equals("Startup", StringComparison.OrdinalIgnoreCase);
    public bool IsClusterTab => SelectedTab.Equals("Cluster", StringComparison.OrdinalIgnoreCase);
    public bool IsModsTab => SelectedTab.Equals("Mods", StringComparison.OrdinalIgnoreCase);
    public bool IsRawEditorTab => SelectedTab.Equals("Raw INI Editor", StringComparison.OrdinalIgnoreCase);
    public bool IsSettingsTab => !IsOverviewTab && !IsHealthValidationTab && !IsInstallUpdateTab && !IsStartupTab && !IsClusterTab && !IsModsTab && !IsRawEditorTab;
    public string ModeDescription => ShowAdvanced
        ? "Full configuration access for experienced ARK administrators."
        : "Recommended settings for most ARK servers.";
    public string CategoryDescription => SelectedTab switch
    {
        "Overview" => "Quick status, configuration health, and the most common ARK server settings.",
        "Health and Validation" => "Validation results, restart requirements, and structured current-vs-pending changes.",
        "Install / Update" => "SteamCMD installation, server build status, update checks, and validation actions.",
        "Startup" => "Startup behavior, launch options, crash recovery, and generated command validation.",
        "Network / Ports" => "Game, query, and RCON port configuration with local conflict guidance.",
        "Admin / Passwords" => "Sensitive access settings. Password values are masked in previews and diagnostics.",
        "Rates" => "Experience, taming, harvesting, difficulty, and other server pace multipliers.",
        "Breeding / Imprinting" => "Mating, hatching, maturation, cuddle timing, and imprint behavior.",
        "Mods" => "Dedicated CurseForge and ASA mod list management.",
        "Cluster" => "CrossARK transfer identity and shared cluster directory override.",
        "Raw INI Editor" => "Advanced direct editing for GameUserSettings.ini, Game.ini, and launch command review.",
        _ => $"Configure {SelectedTab.ToLowerInvariant()} settings for this ARK server."
    };

    public string Message
    {
        get => _message;
        set => SetProperty(ref _message, value);
    }

    public string ServerName
    {
        get => _profile.Basic.ServerName;
        set
        {
            if (_profile.Basic.ServerName != value)
            {
                _profile.Basic.ServerName = value;
                OnPropertyChanged();
                NotifyCommandChanged();
                RefreshChangeState();
            }
        }
    }

    public string MapName
    {
        get => _profile.Basic.MapName;
        set
        {
            if (_profile.Basic.MapName != value)
            {
                _profile.Basic.MapName = value;
                OnPropertyChanged();
                NotifyCommandChanged();
                RefreshChangeState();
            }
        }
    }

    public string InstallPath
    {
        get => _profile.Basic.InstallPath;
        set
        {
            if (_profile.Basic.InstallPath != value)
            {
                _profile.Basic.InstallPath = value;
                _mapper.HydratePaths(_profile);
                OnPropertyChanged();
                OnPropertyChanged(nameof(SteamCmdArguments));
                OnPropertyChanged(nameof(GameUserSettingsPath));
                OnPropertyChanged(nameof(GameIniPath));
                OnPropertyChanged(nameof(InstallDirectory));
                OnPropertyChanged(nameof(SteamCmdStatus));
                OnPropertyChanged(nameof(InstallationStatus));
                OnPropertyChanged(nameof(ServerExecutablePath));
                OnPropertyChanged(nameof(WorkingDirectory));
                NotifyCommandChanged();
                RefreshChangeState();
            }
        }
    }
    public string GameUserSettingsPath => _profile.Paths.GameUserSettingsPath;
    public string GameIniPath => _profile.Paths.GameIniPath;
    public string ConfigFolder => _profile.Paths.ConfigPath;
    public string LogsFolder => _profile.Paths.LogsPath;
    public string ExecutableStatus => File.Exists(_profile.Paths.ExecutablePath) ? "Executable found" : "Executable missing";
    public string ServerStatusText => "Offline";
    public string JoinabilityText => File.Exists(_profile.Paths.ExecutablePath) ? "Ready to start" : "Install required";
    public string CurrentServerBuild => "Not checked";
    public string LatestServerBuild => "Not checked";
    public string OnlinePlayersText => $"0 / {_profile.Basic.MaxPlayers}";
    public string UptimeText => "Not running";
    public string ModCountText => $"{_profile.Mods.EnabledMods.Count(mod => mod.Enabled)} enabled";
    public string ClusterNameText => ClusterEnabled
        ? (string.IsNullOrWhiteSpace(ClusterId) ? "Cluster enabled" : ClusterId)
        : "Not clustered";
    public bool ClusterEnabled
    {
        get => _profile.Cluster.ClusterEnabled;
        set
        {
            if (_profile.Cluster.ClusterEnabled != value)
            {
                _profile.Cluster.ClusterEnabled = value;
                OnPropertyChanged();
                OnClusterSettingChanged();
            }
        }
    }
    public string ClusterId
    {
        get => _profile.Cluster.ClusterID;
        set
        {
            value ??= string.Empty;
            if (_profile.Cluster.ClusterID != value)
            {
                _profile.Cluster.ClusterID = value;
                OnPropertyChanged();
                OnClusterSettingChanged();
            }
        }
    }
    public string ClusterDirectoryOverride
    {
        get => _profile.Cluster.ClusterDirectoryOverride;
        set
        {
            value ??= string.Empty;
            if (_profile.Cluster.ClusterDirectoryOverride != value)
            {
                _profile.Cluster.ClusterDirectoryOverride = value;
                _profile.Paths.ClusterPath = string.IsNullOrWhiteSpace(value)
                    ? Path.Combine(_profile.Basic.InstallPath, "Cluster")
                    : value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(ClusterDirectoryStatusText));
                OnClusterSettingChanged();
            }
        }
    }
    public bool ClusterHasUnsavedChanges =>
        ClusterEnabled != _savedClusterEnabled ||
        !string.Equals(ClusterId, _savedClusterId, StringComparison.Ordinal) ||
        !string.Equals(ClusterDirectoryOverride, _savedClusterDirectoryOverride, StringComparison.Ordinal);
    public string ClusterStatusText
    {
        get
        {
            if (!ClusterEnabled)
            {
                return "Not configured";
            }

            if (string.IsNullOrWhiteSpace(ClusterId) || string.IsNullOrWhiteSpace(ClusterDirectoryOverride))
            {
                return "Invalid";
            }

            return ClusterHasUnsavedChanges ? "Needs restart" : "Ready";
        }
    }
    public string ClusterStatusColor => ClusterStatusText switch
    {
        "Ready" => "#1E7D4C",
        "Needs restart" => "#8A6420",
        "Invalid" => "#8A2F3B",
        _ => "#263A50"
    };
    public string ClusterStatusMessage => ClusterEnabled
        ? "Set the same Cluster ID and Cluster Directory Override on every ARK ASA map in this cluster."
        : "Enable clustering when this map should share CrossARK transfers with other maps.";
    public string ClusterDirectoryStatusText
    {
        get
        {
            if (string.IsNullOrWhiteSpace(ClusterDirectoryOverride))
            {
                return "Choose the shared CrossARK transfer folder.";
            }

            if (!Directory.Exists(ClusterDirectoryOverride))
            {
                return "Folder does not exist yet. Create it before starting clustered maps.";
            }

            return CanWriteToDirectory(ClusterDirectoryOverride)
                ? "Folder exists and is writable."
                : "Folder exists but is not writable.";
        }
    }
    public string ClusterLaunchPreview => ClusterEnabled
        ? $"-clusterid={ClusterId} -ClusterDirOverride=\"{ClusterDirectoryOverride}\""
        : string.Empty;
    public bool ShowClusterLaunchPreview => ClusterEnabled;
    public int GamePort => _profile.Network.GamePort;
    public int QueryPort => _profile.Network.QueryPort;
    public int RconPort => _profile.Network.RCONPort;
    public int TotalSettingsCount => Settings.Count;
    public int VisibleSettingsCount => FilteredSettings.Count;
    public int UnsavedChangesCount => Settings.Count(setting => setting.IsModified)
        + (ClusterHasUnsavedChanges ? 1 : 0)
        + (ServerName != (Settings.FirstOrDefault(setting => setting.Key == "SessionName")?.SavedValue ?? ServerName) ? 0 : 0);
    public int RestartRequiredChangesCount => Settings.Count(setting => setting.IsModified && setting.RestartRequired)
        + (ClusterHasUnsavedChanges ? 1 : 0);
    public bool HasUnsavedChanges => Settings.Any(setting => setting.IsModified) || ClusterHasUnsavedChanges || RawGameUserSettings != _loadedRawGameUserSettings || RawGameIni != _loadedRawGameIni;
    public string UnsavedChangesText => HasUnsavedChanges
        ? $"{Settings.Count(setting => setting.IsModified)} unsaved setting changes - {RestartRequiredChangesCount} require restart"
        : "All changes saved";
    public int ValidationErrorCount
    {
        get => _validationErrorCount;
        private set => SetProperty(ref _validationErrorCount, value);
    }
    public int ValidationWarningCount
    {
        get => _validationWarningCount;
        private set => SetProperty(ref _validationWarningCount, value);
    }
    public string MatchingSettingsText => string.IsNullOrWhiteSpace(SearchText)
        ? $"{VisibleSettingsCount} available settings"
        : $"{VisibleSettingsCount} matching settings";
    public string SteamCmdArguments => new ArkAsaSteamCmdService().BuildInstallOrUpdateArguments(_profile.Basic.InstallPath);
    public string CommandPreview => _launchBuilder.Build(_profile).CommandLine;
    public string MaskedCommandPreview => MaskSecrets(CommandPreview);
    public string DiffPreview => BuildPendingSummary();
    public string InstallDirectory => _profile.Basic.InstallPath;
    public string SteamCmdStatus => Directory.Exists(_profile.Basic.InstallPath) ? "Install directory ready" : "Install directory missing";
    public string InstallationStatus => File.Exists(_profile.Paths.ExecutablePath) ? "Installed" : "Not installed";
    public string LastUpdatedText => "Not checked";
    public string StartupDelayText => "0 seconds";
    public string StartupTimeoutText => "120 seconds";
    public string GracefulShutdownTimeoutText => "30 seconds";
    public string ServerExecutablePath => _profile.Paths.ExecutablePath;
    public string WorkingDirectory => _profile.Basic.InstallPath;
    public bool RevealSensitiveRawValues
    {
        get => _revealSensitiveRawValues;
        set
        {
            if (SetProperty(ref _revealSensitiveRawValues, value))
            {
                OnPropertyChanged(nameof(RawGameUserSettingsEditorText));
                OnPropertyChanged(nameof(RawGameIniEditorText));
            }
        }
    }
    public string RawGameUserSettingsEditorText
    {
        get => RevealSensitiveRawValues ? RawGameUserSettings : MaskSecrets(RawGameUserSettings);
        set
        {
            if (RevealSensitiveRawValues)
            {
                RawGameUserSettings = value;
                TryApplyRawEditorToPendingState(showError: false);
            }
        }
    }
    public string RawGameIniEditorText
    {
        get => RevealSensitiveRawValues ? RawGameIni : MaskSecrets(RawGameIni);
        set
        {
            if (RevealSensitiveRawValues)
            {
                RawGameIni = value;
                TryApplyRawEditorToPendingState(showError: false);
            }
        }
    }

    public string RawGameUserSettings
    {
        get => _rawGameUserSettings;
        set
        {
            if (SetProperty(ref _rawGameUserSettings, value))
            {
                OnPropertyChanged(nameof(RawGameUserSettingsEditorText));
                RefreshChangeState();
            }
        }
    }

    public string RawGameIni
    {
        get => _rawGameIni;
        set
        {
            if (SetProperty(ref _rawGameIni, value))
            {
                OnPropertyChanged(nameof(RawGameIniEditorText));
                RefreshChangeState();
            }
        }
    }

    private async Task LoadAsync()
    {
        var profiles = await _serversJsonService.LoadServersAsync();
        var arkProfiles = profiles
            .Where(server =>
                server.GameId.Equals(ArkSurvivalAscendedServerProfile.GameId, StringComparison.OrdinalIgnoreCase) ||
                server.GameId.Equals(ArkSurvivalAscendedServerProfile.LegacyGameId, StringComparison.OrdinalIgnoreCase))
            .ToList();
        var profile = !string.IsNullOrWhiteSpace(_requestedProfileId)
            ? arkProfiles.FirstOrDefault(server => server.Id.Equals(_requestedProfileId, StringComparison.OrdinalIgnoreCase))
            : arkProfiles.FirstOrDefault();

        if (profile != null)
        {
            _loadedServerProfileId = profile.Id;
            _profile = _mapper.FromServerProfile(profile);
            Message = $"Loaded ARK ASA settings from {profile.ProfileName}.";
        }
        else
        {
            _loadedServerProfileId = null;
            _profile = new ArkSurvivalAscendedServerProfile();
            _profile.Basic.InstallPath = Path.Combine(Environment.CurrentDirectory, "Servers", "ARK_Survival_Ascended");
            _mapper.HydratePaths(_profile);
            Message = "No ARK ASA profile exists yet. Defaults are ready for a new server instance.";
        }

        CaptureSavedClusterSettings();
        _configurationState = await _configurationStateService.LoadAsync(profile?.Id ?? "new-ark-server", _profile);
        _loadedRawGameUserSettings = _configurationState.GameUserSettingsRawText;
        _loadedRawGameIni = _configurationState.GameIniRawText;
        RawGameUserSettings = _loadedRawGameUserSettings;
        RawGameIni = _loadedRawGameIni;
        LoadRegistry();
        ValidateCurrentProfile();
        AppSettings? appSettings = null;
        try { appSettings = await new AppSettingsService().LoadAsync(); } catch { }
        Mods.LoadFrom(_profile, appSettings);
        NotifyAll();
        RefreshChangeState();
        ConfigureConfigurationWatcher();
    }

    private async Task SaveAsync()
    {
        _suppressConfigurationWatcher = true;
        try
        {
            Mods.ApplyTo(_profile);

            if (!TryApplyRawEditorToPendingState(showError: true))
            {
                return;
            }

            foreach (var setting in Settings)
            {
                if (setting.FileLocation == ArkSettingFileLocation.GameUserSettingsIni)
                {
                    _profile.GameUserSettings.ServerSettings[setting.Key] = setting.SerializedValue;
                }
                else if (setting.FileLocation == ArkSettingFileLocation.GameIni && setting.DataType == ArkSettingDataType.RepeatedLine)
                {
                    _profile.GameIni.RepeatedSettings[setting.Key] = setting.Value
                        .Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries)
                        .ToList();
                }
                else if (setting.FileLocation == ArkSettingFileLocation.GameIni)
                {
                    _profile.GameIni.ShooterGameModeSettings[setting.Key] = setting.SerializedValue;
                }
            }

            await SaveRawEditorTextIfChangedAsync();
            await SaveClusterSettingsToServerProfileAsync();
            var result = await new ArkAsaConfigService().SaveAsync(_profile);
            Message = $"Saved configs: {Path.GetFileName(result.GameUserSettingsPath)}, {Path.GetFileName(result.GameIniPath)}";
            await LoadAsync();
        }
        finally
        {
            _ = Task.Delay(1000).ContinueWith(_ => _suppressConfigurationWatcher = false);
        }
    }

    private async Task SaveRawEditorTextIfChangedAsync()
    {
        if (RawGameUserSettings != _loadedRawGameUserSettings)
        {
            await WriteRawTextAtomicallyAsync(_profile.Paths.GameUserSettingsPath, RawGameUserSettings);
        }

        if (RawGameIni != _loadedRawGameIni)
        {
            await WriteRawTextAtomicallyAsync(_profile.Paths.GameIniPath, RawGameIni);
        }
    }

    private async Task SaveClusterSettingsToServerProfileAsync()
    {
        if (string.IsNullOrWhiteSpace(_loadedServerProfileId))
        {
            return;
        }

        var profiles = (await _serversJsonService.LoadServersAsync()).ToList();
        var profile = profiles.FirstOrDefault(server => server.Id.Equals(_loadedServerProfileId, StringComparison.OrdinalIgnoreCase));
        if (profile == null)
        {
            return;
        }

        profile.Settings["Cluster.Enabled"] = ClusterEnabled.ToString();
        profile.Settings["Cluster.Id"] = ClusterId;
        profile.Settings["Cluster.DirectoryOverride"] = ClusterDirectoryOverride;
        profile.Settings["ClusterEnabled"] = ClusterEnabled.ToString();
        profile.Settings["ClusterID"] = ClusterId;
        profile.Settings["ClusterDirOverride"] = ClusterDirectoryOverride;
        profile.Settings.Remove("NoTransferFromFiltering");
        await _serversJsonService.UpdateServerAsync(profile);
        CaptureSavedClusterSettings();
    }

    private void CaptureSavedClusterSettings()
    {
        _savedClusterEnabled = ClusterEnabled;
        _savedClusterId = ClusterId;
        _savedClusterDirectoryOverride = ClusterDirectoryOverride;
    }

    private static async Task WriteRawTextAtomicallyAsync(string path, string content)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        if (File.Exists(path))
        {
            File.Copy(path, $"{path}.{DateTime.UtcNow:yyyyMMddHHmmss}.raw.bak", overwrite: false);
        }

        var tempPath = $"{path}.{Guid.NewGuid():N}.tmp";
        await File.WriteAllTextAsync(tempPath, content);
        if (File.Exists(path))
        {
            File.Replace(tempPath, path, null);
        }
        else
        {
            File.Move(tempPath, path);
        }
    }

    private void LoadRegistry()
    {
        Settings.Clear();
        foreach (var definition in ArkAsaSettingRegistry.All)
        {
            Settings.Add(new ArkSettingDefinitionViewModel(definition, ReadCurrentValue(definition), OnSettingChanged));
        }

        ApplyFilters();
    }

    private void RefreshSettingValuesFromProfile()
    {
        foreach (var setting in Settings)
        {
            setting.SetPendingValue(ReadCurrentValue(setting.Definition));
        }

        RefreshChangeState();
    }

    private string ReadCurrentValue(ArkSettingDefinition definition)
    {
        return definition.FileLocation switch
        {
            ArkSettingFileLocation.GameUserSettingsIni when _profile.GameUserSettings.ServerSettings.TryGetValue(definition.Key, out var value) => value,
            ArkSettingFileLocation.GameIni when _profile.GameIni.ShooterGameModeSettings.TryGetValue(definition.Key, out var value) => value,
            ArkSettingFileLocation.GameIni when definition.DataType == ArkSettingDataType.RepeatedLine && _profile.GameIni.RepeatedSettings.TryGetValue(definition.Key, out var repeated) => string.Join(Environment.NewLine, repeated),
            ArkSettingFileLocation.LaunchArguments => ReadLaunchValue(definition),
            _ => definition.DefaultValue
        };
    }

    private string ReadLaunchValue(ArkSettingDefinition definition)
    {
        return definition.Key.ToLowerInvariant() switch
        {
            "mapname" => _profile.Basic.MapName,
            "sessionname" => _profile.Basic.ServerName,
            "serverpassword" => _profile.Basic.ServerPassword,
            "serveradminpassword" => _profile.Basic.AdminPassword,
            "port" => _profile.Network.GamePort.ToString(System.Globalization.CultureInfo.InvariantCulture),
            "queryport" => _profile.Network.QueryPort.ToString(System.Globalization.CultureInfo.InvariantCulture),
            "rconport" => _profile.Network.RCONPort.ToString(System.Globalization.CultureInfo.InvariantCulture),
            "rconenabled" => _profile.Network.RCONEnabled ? "True" : "False",
            "maxplayers" => _profile.Basic.MaxPlayers.ToString(System.Globalization.CultureInfo.InvariantCulture),
            "altsavedirectoryname" => _profile.Basic.AltSaveDirectoryName,
            "clusterid" => _profile.Cluster.ClusterID,
            "clusterdiroverride" => _profile.Cluster.ClusterDirectoryOverride,
            "notransferfromfiltering" => _profile.Cluster.NoTransferFromFiltering ? "True" : "False",
            "mods" => string.Join(',', _profile.Mods.EnabledMods.Where(mod => mod.Enabled).Select(mod => mod.Id)),
            "nobattleye" => _profile.Basic.EnableBattlEye ? "False" : "True",
            "log" => _profile.Basic.EnableConsoleLog ? "True" : "False",
            _ => definition.DefaultValue
        };
    }

    private void ApplyFilters()
    {
        var filtered = Settings.AsEnumerable();
        if (!ShowAdvanced)
        {
            filtered = filtered.Where(setting => !setting.AdvancedSetting);
        }

        if (SelectedTab != "Overview" && SelectedTab != "Raw INI Editor" && SelectedTab != "Health and Validation")
        {
            filtered = filtered.Where(setting => setting.Category.Equals(SelectedTab, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            filtered = filtered.Where(setting =>
                setting.Key.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                setting.Category.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                setting.DisplayName.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                setting.Description.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                setting.FileLabel.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                setting.IniSectionLabel.Contains(SearchText, StringComparison.OrdinalIgnoreCase));
        }

        FilteredSettings.Clear();
        foreach (var setting in filtered.OrderBy(setting => setting.Category).ThenBy(setting => setting.DisplayName))
        {
            FilteredSettings.Add(setting);
        }

        FilteredSections.Clear();
        foreach (var section in FilteredSettings
                     .GroupBy(setting => setting.SectionTitle)
                     .Select(group => new ArkSettingsSectionViewModel(group.Key, SectionDescription(group.Key), group.ToArray())))
        {
            FilteredSections.Add(section);
        }

        OnPropertyChanged(nameof(VisibleSettingsCount));
        OnPropertyChanged(nameof(MatchingSettingsText));
        RefreshNavigationState();
    }

    private string BuildPendingSummary()
    {
        var lines = Settings.Where(setting => setting.IsModified).Take(20).Select(setting =>
            $"{setting.Category} | {setting.DisplayName} ({setting.Key}): {setting.MaskedSavedValue} -> {setting.MaskedDisplayValue}").ToList();
        if (ClusterHasUnsavedChanges)
        {
            lines.Add($"Cluster | Enable Cluster (Cluster.Enabled): {_savedClusterEnabled} -> {ClusterEnabled}");
            lines.Add($"Cluster | Cluster ID (Cluster.Id): {_savedClusterId} -> {ClusterId}");
            lines.Add($"Cluster | Cluster Directory Override (Cluster.DirectoryOverride): {_savedClusterDirectoryOverride} -> {ClusterDirectoryOverride}");
        }

        return lines.Count == 0 ? "No pending configuration changes." : string.Join(Environment.NewLine, lines);
    }

    private void NotifyAll()
    {
        OnPropertyChanged(nameof(ServerName));
        OnPropertyChanged(nameof(MapName));
        OnPropertyChanged(nameof(InstallPath));
        OnPropertyChanged(nameof(GameUserSettingsPath));
        OnPropertyChanged(nameof(GameIniPath));
        OnPropertyChanged(nameof(ConfigFolder));
        OnPropertyChanged(nameof(LogsFolder));
        OnPropertyChanged(nameof(ExecutableStatus));
        OnPropertyChanged(nameof(JoinabilityText));
        OnPropertyChanged(nameof(OnlinePlayersText));
        OnPropertyChanged(nameof(ModCountText));
        OnPropertyChanged(nameof(ClusterNameText));
        OnPropertyChanged(nameof(ClusterEnabled));
        OnPropertyChanged(nameof(ClusterId));
        OnPropertyChanged(nameof(ClusterDirectoryOverride));
        OnPropertyChanged(nameof(ClusterStatusText));
        OnPropertyChanged(nameof(ClusterStatusColor));
        OnPropertyChanged(nameof(ClusterStatusMessage));
        OnPropertyChanged(nameof(ClusterDirectoryStatusText));
        OnPropertyChanged(nameof(ClusterLaunchPreview));
        OnPropertyChanged(nameof(ShowClusterLaunchPreview));
        OnPropertyChanged(nameof(GamePort));
        OnPropertyChanged(nameof(QueryPort));
        OnPropertyChanged(nameof(RconPort));
        OnPropertyChanged(nameof(SteamCmdArguments));
        OnPropertyChanged(nameof(MaskedCommandPreview));
        NotifyCommandChanged();
    }

    private void NotifyCommandChanged()
    {
        OnPropertyChanged(nameof(CommandPreview));
        OnPropertyChanged(nameof(MaskedCommandPreview));
        OnPropertyChanged(nameof(DiffPreview));
    }

    private void OnClusterSettingChanged()
    {
        OnPropertyChanged(nameof(ClusterNameText));
        OnPropertyChanged(nameof(ClusterHasUnsavedChanges));
        OnPropertyChanged(nameof(ClusterStatusText));
        OnPropertyChanged(nameof(ClusterStatusColor));
        OnPropertyChanged(nameof(ClusterStatusMessage));
        OnPropertyChanged(nameof(ClusterLaunchPreview));
        OnPropertyChanged(nameof(ShowClusterLaunchPreview));
        ValidateCurrentProfile();
        RefreshChangeState();
        NotifyCommandChanged();
    }

    private void OnSettingChanged()
    {
        ApplyVisualSettingsToPendingProfile();
        RegenerateRawPreviewFromPendingProfile();
        ValidateCurrentProfile();
        RefreshChangeState();
        NotifyCommandChanged();
    }

    private void ApplyVisualSettingsToPendingProfile()
    {
        foreach (var setting in Settings)
        {
            if (setting.FileLocation == ArkSettingFileLocation.GameUserSettingsIni)
            {
                _profile.GameUserSettings.ServerSettings[setting.Key] = setting.SerializedValue;
                ApplySpecialVisualValue(setting.Key, setting.SerializedValue);
            }
            else if (setting.FileLocation == ArkSettingFileLocation.GameIni)
            {
                if (setting.DataType == ArkSettingDataType.RepeatedLine)
                {
                    _profile.GameIni.RepeatedSettings[setting.Key] = setting.Value
                        .Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries)
                        .ToList();
                }
                else
                {
                    _profile.GameIni.ShooterGameModeSettings[setting.Key] = setting.SerializedValue;
                }
            }
        }
    }

    private void ApplySpecialVisualValue(string key, string value)
    {
        if (key.Equals("SessionName", StringComparison.OrdinalIgnoreCase)) _profile.Basic.ServerName = value;
        else if (key.Equals("ServerPassword", StringComparison.OrdinalIgnoreCase)) _profile.Basic.ServerPassword = value;
        else if (key.Equals("ServerAdminPassword", StringComparison.OrdinalIgnoreCase)) _profile.Basic.AdminPassword = value;
        else if (key.Equals("SpectatorPassword", StringComparison.OrdinalIgnoreCase)) _profile.Basic.SpectatorPassword = value;
        else if (key.Equals("MaxPlayers", StringComparison.OrdinalIgnoreCase) && int.TryParse(value, out var maxPlayers)) _profile.Basic.MaxPlayers = maxPlayers;
        else if (key.Equals("RCONEnabled", StringComparison.OrdinalIgnoreCase) && bool.TryParse(value, out var rconEnabled)) _profile.Network.RCONEnabled = rconEnabled;
        else if (key.Equals("RCONPort", StringComparison.OrdinalIgnoreCase) && int.TryParse(value, out var rconPort)) _profile.Network.RCONPort = rconPort;
    }

    private void RegenerateRawPreviewFromPendingProfile()
    {
        if (_syncingRawEditor)
        {
            return;
        }

        try
        {
            var gus = IniDocument.Parse(RawGameUserSettings);
            foreach (var setting in _profile.GameUserSettings.ServerSettings)
            {
                gus.SetValue(ArkAsaSettingRegistry.ServerSettingsSection, setting.Key, setting.Value);
            }

            var game = IniDocument.Parse(RawGameIni);
            foreach (var setting in _profile.GameIni.ShooterGameModeSettings)
            {
                game.SetValue(ArkAsaSettingRegistry.GameModeSection, setting.Key, setting.Value);
            }

            foreach (var repeated in _profile.GameIni.RepeatedSettings)
            {
                game.SetRepeatedValues(ArkAsaSettingRegistry.GameModeSection, repeated.Key, repeated.Value);
            }

            _syncingRawEditor = true;
            RawGameUserSettings = gus.Render();
            RawGameIni = game.Render();
        }
        finally
        {
            _syncingRawEditor = false;
        }
    }

    private bool TryApplyRawEditorToPendingState(bool showError)
    {
        if (_syncingRawEditor)
        {
            return true;
        }

        try
        {
            _configurationState = _configurationStateService.LoadFromRawText(_configurationState?.ServerId ?? "new-ark-server", _profile, RawGameUserSettings, RawGameIni);
            RefreshSettingValuesFromProfile();
            return true;
        }
        catch (Exception ex)
        {
            if (showError)
            {
                Message = $"Raw INI syntax could not be synchronized: {ex.Message}";
            }

            return false;
        }
    }

    private void SelectCategory(object? parameter)
    {
        if (parameter is ArkCategoryItemViewModel item)
        {
            SelectedTab = item.Name;
        }
        else if (parameter is string category)
        {
            SelectedTab = category;
        }
    }

    private void RefreshNavigationState()
    {
        foreach (var group in NavigationGroups)
        {
            foreach (var item in group.Items)
            {
                item.IsSelected = item.Name.Equals(SelectedTab, StringComparison.OrdinalIgnoreCase);
                item.ModifiedCount = Settings.Count(setting => setting.Category.Equals(item.Name, StringComparison.OrdinalIgnoreCase) && setting.IsModified);
                item.ErrorCount = Settings.Count(setting => setting.Category.Equals(item.Name, StringComparison.OrdinalIgnoreCase) && setting.HasError);
                if (item.Name.Equals("Cluster", StringComparison.OrdinalIgnoreCase))
                {
                    item.ModifiedCount += ClusterHasUnsavedChanges ? 1 : 0;
                    item.ErrorCount += ClusterEnabled && (string.IsNullOrWhiteSpace(ClusterId) || string.IsNullOrWhiteSpace(ClusterDirectoryOverride)) ? 1 : 0;
                }
            }
        }
    }

    private void ConfigureConfigurationWatcher()
    {
        _configurationWatcher?.Dispose();
        _configurationWatcher = null;

        if (string.IsNullOrWhiteSpace(_profile.Paths.ConfigPath) || !Directory.Exists(_profile.Paths.ConfigPath))
        {
            return;
        }

        _configurationWatcher = new FileSystemWatcher(_profile.Paths.ConfigPath, "*.ini")
        {
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.Size,
            IncludeSubdirectories = false,
            EnableRaisingEvents = true
        };
        _configurationWatcher.Changed += OnConfigurationFileChanged;
        _configurationWatcher.Created += OnConfigurationFileChanged;
        _configurationWatcher.Renamed += OnConfigurationFileChanged;
    }

    private void OnConfigurationFileChanged(object sender, FileSystemEventArgs e)
    {
        if (_suppressConfigurationWatcher ||
            (!e.FullPath.Equals(_profile.Paths.GameUserSettingsPath, StringComparison.OrdinalIgnoreCase) &&
             !e.FullPath.Equals(_profile.Paths.GameIniPath, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        var now = DateTime.UtcNow;
        if ((now - _lastConfigurationWatcherEventUtc).TotalMilliseconds < 750)
        {
            return;
        }

        _lastConfigurationWatcherEventUtc = now;
        _ = HandleExternalConfigurationChangeAsync(e.FullPath);
    }

    private async Task HandleExternalConfigurationChangeAsync(string path)
    {
        await Task.Delay(750);
        await Application.Current.Dispatcher.InvokeAsync(async () =>
        {
            if (HasUnsavedChanges)
            {
                Message = $"{Path.GetFileName(path)} changed on disk while you have unsaved changes. Use Reload from Disk or review pending changes before saving.";
                return;
            }

            await LoadAsync();
            Message = $"{Path.GetFileName(path)} was changed outside Game Server Manager and has been reloaded.";
        });
    }

    private void RefreshChangeState()
    {
        OnPropertyChanged(nameof(HasUnsavedChanges));
        OnPropertyChanged(nameof(UnsavedChangesCount));
        OnPropertyChanged(nameof(RestartRequiredChangesCount));
        OnPropertyChanged(nameof(UnsavedChangesText));
        OnPropertyChanged(nameof(DiffPreview));
        RefreshNavigationState();
    }

    private static string MaskSecrets(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return value;
        }

        var keys = new[]
        {
            "ServerAdminPassword",
            "ServerPassword",
            "RCONPassword",
            "RCONServerPassword",
            "AdminPassword",
            "Password",
            "ApiKey",
            "Token",
            "Secret"
        };

        var masked = value;
        foreach (var key in keys)
        {
            masked = System.Text.RegularExpressions.Regex.Replace(
                masked,
                $@"(?im)({System.Text.RegularExpressions.Regex.Escape(key)}\s*=\s*)(""[^""]*""|[^\s\r\n?&]+)",
                "$1********");
            masked = System.Text.RegularExpressions.Regex.Replace(
                masked,
                $@"(?im)({System.Text.RegularExpressions.Regex.Escape(key)}\s*[:]\s*)(""[^""]*""|[^\s\r\n,}}]+)",
                "$1********");
        }

        return masked;
    }

    private void ValidateCurrentProfile()
    {
        Mods.ApplyTo(_profile);
        foreach (var setting in Settings)
        {
            setting.Validate();
        }

        var validation = new ArkAsaValidator().Validate(_profile);
        var clusterErrors = 0;
        var clusterWarnings = 0;
        if (ClusterEnabled && !string.IsNullOrWhiteSpace(ClusterDirectoryOverride))
        {
            if (!Directory.Exists(ClusterDirectoryOverride))
            {
                clusterWarnings++;
            }
            else if (!CanWriteToDirectory(ClusterDirectoryOverride))
            {
                clusterErrors++;
            }

            if (!string.IsNullOrWhiteSpace(InstallPath) &&
                ClusterDirectoryOverride.StartsWith(InstallPath, StringComparison.OrdinalIgnoreCase))
            {
                clusterWarnings++;
            }
        }

        ValidationErrorCount = validation.Errors.Count + clusterErrors + Settings.Count(setting => setting.HasError);
        ValidationWarningCount = validation.Warnings.Count + clusterWarnings + Settings.Count(setting => !string.IsNullOrWhiteSpace(setting.WarningText));
        Message = ValidationErrorCount == 0
            ? $"Configuration validated. {ValidationWarningCount} warning(s)."
            : $"Configuration has {ValidationErrorCount} error(s).";
        OnPropertyChanged(nameof(ClusterStatusText));
        OnPropertyChanged(nameof(ClusterStatusColor));
        OnPropertyChanged(nameof(ClusterDirectoryStatusText));
    }

    private void ResetSelectedCategory()
    {
        var answer = MessageBox.Show($"Reset modified {SelectedTab} settings to their saved values?", "Reset Category", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (answer != MessageBoxResult.Yes)
        {
            return;
        }

        foreach (var setting in Settings.Where(setting => setting.Category.Equals(SelectedTab, StringComparison.OrdinalIgnoreCase)))
        {
            setting.ResetToSaved();
        }

        if (SelectedTab.Equals("Cluster", StringComparison.OrdinalIgnoreCase))
        {
            ClusterEnabled = _savedClusterEnabled;
            ClusterId = _savedClusterId;
            ClusterDirectoryOverride = _savedClusterDirectoryOverride;
        }

        RefreshChangeState();
        Message = $"{SelectedTab} settings reset to saved values.";
    }

    private void ReviewChanges()
    {
        MessageBox.Show(DiffPreview, "Pending ARK Configuration Changes", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private async Task CreateBackupAsync()
    {
        try
        {
            var backup = await new ArkAsaBackupService().CreateBackupAsync(_profile, "Manual backup from ARK settings page.");
            Message = $"Backup created: {Path.GetFileName(backup.Path)}";
        }
        catch (Exception ex)
        {
            Message = $"Could not create backup: {ex.Message}";
        }
    }

    private void ExportConfiguration()
    {
        try
        {
            var exportDir = Path.Combine(new AppDataPaths().RootDirectory, "Exports");
            Directory.CreateDirectory(exportDir);
            var exportPath = Path.Combine(exportDir, $"ark-config-{DateTime.Now:yyyyMMdd-HHmmss}.txt");
            File.WriteAllText(exportPath, $"# GameUserSettings.ini{Environment.NewLine}{RawGameUserSettings}{Environment.NewLine}# Game.ini{Environment.NewLine}{RawGameIni}");
            Message = $"Configuration exported to {exportPath}";
        }
        catch (Exception ex)
        {
            Message = $"Could not export configuration: {ex.Message}";
        }
    }

    private void GenerateClusterId()
    {
        ClusterId = $"asa-{Guid.NewGuid():N}"[..20];
        ClusterEnabled = true;
        Message = "Generated a new Cluster ID. Use this same value on every clustered map.";
    }

    private void BrowseClusterDirectory()
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Select ARK cluster transfer directory",
            Multiselect = false
        };

        if (!string.IsNullOrWhiteSpace(ClusterDirectoryOverride) && Directory.Exists(ClusterDirectoryOverride))
        {
            dialog.InitialDirectory = ClusterDirectoryOverride;
        }

        if (dialog.ShowDialog() == true)
        {
            ClusterDirectoryOverride = dialog.FolderName;
            ClusterEnabled = true;
        }
    }

    private void CreateClusterDirectory()
    {
        try
        {
            if (string.IsNullOrWhiteSpace(ClusterDirectoryOverride))
            {
                Message = "Enter a Cluster Directory Override path first.";
                return;
            }

            Directory.CreateDirectory(ClusterDirectoryOverride);
            OnPropertyChanged(nameof(ClusterDirectoryStatusText));
            Message = "Cluster directory is ready.";
        }
        catch (Exception ex)
        {
            Message = $"Could not create cluster directory: {ex.Message}";
        }
    }

    private void OpenClusterFolder()
    {
        if (string.IsNullOrWhiteSpace(ClusterDirectoryOverride))
        {
            Message = "Enter a Cluster Directory Override path before opening it.";
            return;
        }

        OpenFolder(ClusterDirectoryOverride);
    }

    private void ResetClusterSettings()
    {
        ClusterEnabled = false;
        ClusterId = string.Empty;
        ClusterDirectoryOverride = string.Empty;
        Message = "Cluster settings reset. Save Changes to remove cluster launch arguments.";
    }

    private static void OpenFolder(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        Directory.CreateDirectory(path);
        Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
    }

    private static bool CanWriteToDirectory(string path)
    {
        try
        {
            Directory.CreateDirectory(path);
            var testPath = Path.Combine(path, $".nsm-write-test-{Guid.NewGuid():N}.tmp");
            File.WriteAllText(testPath, string.Empty);
            File.Delete(testPath);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static string SectionDescription(string section) => section switch
    {
        "Server Identity" => "Name, capacity, passwords, and server-browser identity.",
        "Core Rates" => "High-impact pacing multipliers that define the feel of the server.",
        "Breeding Timing" => "Mating, hatch, maturation, cuddle, and imprint timing.",
        "Network Endpoints" => "Ports, RCON, binding, and connectivity-related settings.",
        "Access Control" => "Passwords, admin access, exclusive join, and security-sensitive options.",
        "Advanced Entries" => "Complex repeated lines, overrides, or rarely changed options.",
        _ => "Related ARK configuration settings written to the appropriate launch command or INI section."
    };

    private void BrowseInstallPath()
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Select install directory",
            Multiselect = false
        };

        var current = _profile.Basic.InstallPath;
        if (!string.IsNullOrWhiteSpace(current) && Directory.Exists(current))
            dialog.InitialDirectory = current;

        if (dialog.ShowDialog() == true)
            InstallPath = dialog.FolderName;
    }
}

public sealed class ArkNavigationGroupViewModel : BaseViewModel
{
    private bool _isExpanded = true;

    public ArkNavigationGroupViewModel(string name, IEnumerable<string> items)
    {
        Name = name;
        Items = new ObservableCollection<ArkCategoryItemViewModel>(items.Select(item => new ArkCategoryItemViewModel(item)));
    }

    public string Name { get; }
    public ObservableCollection<ArkCategoryItemViewModel> Items { get; }

    public bool IsExpanded
    {
        get => _isExpanded;
        set => SetProperty(ref _isExpanded, value);
    }
}

public sealed class ArkCategoryItemViewModel : BaseViewModel
{
    private bool _isSelected;
    private int _modifiedCount;
    private int _errorCount;

    public ArkCategoryItemViewModel(string name)
    {
        Name = name;
    }

    public string Name { get; }

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }

    public int ModifiedCount
    {
        get => _modifiedCount;
        set
        {
            if (SetProperty(ref _modifiedCount, value))
            {
                OnPropertyChanged(nameof(HasModified));
            }
        }
    }

    public int ErrorCount
    {
        get => _errorCount;
        set
        {
            if (SetProperty(ref _errorCount, value))
            {
                OnPropertyChanged(nameof(HasErrors));
            }
        }
    }

    public bool HasModified => ModifiedCount > 0;
    public bool HasErrors => ErrorCount > 0;
}

public sealed record ArkSettingsSectionViewModel(string Title, string Description, IReadOnlyList<ArkSettingDefinitionViewModel> Settings);

public sealed class ArkSettingDefinitionViewModel : BaseViewModel
{
    private string _value;
    private string _errorText = string.Empty;
    private readonly Action _changed;

    public ArkSettingDefinitionViewModel(ArkSettingDefinition definition, string value, Action changed)
    {
        Definition = definition;
        Key = definition.Key;
        DisplayName = definition.DisplayName;
        Description = definition.Description;
        Category = definition.Category;
        FileLocation = definition.FileLocation;
        DataType = definition.DataType;
        DefaultValue = definition.DefaultValue;
        SavedValue = value;
        Min = definition.Min;
        Max = definition.Max;
        IniSection = definition.IniSection;
        RestartRequired = definition.RestartRequired;
        AdvancedSetting = definition.AdvancedSetting;
        WarningText = definition.WarningText;
        WikiReference = definition.WikiReference;
        _value = value;
        _changed = changed;
        ResetToSavedCommand = new RelayCommand(_ => ResetToSaved());
        ResetToDefaultCommand = new RelayCommand(_ => Value = DefaultValue);
        CopyKeyCommand = new RelayCommand(_ => ClipboardService.SetText(Key));
        CopyValueCommand = new RelayCommand(_ => ClipboardService.SetText(Value));
        Validate();
    }

    public ArkSettingDefinition Definition { get; }
    public string Key { get; }
    public string DisplayName { get; }
    public string Description { get; }
    public string Category { get; }
    public ArkSettingFileLocation FileLocation { get; }
    public ArkSettingDataType DataType { get; }
    public string DefaultValue { get; }
    public string SavedValue { get; private set; }
    public decimal? Min { get; }
    public decimal? Max { get; }
    public string IniSection { get; }
    public bool RestartRequired { get; }
    public bool AdvancedSetting { get; }
    public string WarningText { get; }
    public string WikiReference { get; }
    public RelayCommand ResetToSavedCommand { get; }
    public RelayCommand ResetToDefaultCommand { get; }
    public RelayCommand CopyKeyCommand { get; }
    public RelayCommand CopyValueCommand { get; }
    public bool IsBoolean => DataType == ArkSettingDataType.Boolean;
    public bool IsPassword => DataType == ArkSettingDataType.Password;
    public bool IsNumeric => DataType is ArkSettingDataType.Integer or ArkSettingDataType.Decimal;
    public bool IsText => !IsBoolean && !IsPassword && !IsComplex;
    public bool IsComplex => DataType is ArkSettingDataType.RepeatedLine or ArkSettingDataType.StringList;
    public bool IsModified => !string.Equals(Value, SavedValue, StringComparison.Ordinal);
    public bool IsCustomized => !string.Equals(Value, DefaultValue, StringComparison.Ordinal);
    public bool HasWarning => !string.IsNullOrWhiteSpace(WarningText);
    public bool HasError => !string.IsNullOrWhiteSpace(ErrorText);
    public string FileLabel => FileLocation switch
    {
        ArkSettingFileLocation.GameUserSettingsIni => "GameUserSettings.ini",
        ArkSettingFileLocation.GameIni => "Game.ini",
        ArkSettingFileLocation.LaunchArguments => "Launch arguments",
        _ => FileLocation.ToString()
    };
    public string IniSectionLabel => string.IsNullOrWhiteSpace(IniSection) ? "Launch command" : $"[{IniSection}]";
    public string TechnicalDetails => $"Key: {Key}{Environment.NewLine}File: {FileLabel}{Environment.NewLine}Section: {IniSectionLabel}{Environment.NewLine}Default: {DefaultValue}{Environment.NewLine}Restart required: {(RestartRequired ? "Yes" : "No")}{Environment.NewLine}Source reviewed: ARK Official Community Wiki, 2026-06-17";
    public string SectionTitle => Category switch
    {
        "Rates" => Key.Contains("XP", StringComparison.OrdinalIgnoreCase) ? "Experience" :
            Key.Contains("Taming", StringComparison.OrdinalIgnoreCase) ? "Taming" :
            Key.Contains("Harvest", StringComparison.OrdinalIgnoreCase) ? "Harvesting" : "Core Rates",
        "Breeding / Imprinting" => "Breeding Timing",
        "Network / Ports" or "RCON / Console" => "Network Endpoints",
        "Admin / Passwords" or "Server Identity" => "Access Control",
        _ when IsComplex => "Advanced Entries",
        _ => Category
    };
    public string UnitSuffix => DataType == ArkSettingDataType.Decimal && DisplayName.Contains("Multiplier", StringComparison.OrdinalIgnoreCase)
        ? "x"
        : DataType == ArkSettingDataType.Integer && Key.Contains("Port", StringComparison.OrdinalIgnoreCase)
            ? "port"
            : string.Empty;
    public string MaskedDisplayValue => IsPassword && !string.IsNullOrEmpty(Value) ? "********" : Value;
    public string MaskedSavedValue => IsPassword && !string.IsNullOrEmpty(SavedValue) ? "********" : SavedValue;
    public string SerializedValue => SerializeValue(Value);

    public string Value
    {
        get => _value;
        set
        {
            if (SetProperty(ref _value, value))
            {
                Validate();
                OnPropertyChanged(nameof(BooleanValue));
                OnPropertyChanged(nameof(IsModified));
                OnPropertyChanged(nameof(IsCustomized));
                OnPropertyChanged(nameof(MaskedDisplayValue));
                OnPropertyChanged(nameof(SerializedValue));
                _changed();
            }
        }
    }

    public bool BooleanValue
    {
        get => Value.Equals("True", StringComparison.OrdinalIgnoreCase);
        set => Value = value ? "True" : "False";
    }

    public string ErrorText
    {
        get => _errorText;
        private set
        {
            if (SetProperty(ref _errorText, value))
            {
                OnPropertyChanged(nameof(HasError));
            }
        }
    }

    public void ResetToSaved()
    {
        Value = SavedValue;
    }

    public void SetPendingValue(string value)
    {
        if (SetProperty(ref _value, value, nameof(Value)))
        {
            Validate();
            OnPropertyChanged(nameof(BooleanValue));
            OnPropertyChanged(nameof(IsModified));
            OnPropertyChanged(nameof(IsCustomized));
            OnPropertyChanged(nameof(MaskedDisplayValue));
            OnPropertyChanged(nameof(SerializedValue));
        }
    }

    public void Validate()
    {
        ErrorText = string.Empty;
        if (IsBoolean && !Value.Equals("True", StringComparison.OrdinalIgnoreCase) && !Value.Equals("False", StringComparison.OrdinalIgnoreCase))
        {
            ErrorText = "Use on or off for this setting.";
            return;
        }

        if (DataType == ArkSettingDataType.Integer && (!int.TryParse(Value, out var integer) || HasRangeError(integer)))
        {
            ErrorText = RangeMessage("whole number");
            return;
        }

        if (DataType == ArkSettingDataType.Decimal && (!decimal.TryParse(Value, System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out var number) || HasRangeError(number)))
        {
            ErrorText = RangeMessage("number");
        }
    }

    private string SerializeValue(string value)
    {
        if (DataType == ArkSettingDataType.Boolean)
        {
            return value.Equals("True", StringComparison.OrdinalIgnoreCase) || value == "1" ? "True" : "False";
        }

        if (DataType == ArkSettingDataType.Integer && int.TryParse(value, out var integer))
        {
            return integer.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        if (DataType == ArkSettingDataType.Decimal &&
            decimal.TryParse(value, System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out var number))
        {
            return number.ToString("G29", System.Globalization.CultureInfo.InvariantCulture);
        }

        return value;
    }

    private bool HasRangeError(decimal value) =>
        (Min.HasValue && value < Min.Value) || (Max.HasValue && value > Max.Value);

    private string RangeMessage(string type)
    {
        if (Min.HasValue && Max.HasValue) return $"Enter a {type} from {Min.Value} to {Max.Value}.";
        if (Min.HasValue) return $"Enter a {type} of at least {Min.Value}.";
        if (Max.HasValue) return $"Enter a {type} no greater than {Max.Value}.";
        return $"Enter a valid {type}.";
    }
}

internal static class ClipboardService
{
    public static void SetText(string text)
    {
        try
        {
            System.Windows.Clipboard.SetText(text);
        }
        catch
        {
            // Clipboard can be temporarily locked by another process.
        }
    }
}
