using GameServerManager.Core.Models;

namespace GameServerManager.GameProviders;

public class SevenDaysToDieProvider : GameServerProviderBase
{
    public const string GameIdConst = "seven_days_to_die";
    public const int DedicatedServerAppId = 294420;

    public override string GameId => GameIdConst;
    public override string GameName => "7 Days to Die";
    public override int? SteamAppId => DedicatedServerAppId;
    public override string DefaultInstallFolder => "Servers/7_Days_To_Die";

    // Windows: 7DaysToDieServer.exe; Linux build uses .x86_64 but this manager targets Windows
    public override string ExecutableRelativePath => "7DaysToDieServer.exe";

    // Config is at the server root (serverconfig.xml lives next to the exe)
    public override string ConfigFolder => ".";
    public override string SavesFolder => "Saves";
    public override string LogsFolder => "logs";

    public override IReadOnlyList<ServerPort> DefaultPorts => new[]
    {
        // Base game port: TCP (server-to-client) + UDP (primary game traffic)
        new ServerPort { Name = "Game",     Port = 26900, DefaultPort = 26900, Protocol = PortProtocol.Both, Description = "Primary game traffic (TCP + UDP)",   IsRequired = true  },
        // Additional UDP ports automatically used adjacent to the base port
        new ServerPort { Name = "GameUDP1", Port = 26901, DefaultPort = 26901, Protocol = PortProtocol.UDP,  Description = "Additional game traffic UDP +1",    IsRequired = true  },
        new ServerPort { Name = "GameUDP2", Port = 26902, DefaultPort = 26902, Protocol = PortProtocol.UDP,  Description = "Additional game traffic UDP +2",    IsRequired = true  },
        new ServerPort { Name = "GameUDP3", Port = 26903, DefaultPort = 26903, Protocol = PortProtocol.UDP,  Description = "Additional game traffic UDP +3",    IsRequired = true  },
        // Web control panel / WebAPI
        new ServerPort { Name = "Web",      Port = 8080,  DefaultPort = 8080,  Protocol = PortProtocol.TCP,  Description = "Web control panel (optional)",      IsRequired = false },
        // Telnet administration
        new ServerPort { Name = "Telnet",   Port = 8081,  DefaultPort = 8081,  Protocol = PortProtocol.TCP,  Description = "Telnet administration (optional)",  IsRequired = false }
    };

    public override GameServerFeatures SupportedFeatures =>
        GameServerFeatures.SteamCmdInstall |
        GameServerFeatures.Password |
        GameServerFeatures.AdminPassword |
        GameServerFeatures.Mods |
        GameServerFeatures.Backups |
        GameServerFeatures.Console |
        GameServerFeatures.WorldName |
        GameServerFeatures.MaxPlayers;

    public override IReadOnlyList<ServerSettingDefinition> SettingsDefinitions => _settings;

