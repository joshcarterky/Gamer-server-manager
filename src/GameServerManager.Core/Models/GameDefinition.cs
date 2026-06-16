namespace GameServerManager.Core.Models;

/// <summary>
/// Static metadata that describes a supported dedicated server game.
/// </summary>
public class GameDefinition
{
    public string GameId { get; set; } = string.Empty;
    public string GameName { get; set; } = string.Empty;
    public int? SteamAppId { get; set; }
    public string DefaultInstallFolder { get; set; } = string.Empty;
    public string ExecutableRelativePath { get; set; } = string.Empty;
    public List<ServerPort> DefaultPorts { get; set; } = new();
    public string ConfigFolder { get; set; } = string.Empty;
    public string SavesFolder { get; set; } = string.Empty;
    public string LogsFolder { get; set; } = string.Empty;
    public GameServerFeatures SupportedFeatures { get; set; } = GameServerFeatures.None;
    public List<ServerSettingDefinition> SettingsDefinitions { get; set; } = new();
}

[Flags]
public enum GameServerFeatures
{
    None = 0,
    SteamCmdInstall = 1,
    Password = 2,
    AdminPassword = 4,
    Rcon = 8,
    Mods = 16,
    Backups = 32,
    Console = 64,
    WorldName = 128,
    MaxPlayers = 256
}
