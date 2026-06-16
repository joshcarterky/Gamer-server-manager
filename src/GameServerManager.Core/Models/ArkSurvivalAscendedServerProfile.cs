namespace GameServerManager.Core.Models;

public sealed class ArkSurvivalAscendedServerProfile
{
    public const string GameId = "ark-survival-ascended";
    public const string LegacyGameId = "ark_survival_ascended";
    public const int SteamCmdAppId = 2430930;
    public const string DefaultExecutableRelativePath = "ShooterGame/Binaries/Win64/ArkAscendedServer.exe";

    public ArkBasicServerInfo Basic { get; set; } = new();
    public ArkNetworkSettings Network { get; set; } = new();
    public ArkPathSettings Paths { get; set; } = new();
    public ArkLaunchSettings Launch { get; set; } = new();
    public ArkGameUserSettings GameUserSettings { get; set; } = new();
    public ArkGameIniSettings GameIni { get; set; } = new();
    public ArkRawSettings RawSettings { get; set; } = new();
    public ArkClusterSettings Cluster { get; set; } = new();
    public ArkModSettings Mods { get; set; } = new();
    public ArkBackupSettings Backup { get; set; } = new();
    public ArkMonitoringSettings Monitoring { get; set; } = new();
    public ArkAdvancedSettings Advanced { get; set; } = new();
}

public sealed class ArkBasicServerInfo
{
    public string ServerName { get; set; } = "ARK ASA Server";
    public string Description { get; set; } = string.Empty;
    public string InstallPath { get; set; } = string.Empty;
    public string InstanceName { get; set; } = "TheIsland";
    public string MapName { get; set; } = "TheIsland_WP";
    public string AltSaveDirectoryName { get; set; } = string.Empty;
    public int MaxPlayers { get; set; } = 70;
    public string ServerPassword { get; set; } = string.Empty;
    public string AdminPassword { get; set; } = string.Empty;
    public string SpectatorPassword { get; set; } = string.Empty;
    public bool EnableBattlEye { get; set; } = true;
    public bool EnableConsoleLog { get; set; } = true;
    public bool AutoStart { get; set; }
    public bool AutoRestartOnCrash { get; set; } = true;
    public string RestartSchedule { get; set; } = string.Empty;
}

public sealed class ArkNetworkSettings
{
    public string ServerIP { get; set; } = string.Empty;
    public string MultiHome { get; set; } = string.Empty;
    public int GamePort { get; set; } = 7777;
    public int QueryPort { get; set; } = 27015;
    public int RCONPort { get; set; } = 27020;
    public bool RCONEnabled { get; set; } = true;
    public string RCONPassword { get; set; } = string.Empty;
    public bool PublicServer { get; set; } = true;
    public bool LANOnly { get; set; }
    public bool EnableCrossplay { get; set; } = true;
    public bool CreateFirewallRules { get; set; }
}

public sealed class ArkPathSettings
{
    public string ServerRootPath { get; set; } = string.Empty;
    public string ExecutablePath { get; set; } = string.Empty;
    public string SavedPath { get; set; } = string.Empty;
    public string ConfigPath { get; set; } = string.Empty;
    public string GameUserSettingsPath { get; set; } = string.Empty;
    public string GameIniPath { get; set; } = string.Empty;
    public string LogsPath { get; set; } = string.Empty;
    public string SavesPath { get; set; } = string.Empty;
    public string ModsPath { get; set; } = string.Empty;
    public string BackupPath { get; set; } = string.Empty;
    public string ClusterPath { get; set; } = string.Empty;
}

public sealed class ArkLaunchSettings
{
    public string CustomLaunchArguments { get; set; } = string.Empty;
    public Dictionary<string, string> EnvironmentVariables { get; set; } = new();
    public string ProcessPriority { get; set; } = "Normal";
    public string CPUAffinity { get; set; } = string.Empty;
}