    // Settings are grouped into categories matching serverconfig.xml property groups.
    private static readonly ServerSettingDefinition[] _settings = new[]
    {
        // ── General ──────────────────────────────────────────────────────────────
        Text("ServerName",                "Server Name",                "My 7 Days Server",  true,  "General"),
        Text("ServerDescription",         "Description",                "",                  false, "General"),
        Text("ServerWebsiteURL",          "Website URL",                "",                  false, "General"),
        Text("ServerLoginConfirmationText","Login Confirmation Text",   "",                  false, "General"),
        Dropdown("Region",                "Region",                     "NorthAmericaEast",  new[]
        {
            "NorthAmericaEast:North America (East)",
            "NorthAmericaWest:North America (West)",
            "CentralAmerica:Central America",
            "SouthAmerica:South America",
            "Europe:Europe",
            "Russia:Russia",
            "Asia:Asia",
            "MiddleEast:Middle East",
            "Africa:Africa",
            "Oceania:Oceania"
        }, "General"),
        Dropdown("Language",              "Language",                   "English",           new[]
        {
            "English:English",
            "German:German",
            "Spanish:Spanish",
            "French:French",
            "Italian:Italian",
            "Japanese:Japanese",
            "Korean:Korean",
            "Polish:Polish",
            "Portuguese:Portuguese",
            "Russian:Russian",
            "Turkish:Turkish",
            "Chinese_Simplified:Chinese (Simplified)"
        }, "General"),

        // ── Networking ───────────────────────────────────────────────────────────
        Number("ServerPort",              "Game Port",                  "26900", 1, 65530, "Networking"),
        Dropdown("ServerVisibility",      "Server Visibility",          "2",     new[]
        {
            "0:Not Listed (Private)",
            "1:Friends Only",
            "2:Public"
        }, "Networking"),
        Toggle("ServerAllowCrossplay",    "Allow Console Crossplay",    "False", "Networking"),
        Number("ServerMaxWorldTransferSpeedKiBs", "Max World Transfer Speed (KiB/s)", "512", 64, 131072, "Networking"),
        Text("ServerDisabledNetworkProtocols", "Disabled Network Protocols", "SteamNetworking", false, "Networking"),

        // ── Player Slots ─────────────────────────────────────────────────────────
        Number("ServerMaxPlayerCount",    "Max Players",                "8",     1, 64,    "Player Slots"),
        Number("ServerReservedSlots",     "Reserved Slots",             "0",     0, 64,    "Player Slots"),
        Number("ServerReservedSlotsPermission", "Reserved Slots Permission Level", "100", 0, 1000, "Player Slots"),
        Toggle("PersistentPlayerProfiles","Persistent Player Profiles", "False", "Player Slots"),

        // ── Security ─────────────────────────────────────────────────────────────
        Password("ServerPassword",        "Server Password",            "",      false, "Security"),
        Password("ServerAdminPassword",   "Admin Password",             "",      false, "Security"),
        Toggle("EACEnabled",              "Easy Anti-Cheat (EAC)",      "True",  "Security"),
        Toggle("IgnoreEOSSanctions",      "Ignore EOS Sanctions",       "False", "Security"),
        Toggle("HideCommandExecutionLog", "Hide Command Execution Log", "False", "Security"),
        Text("AdminFileName",             "Admin File Name",            "serveradmin.xml", false, "Security"),

        // ── Web Control Panel ─────────────────────────────────────────────────────
        Toggle("ControlPanelEnabled",     "Enable Web Control Panel",   "False", "Web Control Panel"),
        Number("ControlPanelPort",        "Web Control Panel Port",     "8080",  1, 65535, "Web Control Panel"),
        Password("ControlPanelPassword",  "Web Control Panel Password", "",      false,    "Web Control Panel"),

        // ── Telnet ────────────────────────────────────────────────────────────────
        Toggle("TelnetEnabled",           "Enable Telnet",              "False", "Telnet"),
        Number("TelnetPort",              "Telnet Port",                "8081",  1, 65535, "Telnet"),
        Password("TelnetPassword",        "Telnet Password",            "",      false,    "Telnet"),
        Number("TelnetFailedLoginLimit",  "Failed Login Limit",         "10",    0, 100,   "Telnet"),
        Number("TelnetFailedLoginsBlocktime", "Failed Login Block Duration (s)", "10", 0, 3600, "Telnet"),

        // ── Data and Storage ──────────────────────────────────────────────────────
        Folder("UserDataFolder",          "User Data Folder",           "",      false, "Data & Storage"),
        Text("SaveGameFolder",            "Save Game Folder Override",  "",      false, "Data & Storage"),
        Number("MaxUncoveredMapChunksPerPlayer", "Max Uncovered Map Chunks Per Player", "131072", 0, 1000000, "Data & Storage"),
        Number("MaxChunkAge",             "Max Chunk Age (days)",       "-1",    -1, 365,  "Data & Storage"),
        Number("SaveDataLimit",           "Save Data Limit (MB)",       "-1",    -1, 100000, "Data & Storage"),

        // ── World ─────────────────────────────────────────────────────────────────
        Dropdown("GameWorld",             "Game World",                 "Navezgane", new[]
        {
            "Navezgane:Navezgane (Preset)",
            "RWG:Random World Generation"
        }, "World"),
        Text("WorldGenSeed",              "World Gen Seed",             "asdf",  false, "World"),
        Dropdown("WorldGenSize",          "World Gen Size",             "6144",  new[]
        {
            "6144:Small (6144)",
            "8192:Medium (8192)",
            "10240:Large (10240)"
        }, "World"),
        Text("GameName",                  "Save Game Name",             "My Game", true, "World"),
        Dropdown("GameMode",              "Game Mode",                  "GameModeSurvival", new[]
        {
            "GameModeSurvival:Survival",
            "GameModeCreative:Creative"
        }, "World"),

        // ── V3.0 Sandbox ──────────────────────────────────────────────────────────
        Text("SandboxCode",               "Sandbox Code (V3)",          "",      false, "Sandbox (V3)"),

        // ── Branch Selection ──────────────────────────────────────────────────────
        Dropdown("SteamBranch",           "Steam Branch",               "stable", new[]
        {
            "stable:Stable (Public)",
            "latest_experimental:Experimental",
            "latest_experimental_fallback:Experimental Fallback",
            "custom:Custom Branch"
        }, "Installation", advanced: true),
        Text("CustomSteamBranch",         "Custom Branch Name",         "",      false, "Installation", advanced: true),
    };

