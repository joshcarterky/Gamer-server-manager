using GameServerManager.Core.Models;
using GameServerManager.GameProviders;
using System.IO;

namespace GameServerManager.App.ViewModels;

public class AddServerWizardViewModel : BaseViewModel
{
    private IGameServerProvider? _selectedProvider;
    private int _currentStep = 1;
    private string _profileName = "New Server";
    private string _serverName = "New Server";
    private string _description = string.Empty;
    private string _tags = string.Empty;
    private string _serverPath = string.Empty;
    private string _installPath = string.Empty;
    private string _executablePath = string.Empty;
    private string _saveDirectory = string.Empty;
    private string _backupDirectory = string.Empty;
    private string _ipAddress = "0.0.0.0";
    private int _gamePort;
    private int _queryPort;
    private int _rconPort;
    private string _rconPassword = string.Empty;
    private string _mapName = string.Empty;
    private int _maxPlayers = 10;
    private int _cpuLimit = 100;
    private int _ramLimit = 4096;
    private bool _autoRestart;
    private bool _autoBackup = true;
    private string _password = string.Empty;
    private string _adminPassword = string.Empty;
    private string _launchArgs = string.Empty;
    private ServerProfile? _editingProfile;
    private bool _isEditMode;

    public AddServerWizardViewModel(IReadOnlyCollection<IGameServerProvider> providers)
    {
        Providers = providers
            .OrderBy(provider => GetWizardSortOrder(provider.GameName))
            .ThenBy(provider => provider.GameName)
            .ToList();
        SelectedProvider = Providers.FirstOrDefault();
    }

    public IReadOnlyList<IGameServerProvider> Providers { get; }
    public int TotalSteps => 5;

    public int CurrentStep
    {
        get => _currentStep;
        set
        {
            var nextStep = Math.Clamp(value, 1, TotalSteps);
            if (SetProperty(ref _currentStep, nextStep))
            {
                OnPropertyChanged(nameof(StepTitle));
                OnPropertyChanged(nameof(IsStep1));
                OnPropertyChanged(nameof(IsStep2));
                OnPropertyChanged(nameof(IsStep3));
                OnPropertyChanged(nameof(IsStep4));
                OnPropertyChanged(nameof(IsStep5));
                OnPropertyChanged(nameof(CanGoBack));
                OnPropertyChanged(nameof(CanGoNext));
                OnPropertyChanged(nameof(IsReviewStep));
                OnPropertyChanged(nameof(ReviewText));
            }
        }
    }

    public string StepTitle => CurrentStep switch
    {
        1 => "Step 1: Select Game",
        2 => "Step 2: Server Information",
        3 => "Step 3: Network Settings",
        4 => "Step 4: Resource Limits",
        _ => "Step 5: Review and Create"
    };

    public bool IsStep1 => CurrentStep == 1;
    public bool IsStep2 => CurrentStep == 2;
    public bool IsStep3 => CurrentStep == 3;
    public bool IsStep4 => CurrentStep == 4;
    public bool IsStep5 => CurrentStep == 5;
    public bool CanGoBack => CurrentStep > 1;
    public bool CanGoNext => CurrentStep < TotalSteps;
    public bool IsReviewStep => CurrentStep == TotalSteps;
    public string DialogTitle => IsEditMode ? "Edit Server" : "Add Server";
    public string SaveButtonText => IsEditMode ? "Save Changes" : "Create";

    public bool IsEditMode
    {
        get => _isEditMode;
        private set
        {
            if (SetProperty(ref _isEditMode, value))
            {
                OnPropertyChanged(nameof(DialogTitle));
                OnPropertyChanged(nameof(SaveButtonText));
            }
        }
    }

    public IGameServerProvider? SelectedProvider
    {
        get => _selectedProvider;
        set
        {
            if (SetProperty(ref _selectedProvider, value))
            {
                ApplyProviderDefaults(value);
                NotifyReviewChanged();
            }
        }
    }

