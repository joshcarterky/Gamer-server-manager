namespace GameServerManager.Core.Models;

public sealed class PalworldServerProfile
{
    public const string GameId = "palworld";
    public const int SteamCmdAppId = 2394010;
    public const string DefaultExecutableRelativePath = "PalServer.exe";
    public const string DefaultPalWorldSettingsRelativePath = "DefaultPalWorldSettings.ini";
    public const string ActiveConfigRelativePath = "Pal/Saved/Config/WindowsServer/PalWorldSettings.ini";
    public const string PalModSettingsRelativePath = "Mods/PalModSettings.ini";
    public const string PalGameWorldSettingsSection = "/Script/Pal.PalGameWorldSettings";

    public PalworldBasicInfo Basic { get; set; } = new();
    public PalworldNetworkSettings Network { get; set; } = new();
    public PalworldPathSettings Paths { get; set; } = new();
    public PalworldLaunchSettings Launch { get; set; } = new();
    public PalworldOptionSettingsMap OptionSettings { get; set; } = new();
    public PalworldModSettings Mods { get; set; } = new();
    public PalworldBackupSettings Backup { get; set; } = new();
    public PalworldAdvancedSettings Advanced { get; set; } = new();
}

public sealed class PalworldBasicInfo
{
    public string ServerName { get; set; } = "Default Palworld Server";
    public string ServerDescription { get; set; } = string.Empty;
    public string ServerPassword { get; set; } = string.Empty;
    public string AdminPassword { get; set; } = string.Empty;
    public string InstallPath { get; set; } = string.Empty;
    public string InstanceName { get; set; } = "Palworld";
    public int ServerPlayerMaxNum { get; set; } = 32;
    public bool AutoStart { get; set; }
    public bool AutoRestartOnCrash { get; set; } = true;
    public string RestartSchedule { get; set; } = string.Empty;
    public string ProcessPriority { get; set; } = "Normal";
}

public sealed class PalworldNetworkSettings
{
    public int GamePort { get; set; } = 8211;
    public int PublicPort { get; set; } = 8211;
    public string PublicIP { get; set; } = string.Empty;
    public bool RCONEnabled { get; set; }
    public int RCONPort { get; set; } = 25575;
    public bool RESTAPIEnabled { get; set; }
    public int RESTAPIPort { get; set; } = 8212;
    public bool PublicLobbyEnabled { get; set; }
    public bool LANOnly { get; set; }
    public bool UseAuth { get; set; } = true;
    public string BanListURL { get; set; } = "https://api.palworldgame.com/api/banlist.txt";
    public string Region { get; set; } = string.Empty;
    public string CrossplayPlatforms { get; set; } = "(\"Steam\",\"Xbox\")";
    public string AllowConnectPlatform { get; set; } = "Steam";
}

public sealed class PalworldPathSettings
{
    public string ServerRootPath { get; set; } = string.Empty;
    public string ExecutablePath { get; set; } = string.Empty;
    public string DefaultPalWorldSettingsPath { get; set; } = string.Empty;
    public string PalWorldSettingsPath { get; set; } = string.Empty;
    public string SaveGamesPath { get; set; } = string.Empty;
    public string LogsPath { get; set; } = string.Empty;
    public string ModsPath { get; set; } = string.Empty;
    public string PalModSettingsPath { get; set; } = string.Empty;
    public string WorkshopModsPath { get; set; } = string.Empty;
    public string ManagedModsPath { get; set; } = string.Empty;
    public string BackupPath { get; set; } = string.Empty;
    public string ConfigFolderPath { get; set; } = string.Empty;
}

public sealed class PalworldLaunchSettings
{
    public bool PerformanceMode { get; set; }
    public int WorkerThreadCount { get; set; }
    public bool NoMods { get; set; }
    public string LogFormat { get; set; } = "Text";
    public string WorkshopDir { get; set; } = string.Empty;
    public string CustomLaunchArguments { get; set; } = string.Empty;
    public Dictionary<string, string> EnvironmentVariables { get; set; } = new();
}

