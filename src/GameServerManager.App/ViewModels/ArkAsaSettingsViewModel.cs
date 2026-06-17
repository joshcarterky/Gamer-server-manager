using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using GameServerManager.Core.Models;
using GameServerManager.GameProviders;
using GameServerManager.Services;
using GameServerManager.Services.ArkSurvivalAscended;
using GameServerManager.Services.CurseForge;
using Microsoft.Win32;

namespace GameServerManager.App.ViewModels;

public sealed class ArkAsaSettingsViewModel : BaseViewModel
{
    private readonly ServersJsonService _serversJsonService = new(new AppDataPaths());
    private readonly ArkAsaProfileMapper _mapper = new();
    private readonly ArkAsaLaunchBuilder _launchBuilder = new();
    private ArkSurvivalAscendedServerProfile _profile = new();
    private string _searchText = string.Empty;
    private bool _showAdvanced;
    private string _selectedTab = "Overview";
    private string _message = "Load or create an ARK ASA server profile to edit live settings.";
    private string _rawGameUserSettings = string.Empty;
    private string _rawGameIni = string.Empty;
    private string _loadedRawGameUserSettings = string.Empty;
    private string _loadedRawGameIni = string.Empty;
    private readonly string? _requestedProfileId;
    private int _validationErrorCount;
    private int _validationWarningCount;

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
                OnPropertyChanged(nameof(IsRawEditorTab));
                OnPropertyChanged(nameof(IsSettingsTab));
                OnPropertyChanged(nameof(CategoryDescription));
                RefreshNavigationState();
            }
        }
    }

    public bool IsOverviewTab => SelectedTab.Equals("Overview", StringComparison.OrdinalIgnoreCase);
    public bool IsClusterTab => SelectedTab.Equals("Cluster", StringComparison.OrdinalIgnoreCase);
    public bool IsModsTab => SelectedTab.Equals("Mods", StringComparison.OrdinalIgnoreCase);
    public bool IsRawEditorTab => SelectedTab.Equals("Raw INI Editor", StringComparison.OrdinalIgnoreCase);
    public bool IsSettingsTab => !IsOverviewTab && !IsClusterTab && !IsModsTab && !IsRawEditorTab;
    public bool IsBasicMode => !ShowAdvanced;
    public bool IsAdvancedMode => ShowAdvanced;
    public string ModeDescription => ShowAdvanced
        ? "Full configuration access for experienced ARK administrators."
        : "Recommended settings for most ARK servers.";
    public string CategoryDescription => SelectedTab switch
    {
        "Overview" => "Quick status, configuration health, and the most common ARK server settings.",
        "Install / Update" => "Install path, SteamCMD arguments, executable status, and update workflow entry points.",
        "Network / Ports" => "Game, query, and RCON port configuration with local conflict guidance.",
        "Admin / Passwords" => "Sensitive access settings. Password values are masked in previews and diagnostics.",
        "Rates" => "Experience, taming, harvesting, difficulty, and other server pace multipliers.",
        "Breeding / Imprinting" => "Mating, hatching, maturation, cuddle timing, and imprint behavior.",
        "Mods" => "Dedicated CurseForge and ASA mod list management.",
        "Cluster" => "Cluster ID, shared transfer folder, member maps, and transfer rules.",
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
    public string ClusterNameText => string.IsNullOrWhiteSpace(_profile.Cluster.ClusterMapGroup) ? "Not clustered" : _profile.Cluster.ClusterMapGroup;
    public int GamePort => _profile.Network.GamePort;
    public int QueryPort => _profile.Network.QueryPort;
    public int RconPort => _profile.Network.RCONPort;
    public int TotalSettingsCount => Settings.Count;
    public int VisibleSettingsCount => FilteredSettings.Count;
    public int UnsavedChangesCount => Settings.Count(setting => setting.IsModified)
        + (ServerName != (Settings.FirstOrDefault(setting => setting.Key == "SessionName")?.SavedValue ?? ServerName) ? 0 : 0);
    public int RestartRequiredChangesCount => Settings.Count(setting => setting.IsModified && setting.RestartRequired);
    public bool HasUnsavedChanges => Settings.Any(setting => setting.IsModified) || RawGameUserSettings != _loadedRawGameUserSettings || RawGameIni != _loadedRawGameIni;
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
    public string SteamCmdArguments => new ArkAsaSteamCmdService().BuildInstallOrUpdateArguments(_profile.Basic.InstallPath);
    public string CommandPreview => _launchBuilder.Build(_profile).CommandLine;
    public string DiffPreview => BuildPendingSummary();

    public string RawGameUserSettings
    {
        get => _rawGameUserSettings;
        set
        {
            if (SetProperty(ref _rawGameUserSettings, value))
            {
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
            _profile = _mapper.FromServerProfile(profile);
            Message = $"Loaded ARK ASA settings from {profile.ProfileName}.";
        }
        else
        {
            _profile = new ArkSurvivalAscendedServerProfile();
            _profile.Basic.InstallPath = Path.Combine(Environment.CurrentDirectory, "Servers", "ARK_Survival_Ascended");
            _mapper.HydratePaths(_profile);
            Message = "No ARK ASA profile exists yet. Defaults are ready for a new server instance.";
        }

        _loadedRawGameUserSettings = File.Exists(_profile.Paths.GameUserSettingsPath) ? await File.ReadAllTextAsync(_profile.Paths.GameUserSettingsPath) : string.Empty;
        _loadedRawGameIni = File.Exists(_profile.Paths.GameIniPath) ? await File.ReadAllTextAsync(_profile.Paths.GameIniPath) : string.Empty;
        RawGameUserSettings = _loadedRawGameUserSettings;
        RawGameIni = _loadedRawGameIni;
        LoadRegistry();
        ValidateCurrentProfile();
        AppSettings? appSettings = null;
        try { appSettings = await new AppSettingsService().LoadAsync(); } catch { }
        Mods.LoadFrom(_profile, appSettings);
        NotifyAll();
        RefreshChangeState();
    }

    private async Task SaveAsync()
    {
        Mods.ApplyTo(_profile);

        foreach (var setting in Settings)
        {
            if (setting.FileLocation == ArkSettingFileLocation.GameUserSettingsIni)
            {
                _profile.GameUserSettings.ServerSettings[setting.Key] = setting.Value;
            }
            else if (setting.FileLocation == ArkSettingFileLocation.GameIni && setting.DataType != ArkSettingDataType.RepeatedLine)
            {
                _profile.GameIni.ShooterGameModeSettings[setting.Key] = setting.Value;
            }
        }

        await SaveRawEditorTextIfChangedAsync();
        var result = await new ArkAsaConfigService().SaveAsync(_profile);
        Message = $"Saved configs: {Path.GetFileName(result.GameUserSettingsPath)}, {Path.GetFileName(result.GameIniPath)}";
        await LoadAsync();
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

    private string ReadCurrentValue(ArkSettingDefinition definition)
    {
        return definition.FileLocation switch
        {
            ArkSettingFileLocation.GameUserSettingsIni when _profile.GameUserSettings.ServerSettings.TryGetValue(definition.Key, out var value) => value,
            ArkSettingFileLocation.GameIni when _profile.GameIni.ShooterGameModeSettings.TryGetValue(definition.Key, out var value) => value,
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
        RefreshNavigationState();
    }

    private string BuildPendingSummary()
    {
        var changed = Settings.Where(setting => setting.IsModified).Take(20).Select(setting =>
            $"{setting.Category} | {setting.DisplayName} ({setting.Key}): {setting.MaskedSavedValue} -> {setting.MaskedDisplayValue}");
        var lines = changed.ToArray();
        return lines.Length == 0 ? "No pending configuration changes." : string.Join(Environment.NewLine, lines);
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
        OnPropertyChanged(nameof(GamePort));
        OnPropertyChanged(nameof(QueryPort));
        OnPropertyChanged(nameof(RconPort));
        OnPropertyChanged(nameof(SteamCmdArguments));
        NotifyCommandChanged();
    }

    private void NotifyCommandChanged()
    {
        OnPropertyChanged(nameof(CommandPreview));
        OnPropertyChanged(nameof(DiffPreview));
    }

    private void OnSettingChanged()
    {
        ValidateCurrentProfile();
        RefreshChangeState();
        NotifyCommandChanged();
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
            }
        }
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

    private void ValidateCurrentProfile()
    {
        Mods.ApplyTo(_profile);
        foreach (var setting in Settings)
        {
            setting.Validate();
        }

        var validation = new ArkAsaValidator().Validate(_profile);
        ValidationErrorCount = validation.Errors.Count + Settings.Count(setting => setting.HasError);
        ValidationWarningCount = validation.Warnings.Count + Settings.Count(setting => !string.IsNullOrWhiteSpace(setting.WarningText));
        Message = ValidationErrorCount == 0
            ? $"Configuration validated. {ValidationWarningCount} warning(s)."
            : $"Configuration has {ValidationErrorCount} error(s).";
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

    private static void OpenFolder(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        Directory.CreateDirectory(path);
        Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
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

        if (DataType == ArkSettingDataType.Decimal && (!decimal.TryParse(Value, out var number) || HasRangeError(number)))
        {
            ErrorText = RangeMessage("number");
        }
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
