using GameServerManager.Core.Models;

namespace GameServerManager.GameProviders;

public class ArkSurvivalAscendedProvider : GameServerProviderBase
{
    public override string GameId => ArkSurvivalAscendedServerProfile.GameId;
    public override string GameName => "ARK: Survival Ascended";
    public override int? SteamAppId => ArkSurvivalAscendedServerProfile.SteamCmdAppId;
    public override string DefaultInstallFolder => "Servers/ARK_Survival_Ascended";
    public override string ExecutableRelativePath => ArkSurvivalAscendedServerProfile.DefaultExecutableRelativePath;
    public override string ConfigFolder => "ShooterGame/Saved/Config/WindowsServer";
    public override string SavesFolder => "ShooterGame/Saved/SavedArks";
    public override string LogsFolder => "ShooterGame/Saved/Logs";

    public override IReadOnlyList<ServerPort> DefaultPorts => new[]
    {
        Port("Game", 7777, PortProtocol.UDP, "Game traffic"),
        Port("Query", 27015, PortProtocol.UDP, "Steam query"),
        Port("RCON", 27020, PortProtocol.TCP, "Remote console", false)
    };

    public override GameServerFeatures SupportedFeatures =>
        GameServerFeatures.SteamCmdInstall |
        GameServerFeatures.Password |
        GameServerFeatures.AdminPassword |
        GameServerFeatures.Rcon |
        GameServerFeatures.Mods |
        GameServerFeatures.Backups |
        GameServerFeatures.Console |
        GameServerFeatures.WorldName |
        GameServerFeatures.MaxPlayers;

    public override IReadOnlyList<ServerSettingDefinition> SettingsDefinitions => new[]
    {
        Text("SessionName", "Session Name", "ARK ASA Server", true, "Admin / Passwords"),
        Text("MapName", "Map Name", "TheIsland_WP", true, "Maps"),
        Number("MaxPlayers", "Max Players", "70", 1, 200, "Admin / Passwords"),
        Number("Port", "Game Port", "7777", 1, 65535, "Network / Ports"),
        Number("QueryPort", "Query Port", "27015", 1, 65535, "Network / Ports"),
        Number("RCONPort", "RCON Port", "27020", 1, 65535, "RCON / Console"),
        Toggle("RCONEnabled", "Enable RCON", "True", "RCON / Console"),
        Text("AltSaveDirectoryName", "Alt Save Directory", string.Empty, false, "Maps"),
        Toggle("Cluster.Enabled", "Enable Cluster", "False", "Cluster"),
        Text("Cluster.Id", "Cluster ID", string.Empty, false, "Cluster"),
        Text("Cluster.DirectoryOverride", "Cluster Directory Override", string.Empty, false, "Cluster"),
        Text("ModIDs", "Mod IDs", string.Empty, false, "Mods")
    };

    public override ServerLaunchCommand BuildStartCommand(ServerProfile profile)
    {
        var executablePath = !string.IsNullOrWhiteSpace(profile.ExecutablePath)
            ? profile.ExecutablePath
            : Path.Combine(profile.InstallPath, ExecutableRelativePath);
        var map = string.IsNullOrWhiteSpace(profile.MapName) ? "TheIsland_WP" : profile.MapName;
        var gamePort = FindPort(profile, "Game", ParseInt(profile, "Port", 7777));
        var queryPort = FindPort(profile, "Query", ParseInt(profile, "QueryPort", 27015));
        var rconPort = FindPort(profile, "RCON", ParseInt(profile, "RCONPort", 27020));
        var rconEnabled = ParseBool(profile, "RCONEnabled", true);
        var adminPassword = string.IsNullOrWhiteSpace(profile.AdminPassword)
            ? Get(profile, "ServerAdminPassword")
            : profile.AdminPassword;

        var query = new List<string>
        {
            "listen",
            $"SessionName=\"{Escape(profile.ServerName)}\"",
            $"Port={gamePort}",
            $"QueryPort={queryPort}",
            $"RCONPort={rconPort}",
            $"RCONEnabled={(rconEnabled ? "True" : "False")}",
            $"ServerAdminPassword=\"{Escape(adminPassword)}\""
        };

        if (!string.IsNullOrWhiteSpace(profile.Password))
        {
            query.Add($"ServerPassword=\"{Escape(profile.Password)}\"");
        }

        var altSave = Get(profile, "AltSaveDirectoryName");
        if (!string.IsNullOrWhiteSpace(altSave))
        {
            query.Add($"AltSaveDirectoryName={altSave}");
        }

        var flags = new List<string>();
        if (IsClusterEnabled(profile))
        {
            var clusterId = First(profile, "Cluster.Id", "ClusterID");
            var clusterDir = First(profile, "Cluster.DirectoryOverride", "ClusterDirOverride");
            if (!string.IsNullOrWhiteSpace(clusterId))
            {
                flags.Add($"-clusterid={EscapeArgument(clusterId)}");
            }

            if (!string.IsNullOrWhiteSpace(clusterDir))
            {
                flags.Add($"-ClusterDirOverride=\"{Escape(clusterDir)}\"");
            }
        }

        var modIds = Get(profile, "ModIDs");
        if (string.IsNullOrWhiteSpace(modIds) && profile.Mods.Count > 0)
        {
            modIds = string.Join(',', profile.Mods.Where(mod => mod.IsEnabled).Select(mod => mod.Id));
        }

        // ASA uses -WinLiveMaxPlayers, not the legacy URL-style MaxPlayers= parameter
        var maxPlayers = profile.MaxPlayers <= 0 ? 70 : profile.MaxPlayers;
        flags.Add($"-WinLiveMaxPlayers={maxPlayers}");

        if (!string.IsNullOrWhiteSpace(modIds))
        {
            flags.Add($"-mods={modIds}");
        }

        if (ParseBool(profile, "NoBattlEye"))
        {
            flags.Add("-NoBattlEye");
        }

        if (ParseBool(profile, "EnableConsoleLog", true))
        {
            flags.Add("-log");
        }

        if (!string.IsNullOrWhiteSpace(profile.LaunchArgs))
        {
            flags.Add(profile.LaunchArgs);
        }

        return new ServerLaunchCommand
        {
            ExecutablePath = executablePath,
            WorkingDirectory = profile.InstallPath,
            Arguments = $"{map}?{string.Join('?', query)} {string.Join(' ', flags)}".Trim()
        };
    }