// Stores all OptionSettings key→value pairs.
// KnownSettings: settings we recognize (from the registry).
// UnknownSettings: settings we don't recognize (preserved as raw INI values).
public sealed class PalworldOptionSettingsMap
{
    public Dictionary<string, string> KnownSettings { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, string> UnknownSettings { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed class PalworldModSettings
{
    public bool GlobalModsEnabled { get; set; } = true;
    public List<PalworldMod> ActiveMods { get; set; } = new();
    public string WorkshopRootDir { get; set; } = string.Empty;
    public bool NoModsLaunchFlag { get; set; }
    public bool BackupBeforeModChanges { get; set; } = true;
}

public sealed class PalworldMod
{
    public string PackageName { get; set; } = string.Empty;
    public string WorkshopId { get; set; } = string.Empty;
    public string FolderName { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public string Author { get; set; } = string.Empty;
    public bool Enabled { get; set; } = true;
    public string Source { get; set; } = string.Empty;
    public string InstallPath { get; set; } = string.Empty;
    public string InfoJsonPath { get; set; } = string.Empty;
    public bool IsServerCompatible { get; set; }
    public bool HasDependencies { get; set; }
    public List<string> Dependencies { get; set; } = new();
    public int LoadOrder { get; set; }
    public string Notes { get; set; } = string.Empty;
    public DateTime? DateAdded { get; set; }
    public DateTime? LastValidated { get; set; }
    public List<string> WarningMessages { get; set; } = new();
}

public sealed class PalworldBackupSettings
{
    public bool ManualBackupsEnabled { get; set; } = true;
    public bool ScheduledBackupsEnabled { get; set; } = true;
    public bool BackupBeforeUpdate { get; set; } = true;
    public bool BackupBeforeConfigSave { get; set; } = true;
    public bool BackupBeforeModChange { get; set; } = true;
    public bool CompressBackups { get; set; } = true;
    public int RetentionLimit { get; set; } = 10;
    public string Schedule { get; set; } = "Daily at 03:00";
    public bool UseBuiltInBackup { get; set; } = true;
}

public sealed class PalworldAdvancedSettings
{
    public bool MarkServerNeedsRestart { get; set; }
    public int GracefulShutdownTimeout { get; set; } = 30;
    public DateTime? LastConfigSave { get; set; }
    public DateTime? LastUpdateTime { get; set; }
    public DateTime? LastBackupTime { get; set; }
    public string PalworldVersion { get; set; } = string.Empty;
    public string CustomOptionSettings { get; set; } = string.Empty;
    public bool RawConfigMode { get; set; }
}

// Enum and definition types mirroring ARK ASA pattern
public enum PalworldSettingLocation
{
    OptionSettings,
    LaunchArgument,
    ModSettings,
    BackupSettings
}

public enum PalworldSettingDataType
{
    String,
    Password,
    Boolean,
    Integer,
    Decimal,
    Enum,
    StringArray
}

public sealed class PalworldSettingDefinition
{
    public string Key { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string Category { get; init; } = string.Empty;
    public PalworldSettingLocation FileLocation { get; init; } = PalworldSettingLocation.OptionSettings;
    public PalworldSettingDataType DataType { get; init; }
    public string DefaultValue { get; init; } = string.Empty;
    public decimal? Min { get; init; }
    public decimal? Max { get; init; }
    public IReadOnlyList<string> AllowedValues { get; init; } = Array.Empty<string>();
    public bool RestartRequired { get; init; } = true;
    public bool AdvancedSetting { get; init; }
    public bool DangerousSetting { get; init; }
    public bool PerformanceImpact { get; init; }
    public string WarningText { get; init; } = string.Empty;
    public string WikiReference { get; init; } = string.Empty;
}