    public string ProfileName { get => _profileName; set { if (SetProperty(ref _profileName, value)) NotifyReviewChanged(); } }
    public string ServerName { get => _serverName; set { if (SetProperty(ref _serverName, value)) NotifyReviewChanged(); } }
    public string Description { get => _description; set { if (SetProperty(ref _description, value)) NotifyReviewChanged(); } }
    public string Tags { get => _tags; set { if (SetProperty(ref _tags, value)) NotifyReviewChanged(); } }
    public string ServerPath { get => _serverPath; set { if (SetProperty(ref _serverPath, value)) NotifyReviewChanged(); } }
    public string InstallPath { get => _installPath; set { if (SetProperty(ref _installPath, value)) NotifyReviewChanged(); } }
    public string ExecutablePath { get => _executablePath; set { if (SetProperty(ref _executablePath, value)) NotifyReviewChanged(); } }
    public string SaveDirectory { get => _saveDirectory; set { if (SetProperty(ref _saveDirectory, value)) NotifyReviewChanged(); } }
    public string BackupDirectory { get => _backupDirectory; set { if (SetProperty(ref _backupDirectory, value)) NotifyReviewChanged(); } }
    public string IpAddress { get => _ipAddress; set { if (SetProperty(ref _ipAddress, value)) NotifyReviewChanged(); } }
    public int GamePort { get => _gamePort; set { if (SetProperty(ref _gamePort, value)) NotifyReviewChanged(); } }
    public int QueryPort { get => _queryPort; set { if (SetProperty(ref _queryPort, value)) NotifyReviewChanged(); } }
    public int RconPort { get => _rconPort; set { if (SetProperty(ref _rconPort, value)) NotifyReviewChanged(); } }
    public string RconPassword { get => _rconPassword; set => SetProperty(ref _rconPassword, value); }
    public string MapName { get => _mapName; set => SetProperty(ref _mapName, value); }
    public int MaxPlayers { get => _maxPlayers; set { if (SetProperty(ref _maxPlayers, value)) NotifyReviewChanged(); } }
    public int CpuLimit { get => _cpuLimit; set { if (SetProperty(ref _cpuLimit, value)) NotifyReviewChanged(); } }
    public int RamLimit { get => _ramLimit; set { if (SetProperty(ref _ramLimit, value)) NotifyReviewChanged(); } }
    public bool AutoRestart { get => _autoRestart; set { if (SetProperty(ref _autoRestart, value)) NotifyReviewChanged(); } }
    public bool AutoBackup { get => _autoBackup; set { if (SetProperty(ref _autoBackup, value)) NotifyReviewChanged(); } }
    public string Password { get => _password; set => SetProperty(ref _password, value); }
    public string AdminPassword { get => _adminPassword; set => SetProperty(ref _adminPassword, value); }
    public string LaunchArgs { get => _launchArgs; set => SetProperty(ref _launchArgs, value); }

    public string ReviewText =>
        $"Game: {SelectedProvider?.GameName ?? "None"}{Environment.NewLine}" +
        $"Server Name: {ServerName}{Environment.NewLine}" +
        $"Description: {ValueOrDash(Description)}{Environment.NewLine}" +
        $"Tags: {ValueOrDash(Tags)}{Environment.NewLine}" +
        $"Server Path: {ValueOrDash(ServerPath)}{Environment.NewLine}" +
        $"Install Directory: {InstallPath}{Environment.NewLine}" +
        $"Executable Path: {ExecutablePath}{Environment.NewLine}" +
        $"Save Directory: {ValueOrDash(SaveDirectory)}{Environment.NewLine}" +
        $"Backup Directory: {ValueOrDash(BackupDirectory)}{Environment.NewLine}" +
        $"Endpoint: {IpAddress}:{GamePort}{Environment.NewLine}" +
        $"Query Port: {QueryPort}{Environment.NewLine}" +
        $"RCON Port: {RconPort}{Environment.NewLine}" +
        $"Max Players: {MaxPlayers}{Environment.NewLine}" +
        $"CPU Limit: {CpuLimit}%{Environment.NewLine}" +
        $"RAM Limit: {RamLimit} MB{Environment.NewLine}" +
        $"Auto Restart: {(AutoRestart ? "Enabled" : "Disabled")}{Environment.NewLine}" +
        $"Auto Backup: {(AutoBackup ? "Enabled" : "Disabled")}";

