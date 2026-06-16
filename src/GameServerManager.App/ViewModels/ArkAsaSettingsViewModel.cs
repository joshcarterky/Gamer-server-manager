using System.Collections.ObjectModel;
using System.IO;
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
        OfficialMaps = new ObservableCollection<ArkAsaKnownMap>(
            ArkAsaClusterManager.KnownMaps.Where(map => !map.InternalName.Equals("Custom", StringComparison.OrdinalIgnoreCase)));
        Settings = new ObservableCollection<ArkSettingDefinitionViewModel>();
        FilteredSettings = new ObservableCollection<ArkSettingDefinitionViewModel>();
        Cluster = new ArkAsaClusterViewModel();
        Mods = new ArkAsaModManagerViewModel();
        SaveCommand = new RelayCommand(async _ => await SaveAsync());
        RevertCommand = new RelayCommand(async _ => await LoadAsync());
        CopyCommandCommand = new RelayCommand(_ => ClipboardService.SetText(CommandPreview));
        BrowseInstallPathCommand = new RelayCommand(_ => BrowseInstallPath());

        LoadRegistry();
        _ = LoadAsync();
    }

    public ObservableCollection<string> Tabs { get; }
    public ObservableCollection<ArkAsaKnownMap> OfficialMaps { get; }
    public ObservableCollection<ArkSettingDefinitionViewModel> Settings { get; }
    public ObservableCollection<ArkSettingDefinitionViewModel> FilteredSettings { get; }
    public ArkAsaClusterViewModel Cluster { get; }
    public ArkAsaModManagerViewModel Mods { get; }
    public RelayCommand SaveCommand { get; }
    public RelayCommand RevertCommand { get; }
    public RelayCommand CopyCommandCommand { get; }
    public RelayCommand BrowseInstallPathCommand { get; }

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetProperty(ref _searchText, value))
            {
                ApplyFilters();
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
                OnPropertyChanged(nameof(IsSettingsTab));
            }
        }
    }

    public bool IsClusterTab => SelectedTab.Equals("Cluster", StringComparison.OrdinalIgnoreCase);
    public bool IsModsTab => SelectedTab.Equals("Mods", StringComparison.OrdinalIgnoreCase);
    public bool IsSettingsTab => !IsClusterTab && !IsModsTab;

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
            }
        }
    }
    public string GameUserSettingsPath => _profile.Paths.GameUserSettingsPath;
    public string GameIniPath => _profile.Paths.GameIniPath;
    public string SteamCmdArguments => new ArkAsaSteamCmdService().BuildInstallOrUpdateArguments(_profile.Basic.InstallPath);
    public string CommandPreview => _launchBuilder.Build(_profile).CommandLine;
    public string DiffPreview => BuildPendingSummary();

    public string RawGameUserSettings
    {
        get => _rawGameUserSettings;
        set => SetProperty(ref _rawGameUserSettings, value);
    }

    public string RawGameIni
    {
        get => _rawGameIni;
        set => SetProperty(ref _rawGameIni, value);
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
        AppSettings? appSettings = null;
        try { appSettings = await new AppSettingsService().LoadAsync(); } catch { }
        Mods.LoadFrom(_profile, appSettings);
        NotifyAll();
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
            Directory.CreateDirectory(Path.GetDirectoryName(_profile.Paths.GameUserSettingsPath)!);
            if (File.Exists(_profile.Paths.GameUserSettingsPath))
            {
                File.Copy(_profile.Paths.GameUserSettingsPath, $"{_profile.Paths.GameUserSettingsPath}.{DateTime.UtcNow:yyyyMMddHHmmss}.raw.bak", overwrite: false);
            }

            await File.WriteAllTextAsync(_profile.Paths.GameUserSettingsPath, RawGameUserSettings);
        }

        if (RawGameIni != _loadedRawGameIni)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_profile.Paths.GameIniPath)!);
            if (File.Exists(_profile.Paths.GameIniPath))
            {
                File.Copy(_profile.Paths.GameIniPath, $"{_profile.Paths.GameIniPath}.{DateTime.UtcNow:yyyyMMddHHmmss}.raw.bak", overwrite: false);
            }

            await File.WriteAllTextAsync(_profile.Paths.GameIniPath, RawGameIni);
        }
    }

    private void LoadRegistry()
    {
        Settings.Clear();
        foreach (var definition in ArkAsaSettingRegistry.All)
        {
            Settings.Add(new ArkSettingDefinitionViewModel(definition, ReadCurrentValue(definition)));
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

        if (SelectedTab != "Overview" && SelectedTab != "Raw INI Editor")
        {
            filtered = filtered.Where(setting => setting.Category.Equals(SelectedTab, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            filtered = filtered.Where(setting =>
                setting.Key.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                setting.DisplayName.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                setting.Description.Contains(SearchText, StringComparison.OrdinalIgnoreCase));
        }

        FilteredSettings.Clear();
        foreach (var setting in filtered.OrderBy(setting => setting.Category).ThenBy(setting => setting.DisplayName))
        {
            FilteredSettings.Add(setting);
        }
    }

    private string BuildPendingSummary()
    {
        var changed = Settings.Where(setting => setting.Value != setting.DefaultValue).Take(12).Select(setting => $"{setting.Key}: {setting.DefaultValue} -> {setting.Value}");
        return string.Join(Environment.NewLine, changed);
    }

    private void NotifyAll()
    {
        OnPropertyChanged(nameof(ServerName));
        OnPropertyChanged(nameof(MapName));
        OnPropertyChanged(nameof(InstallPath));
        OnPropertyChanged(nameof(GameUserSettingsPath));
        OnPropertyChanged(nameof(GameIniPath));
        OnPropertyChanged(nameof(SteamCmdArguments));
        NotifyCommandChanged();
    }

    private void NotifyCommandChanged()
    {
        OnPropertyChanged(nameof(CommandPreview));
        OnPropertyChanged(nameof(DiffPreview));
    }

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

public sealed class ArkSettingDefinitionViewModel : BaseViewModel
{
    private string _value;

    public ArkSettingDefinitionViewModel(ArkSettingDefinition definition, string value)
    {
        Key = definition.Key;
        DisplayName = definition.DisplayName;
        Description = definition.Description;
        Category = definition.Category;
        FileLocation = definition.FileLocation;
        DataType = definition.DataType;
        DefaultValue = definition.DefaultValue;
        RestartRequired = definition.RestartRequired;
        AdvancedSetting = definition.AdvancedSetting;
        WarningText = definition.WarningText;
        WikiReference = definition.WikiReference;
        _value = value;
    }

    public string Key { get; }
    public string DisplayName { get; }
    public string Description { get; }
    public string Category { get; }
    public ArkSettingFileLocation FileLocation { get; }
    public ArkSettingDataType DataType { get; }
    public string DefaultValue { get; }
    public bool RestartRequired { get; }
    public bool AdvancedSetting { get; }
    public string WarningText { get; }
    public string WikiReference { get; }

    public string Value
    {
        get => _value;
        set => SetProperty(ref _value, value);
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
