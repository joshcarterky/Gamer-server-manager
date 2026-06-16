using GameServerManager.Core.Models;

namespace GameServerManager.GameProviders;

public class PalworldProvider : GameServerProviderBase
{
    public override string GameId => "palworld";
    public override string GameName => "Palworld";
    public override int? SteamAppId => 2394010;
    public override string DefaultInstallFolder => "Servers/Palworld";
    public override string ExecutableRelativePath => "PalServer.exe";
    public override string ConfigFolder => "Pal/Saved/Config/WindowsServer";
    public override string SavesFolder => "Pal/Saved/SaveGames";
    public override string LogsFolder => "Pal/Saved/Logs";

    public override IReadOnlyList<ServerPort> DefaultPorts => new[]
    {
        new ServerPort { Name = "Game",    Port = 8211,  DefaultPort = 8211,  Protocol = PortProtocol.UDP, Description = "Game traffic (UDP)", IsRequired = true },
        new ServerPort { Name = "RCON",    Port = 25575, DefaultPort = 25575, Protocol = PortProtocol.TCP, Description = "Remote console (TCP)", IsRequired = false },
        new ServerPort { Name = "REST API",Port = 8212,  DefaultPort = 8212,  Protocol = PortProtocol.TCP, Description = "REST API (TCP)", IsRequired = false }
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
        // These are provider-level settings stored in ServerProfile.Settings.
        // The full Palworld settings (100+ OptionSettings keys) are managed by
        // PalworldSettingRegistry and PalworldConfigService in the Services project.
        Text("PublicLobbyEnabled", "Public Lobby",       "Adds -publiclobby to the launch command.", "Network / Ports"),
        Text("PublicIP",           "Public IP",           "Public IP override announced to Steam.",   "Network / Ports"),
        Number("PublicPort",       "Public Port",         "8211", 1, 65535, "Network / Ports"),
        Number("RESTAPIPort",      "REST API Port",       "8212", 1, 65535, "REST API"),
        Toggle("RESTAPIEnabled",   "Enable REST API",     "False", "REST API"),
        Toggle("RCONEnabled",      "Enable RCON",         "False", "RCON"),
        Toggle("PerformanceMode",  "Performance Mode",    "False", "Performance"),
        Number("WorkerThreadCount","Worker Thread Count", "0", 0, 64, "Performance"),
        Toggle("NoMods",           "Disable Mods (-NoMods)", "False", "Mods"),
        Text("LogFormat",          "Log Format",          "Text", "Advanced"),
        Text("WorkshopDir",        "Workshop Directory",  "", "Mods"),
    };

    public override ServerLaunchCommand BuildStartCommand(ServerProfile profile)
    {
        var executablePath = !string.IsNullOrWhiteSpace(profile.ExecutablePath)
            ? profile.ExecutablePath
            : Path.Combine(profile.InstallPath, ExecutableRelativePath);

        var gamePort = FindPort(profile, "Game", 8211);
        var maxPlayers = profile.MaxPlayers <= 0 ? 32 : profile.MaxPlayers;

        var args = new List<string>
        {
            $"-port={gamePort}",
            $"-players={maxPlayers}"
        };

        if (ParseBool(profile, "PublicLobbyEnabled"))
            args.Add("-publiclobby");

        var publicIp = Get(profile, "PublicIP");
        if (!string.IsNullOrWhiteSpace(publicIp))
            args.Add($"-publicip={publicIp}");

        var publicPort = ParseInt(profile, "PublicPort", gamePort);
        if (publicPort != gamePort)
            args.Add($"-publicport={publicPort}");

        var logFormat = Get(profile, "LogFormat");
        args.Add($"-logformat={(string.IsNullOrWhiteSpace(logFormat) ? "Text" : logFormat)}");

        if (ParseBool(profile, "PerformanceMode"))
        {
            args.Add("-useperfthreads");
            args.Add("-NoAsyncLoadingThread");
            args.Add("-UseMultithreadForDS");
        }

        var threads = ParseInt(profile, "WorkerThreadCount", 0);
        if (threads > 0)
            args.Add($"-NumberOfWorkerThreadsServer={threads}");

        var workshopDir = Get(profile, "WorkshopDir");
        if (!string.IsNullOrWhiteSpace(workshopDir))
            args.Add($"-workshopdir=\"{workshopDir}\"");

        if (ParseBool(profile, "NoMods"))
            args.Add("-NoMods");

        if (!string.IsNullOrWhiteSpace(profile.LaunchArgs))
            args.Add(profile.LaunchArgs);

        return new ServerLaunchCommand
        {
            ExecutablePath = executablePath,
            WorkingDirectory = profile.InstallPath,
            Arguments = string.Join(" ", args)
        };
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static int FindPort(ServerProfile p, string name, int fallback)
        => p.Ports.FirstOrDefault(pp => pp.Name.Equals(name, StringComparison.OrdinalIgnoreCase))?.Port ?? fallback;

    private static string Get(ServerProfile p, string key)
        => p.Settings.TryGetValue(key, out var v) ? v : string.Empty;

    private static int ParseInt(ServerProfile p, string key, int fallback)
        => p.Settings.TryGetValue(key, out var v) && int.TryParse(v, out var n) ? n : fallback;

    private static bool ParseBool(ServerProfile p, string key, bool fallback = false)
        => p.Settings.TryGetValue(key, out var v) && bool.TryParse(v, out var b) ? b : fallback;

    private static ServerSettingDefinition Text(string key, string name, string def, string? cat = null)
        => new() { SettingKey = key, DisplayName = name, DefaultValue = def, ControlType = SettingControlType.TextBox, Category = cat, RequiresRestart = true };

    private static ServerSettingDefinition Number(string key, string name, string def, int min, int max, string? cat = null)
        => new() { SettingKey = key, DisplayName = name, DefaultValue = def, ControlType = SettingControlType.NumberBox, MinValue = min, MaxValue = max, Category = cat, RequiresRestart = true };

    private static ServerSettingDefinition Toggle(string key, string name, string def, string? cat = null)
        => new() { SettingKey = key, DisplayName = name, DefaultValue = def, ControlType = SettingControlType.Toggle, Category = cat, RequiresRestart = true };
}