    public override ServerLaunchCommand BuildStartCommand(ServerProfile profile)
    {
        var installPath = profile.InstallPath;
        var executablePath = !string.IsNullOrWhiteSpace(profile.ExecutablePath)
            ? profile.ExecutablePath
            : Path.Combine(installPath, ExecutableRelativePath);

        // Prefer the user-configured configfile path; fall back to the standard location.
        var configPath = Path.Combine(installPath, "serverconfig.xml");

        // UserDataFolder must be explicit to isolate per-server data from the default AppData location.
        var userDataFolder = Get(profile, "UserDataFolder");
        if (string.IsNullOrWhiteSpace(userDataFolder))
        {
            userDataFolder = Path.Combine(installPath, "UserData");
        }

        var logPath = Path.Combine(installPath, "logs",
            $"server_{DateTime.UtcNow:yyyyMMdd_HHmmss}.log");

        // Build argument list with absolute quoted paths — no shell concatenation.
        var args = new List<string>
        {
            $"-logFile \"{logPath}\"",
            "-quit",
            "-batchmode",
            "-nographics",
            "-dedicated",
            $"-configfile=\"{configPath}\"",
            $"-UserDataFolder=\"{userDataFolder}\""
        };

        if (!string.IsNullOrWhiteSpace(profile.LaunchArgs))
        {
            args.Add(profile.LaunchArgs.Trim());
        }

        return new ServerLaunchCommand
        {
            ExecutablePath = executablePath,
            WorkingDirectory = installPath,
            Arguments = string.Join(' ', args)
        };
    }

    // ── Helper factories (mirror ArkSurvivalAscendedProvider style) ──────────────

    private static string Get(ServerProfile profile, string key)
        => profile.Settings.TryGetValue(key, out var v) ? v : string.Empty;

    protected static ServerSettingDefinition Text(string key, string displayName, string defaultValue,
        bool required = false, string? category = null, bool advanced = false)
        => new() { SettingKey = key, DisplayName = displayName, DefaultValue = defaultValue,
                   ControlType = SettingControlType.TextBox, IsRequired = required,
                   Category = category, IsAdvanced = advanced, RequiresRestart = true };

    protected static ServerSettingDefinition Password(string key, string displayName, string defaultValue,
        bool required = false, string? category = null)
        => new() { SettingKey = key, DisplayName = displayName, DefaultValue = defaultValue,
                   ControlType = SettingControlType.PasswordField, IsRequired = required,
                   Category = category, RequiresRestart = true };

    protected static ServerSettingDefinition Number(string key, string displayName, string defaultValue,
        int min, int max, string? category = null, bool advanced = false)
        => new() { SettingKey = key, DisplayName = displayName, DefaultValue = defaultValue,
                   ControlType = SettingControlType.NumberBox, MinValue = min, MaxValue = max,
                   Category = category, IsAdvanced = advanced, RequiresRestart = true };

    protected static ServerSettingDefinition Toggle(string key, string displayName, string defaultValue,
        string? category = null, bool advanced = false)
        => new() { SettingKey = key, DisplayName = displayName, DefaultValue = defaultValue,
                   ControlType = SettingControlType.Toggle, Category = category,
                   IsAdvanced = advanced, RequiresRestart = true };

    protected static ServerSettingDefinition Dropdown(string key, string displayName, string defaultValue,
        string[] options, string? category = null, bool advanced = false)
        => new() { SettingKey = key, DisplayName = displayName, DefaultValue = defaultValue,
                   ControlType = SettingControlType.Dropdown,
                   Options = new List<string>(options),
                   Category = category, IsAdvanced = advanced, RequiresRestart = true };

    protected static ServerSettingDefinition Folder(string key, string displayName, string defaultValue,
        bool required = false, string? category = null)
        => new() { SettingKey = key, DisplayName = displayName, DefaultValue = defaultValue,
                   ControlType = SettingControlType.FolderPicker, IsRequired = required,
                   Category = category, RequiresRestart = true };
}