    public void NextStep()
    {
        CurrentStep++;
    }

    public void PreviousStep()
    {
        CurrentStep--;
    }

    public void Reset()
    {
        _editingProfile = null;
        IsEditMode = false;
        CurrentStep = 1;
        ProfileName = "New Server";
        ServerName = "New Server";
        Description = string.Empty;
        Tags = string.Empty;
        RconPassword = string.Empty;
        MaxPlayers = 10;
        CpuLimit = 100;
        RamLimit = 4096;
        AutoRestart = false;
        AutoBackup = true;
        SelectedProvider = Providers.FirstOrDefault();
    }

    public void LoadFromProfile(ServerProfile profile, IGameServerProvider provider)
    {
        _editingProfile = profile;
        IsEditMode = true;
        CurrentStep = 1;
        SelectedProvider = provider;
        ProfileName = profile.ProfileName;
        ServerName = profile.ServerName;
        Description = profile.Settings.TryGetValue("description", out var description) ? description : profile.Notes;
        Tags = profile.Settings.TryGetValue("tags", out var tags) ? tags : string.Empty;
        ServerPath = profile.Settings.TryGetValue("serverPath", out var serverPath) ? serverPath : profile.InstallPath;
        InstallPath = profile.InstallPath;
        ExecutablePath = profile.ExecutablePath;
        SaveDirectory = profile.Settings.TryGetValue("saveDirectory", out var saveDirectory)
            ? saveDirectory
            : Path.Combine(profile.InstallPath, provider.SavesFolder.Replace('/', Path.DirectorySeparatorChar));
        BackupDirectory = profile.Settings.TryGetValue("backupDirectory", out var backupDirectory)
            ? backupDirectory
            : Path.Combine("Data", "Backups", provider.GameId);
        IpAddress = profile.Settings.TryGetValue("ipAddress", out var ipAddress) ? ipAddress : "0.0.0.0";
        GamePort = GetProfilePort(profile, "Game");
        QueryPort = GetProfilePort(profile, "Query");
        RconPort = GetProfilePort(profile, "RCON");
        RconPassword = profile.Settings.TryGetValue("rconPassword", out var rconPassword) ? rconPassword : profile.AdminPassword;
        MapName = profile.MapName;
        MaxPlayers = profile.MaxPlayers;
        CpuLimit = ParseIntSetting(profile, "cpuLimitPercent", 100);
        RamLimit = ParseIntSetting(profile, "ramLimitMb", 4096);
        AutoRestart = profile.Settings.TryGetValue("autoRestart", out var autoRestart)
            ? bool.TryParse(autoRestart, out var isEnabled) && isEnabled
            : !string.IsNullOrWhiteSpace(profile.RestartSchedule);
        AutoBackup = profile.AutoBackupEnabled;
        Password = profile.Password;
        AdminPassword = profile.AdminPassword;
        LaunchArgs = profile.LaunchArgs;
        NotifyReviewChanged();
    }