    protected static ServerPort Port(string name, int port, PortProtocol protocol, string description, bool required = true)
    {
        return new ServerPort { Name = name, Port = port, DefaultPort = port, Protocol = protocol, Description = description, IsRequired = required };
    }

    protected static int FindPort(ServerProfile profile, string name, int fallback)
    {
        return profile.Ports.FirstOrDefault(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase))?.Port ?? fallback;
    }

    private static string Get(ServerProfile profile, string key)
    {
        return profile.Settings.TryGetValue(key, out var value) ? value : string.Empty;
    }

    private static string First(ServerProfile profile, params string[] keys)
    {
        foreach (var key in keys)
        {
            var value = Get(profile, key);
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return string.Empty;
    }

    private static bool IsClusterEnabled(ServerProfile profile)
    {
        if (profile.Settings.TryGetValue("Cluster.Enabled", out var modern) && bool.TryParse(modern, out var modernEnabled))
        {
            return modernEnabled;
        }

        if (profile.Settings.TryGetValue("ClusterEnabled", out var legacy) && bool.TryParse(legacy, out var legacyEnabled))
        {
            return legacyEnabled;
        }

        return false;
    }

    private static int ParseInt(ServerProfile profile, string key, int fallback)
    {
        return profile.Settings.TryGetValue(key, out var value) && int.TryParse(value, out var parsed) ? parsed : fallback;
    }

    private static bool ParseBool(ServerProfile profile, string key, bool fallback = false)
    {
        return profile.Settings.TryGetValue(key, out var value) && bool.TryParse(value, out var parsed) ? parsed : fallback;
    }

    private static string Escape(string value)
    {
        return value.Replace("\"", "\\\"", StringComparison.Ordinal);
    }

    private static string EscapeArgument(string value)
    {
        return value.Contains(' ', StringComparison.Ordinal) ? $"\"{Escape(value)}\"" : Escape(value);
    }

    protected static ServerSettingDefinition Text(string key, string displayName, string defaultValue, bool required = false, string? category = null)
    {
        return new ServerSettingDefinition { SettingKey = key, DisplayName = displayName, DefaultValue = defaultValue, ControlType = SettingControlType.TextBox, IsRequired = required, Category = category, RequiresRestart = true };
    }

    protected static ServerSettingDefinition Number(string key, string displayName, string defaultValue, int min, int max, string? category = null)
    {
        return new ServerSettingDefinition { SettingKey = key, DisplayName = displayName, DefaultValue = defaultValue, ControlType = SettingControlType.NumberBox, MinValue = min, MaxValue = max, Category = category, RequiresRestart = true };
    }

    protected static ServerSettingDefinition Toggle(string key, string displayName, string defaultValue, string? category = null)
    {
        return new ServerSettingDefinition { SettingKey = key, DisplayName = displayName, DefaultValue = defaultValue, ControlType = SettingControlType.Toggle, Category = category, RequiresRestart = true };
    }
}

public class ARKProvider : ArkSurvivalAscendedProvider
{
}

public class ArkSurvivalAscendedLegacyProvider : ArkSurvivalAscendedProvider
{
    public override string GameId => ArkSurvivalAscendedServerProfile.LegacyGameId;
}