public sealed class ArkGameUserSettings
{
    public Dictionary<string, string> ServerSettings { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public List<string> CustomGameUserSettingsLines { get; set; } = new();
}

public sealed class ArkGameIniSettings
{
    public Dictionary<string, string> ShooterGameModeSettings { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, List<string>> RepeatedSettings { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public List<string> CustomGameIniLines { get; set; } = new();
}

public sealed class ArkRawSettings
{
    public string GameUserSettingsRawText { get; set; } = string.Empty;
    public string GameIniRawText { get; set; } = string.Empty;
}

public sealed class ArkClusterSettings
{
    public bool ClusterEnabled { get; set; }
    public string ClusterID { get; set; } = string.Empty;
    public string ClusterDirectoryOverride { get; set; } = string.Empty;
    public bool NoTransferFromFiltering { get; set; }
    public bool PreventDownloadSurvivors { get; set; }
    public bool PreventDownloadItems { get; set; }
    public bool PreventDownloadDinos { get; set; }
    public bool PreventUploadSurvivors { get; set; }
    public bool PreventUploadItems { get; set; }
    public bool PreventUploadDinos { get; set; }
    public bool AllowTributeDownloads { get; set; } = true;
    public string SharedClusterFolder { get; set; } = string.Empty;
    public string ClusterMapGroup { get; set; } = string.Empty;
}

public sealed class ArkModSettings
{
    public List<ArkModEntry> EnabledMods { get; set; } = new();
    public List<string> ModIDs { get; set; } = new();
    public List<string> ModLoadOrder { get; set; } = new();
    public string ActiveMapModId { get; set; } = string.Empty;
    public bool AutoUpdateMods { get; set; } = true;
    public bool ValidateModsBeforeStart { get; set; } = true;
    public bool WarnOnDuplicateMods { get; set; } = true;
    public string CustomModArguments { get; set; } = string.Empty;
}

public sealed class ArkModEntry
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool Enabled { get; set; } = true;
    public bool ClusterWide { get; set; }
    public bool IsMapMod { get; set; }
    public int LoadOrder { get; set; }
    public ModSource Source { get; set; } = ModSource.Unknown;
    public DateTime? DateAdded { get; set; }
    public string Notes { get; set; } = string.Empty;
    public bool RequiredRestart { get; set; } = true;
    public bool ServerSpecific { get; set; }
    public bool MapSpecific { get; set; }
    public string CurseForgeSlug { get; set; } = string.Empty;
    public string CurseForgeUrl { get; set; } = string.Empty;
    public string Author { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
}

public enum ModInstalledStatus { Unknown, Installed, Missing, UpdateAvailable, Disabled, Error }
public enum ModValidationStatus { Valid, Duplicate, MissingId, InvalidId, LoadOrderConflict, MapModConflict, Unknown }
public enum ModSource { CurseForge, ASAModId, Manual, Unknown }

public sealed class ArkBackupSettings
{
    public bool ManualBackupsEnabled { get; set; } = true;
    public bool ScheduledBackupsEnabled { get; set; } = true;
    public bool BackupBeforeUpdate { get; set; } = true;
    public bool BackupBeforeConfigSave { get; set; } = true;
    public bool BackupBeforeModChange { get; set; } = true;
    public bool BackupBeforeClusterChange { get; set; } = true;
    public bool CompressBackups { get; set; } = true;
    public int RetentionLimit { get; set; } = 10;
    public string Schedule { get; set; } = "Daily at 02:00";
}

public sealed class ArkMonitoringSettings
{
    public bool MonitorProcess { get; set; } = true;
    public bool MonitorPorts { get; set; } = true;
    public bool MonitorQuery { get; set; } = true;
    public bool MonitorRcon { get; set; } = true;
    public bool DetectCrashKeywords { get; set; } = true;
    public int MemoryWarningLimitMb { get; set; } = 8192;
    public int BackupOverdueHours { get; set; } = 24;
}

public sealed class ArkAdvancedSettings
{
    public int AutoSaveInterval { get; set; } = 15;
    public int GracefulShutdownTimeout { get; set; } = 30;
    public bool MarkServerNeedsRestart { get; set; }
}

public enum ArkSettingFileLocation
{
    LaunchArguments,
    GameUserSettingsIni,
    GameIni,
    RawCustom,
    ClusterSettings,
    ModSettings,
    BackupSettings,
    MonitoringSettings
}

public enum ArkSettingDataType
{
    String,
    Password,
    Boolean,
    Integer,
    Decimal,
    Enum,
    StringList,
    RepeatedLine
}

public sealed class ArkSettingDefinition
{
    public string Key { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string Category { get; init; } = string.Empty;
    public ArkSettingFileLocation FileLocation { get; init; }
    public string IniSection { get; init; } = string.Empty;
    public ArkSettingDataType DataType { get; init; }
    public string DefaultValue { get; init; } = string.Empty;
    public decimal? Min { get; init; }
    public decimal? Max { get; init; }
    public IReadOnlyList<string> AllowedValues { get; init; } = Array.Empty<string>();
    public bool RestartRequired { get; init; } = true;
    public bool AdvancedSetting { get; init; }
    public string WarningText { get; init; } = string.Empty;
    public string WikiReference { get; init; } = "https://ark.wiki.gg/wiki/Server_configuration";
}