    public ServerProfile CreateProfile()
    {
        if (SelectedProvider == null)
        {
            throw new InvalidOperationException("Select a game before saving.");
        }

        var profile = new ServerProfile
        {
            Id = _editingProfile?.Id ?? Guid.NewGuid().ToString(),
            GameId = SelectedProvider.GameId,
            ProfileName = string.IsNullOrWhiteSpace(ProfileName) ? ServerName : ProfileName,
            ServerName = ServerName,
            InstallPath = InstallPath,
            ExecutablePath = ExecutablePath,
            MapName = MapName,
            MaxPlayers = MaxPlayers,
            Status = ServerStatus.Stopped,
            Password = Password,
            AdminPassword = string.IsNullOrWhiteSpace(AdminPassword) ? RconPassword : AdminPassword,
            AutoBackupEnabled = AutoBackup,
            RestartSchedule = AutoRestart ? "auto" : null,
            LaunchArgs = LaunchArgs,
            Notes = Description,
            CreatedAt = _editingProfile?.CreatedAt ?? DateTime.UtcNow,
            ModifiedAt = DateTime.UtcNow,
            Ports = CreatePorts().ToList(),
            Settings =
            {
                ["ipAddress"] = string.IsNullOrWhiteSpace(IpAddress) ? "0.0.0.0" : IpAddress,
                ["description"] = Description,
                ["tags"] = Tags,
                ["serverPath"] = ServerPath,
                ["saveDirectory"] = SaveDirectory,
                ["backupDirectory"] = BackupDirectory,
                ["cpuLimitPercent"] = CpuLimit.ToString(),
                ["ramLimitMb"] = RamLimit.ToString(),
                ["autoRestart"] = AutoRestart.ToString(),
                ["rconPassword"] = RconPassword
            }
        };

        foreach (var setting in SelectedProvider.SettingsDefinitions)
        {
            if (!string.IsNullOrWhiteSpace(setting.SettingKey) && setting.DefaultValue != null)
            {
                profile.Settings.TryAdd(setting.SettingKey, setting.DefaultValue);
            }
        }

        return profile;
    }

    private void ApplyProviderDefaults(IGameServerProvider? provider)
    {
        if (provider == null)
        {
            return;
        }

        InstallPath = provider.DefaultInstallFolder.Replace('/', Path.DirectorySeparatorChar);
        ServerPath = InstallPath;
        ExecutablePath = Path.Combine(InstallPath, provider.ExecutableRelativePath.Replace('/', Path.DirectorySeparatorChar));
        SaveDirectory = Path.Combine(InstallPath, provider.SavesFolder.Replace('/', Path.DirectorySeparatorChar));
        BackupDirectory = Path.Combine("Data", "Backups", provider.GameId);
        MapName = provider.GameId == "minecraft_java" ? "world" : string.Empty;
        GamePort = GetDefaultPort(provider, "Game");
        QueryPort = GetDefaultPort(provider, "Query");
        RconPort = GetDefaultPort(provider, "RCON");
    }

    private IEnumerable<ServerPort> CreatePorts()
    {
        if (GamePort > 0)
        {
            yield return CreatePort("Game", GamePort, PortProtocol.UDP);
        }

        if (QueryPort > 0)
        {
            yield return CreatePort("Query", QueryPort, PortProtocol.UDP);
        }

        if (RconPort > 0)
        {
            yield return CreatePort("RCON", RconPort, PortProtocol.TCP);
        }
    }

    private void NotifyReviewChanged()
    {
        OnPropertyChanged(nameof(ReviewText));
    }

    private static ServerPort CreatePort(string name, int port, PortProtocol protocol)
    {
        return new ServerPort
        {
            Name = name,
            Port = port,
            DefaultPort = port,
            Protocol = protocol,
            IsRequired = name != "RCON"
        };
    }

    private static int GetDefaultPort(IGameServerProvider provider, string name)
    {
        return provider.DefaultPorts
            .FirstOrDefault(port => port.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
            ?.Port ?? 0;
    }

    private static int GetProfilePort(ServerProfile profile, string name)
    {
        return profile.Ports
            .FirstOrDefault(port => port.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
            ?.Port ?? 0;
    }

    private static int ParseIntSetting(ServerProfile profile, string key, int fallback)
    {
        return profile.Settings.TryGetValue(key, out var value) && int.TryParse(value, out var parsed)
            ? parsed
            : fallback;
    }

    private static string ValueOrDash(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? "-" : value;
    }

    private static int GetWizardSortOrder(string gameName)
    {
        var orderedNames = new[]
        {
            "ARK: Survival Ascended",
            "ARK: Survival Evolved",
            "Minecraft Java",
            "Minecraft Bedrock",
            "7 Days To Die",
            "Palworld",
            "Rust",
            "Valheim",
            "Conan Exiles",
            "Project Zomboid",
            "Satisfactory",
            "Factorio",
            "Generic Server"
        };

        var index = Array.FindIndex(orderedNames, name => name.Equals(gameName, StringComparison.OrdinalIgnoreCase));
        return index < 0 ? int.MaxValue : index;
    }
}
