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
    public override string ExecutableRelativePath => "7DaysToDieServer.exe";
    public override string ConfigFolder => ".";
    public override string SavesFolder => "Saves";
    public override string LogsFolder => "logs";

    public override IReadOnlyList<ServerPort> DefaultPorts => new[]
    {
        new ServerPort { Name = "Game",     Port = 26900, DefaultPort = 26900, Protocol = PortProtocol.Both, Description = "Primary game traffic (TCP + UDP)",  IsRequired = true  },
        new ServerPort { Name = "GameUDP1", Port = 26901, DefaultPort = 26901, Protocol = PortProtocol.UDP,  Description = "Additional UDP +1",                 IsRequired = true  },
        new ServerPort { Name = "GameUDP2", Port = 26902, DefaultPort = 26902, Protocol = PortProtocol.UDP,  Description = "Additional UDP +2",                 IsRequired = true  },
        new ServerPort { Name = "GameUDP3", Port = 26903, DefaultPort = 26903, Protocol = PortProtocol.UDP,  Description = "Additional UDP +3",                 IsRequired = true  },
        new ServerPort { Name = "Web",      Port = 8080,  DefaultPort = 8080,  Protocol = PortProtocol.TCP,  Description = "Web control panel / dashboard",     IsRequired = false },
        new ServerPort { Name = "Telnet",   Port = 8081,  DefaultPort = 8081,  Protocol = PortProtocol.TCP,  Description = "Telnet administration",             IsRequired = false }
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

    private static readonly ServerSettingDefinition[] _settings =
    [
        // ── General ──────────────────────────────────────────────────────────────
        Text("ServerName",
            "Server Name", "My 7 Days Server", required: true, category: "General",
            description: "The name shown in the Steam server browser.",
            helpText: "This is the primary identifier players see when searching for your server. Keep it concise and descriptive."),

        Text("ServerDescription",
            "Description", "", category: "General",
            description: "Longer description shown on the Steam listing page.",
            placeholder: "A short description of your server's rules and play style"),

        Text("ServerWebsiteURL",
            "Website URL", "", category: "General",
            description: "Optional link shown in the Steam server listing.",
            placeholder: "https://your-server-website.com"),

        Text("ServerLoginConfirmationText",
            "Login Confirmation Message", "", category: "General",
            description: "Message shown to players when they attempt to connect. Leave blank to allow immediate joining.",
            helpText: "Use this to display server rules or welcome messages. Players must acknowledge the text before they can join.",
            placeholder: "Welcome! Please read the rules at..."),

        Dropdown("Region", "Region", "NorthAmericaEast",
            [
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
            ], category: "General",
            description: "Geographic region closest to your server. Affects how the server appears in the browser."),

        Dropdown("Language", "Language", "English",
            [
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
            ], category: "General",
            description: "Interface language for the server."),

        // ── Networking ───────────────────────────────────────────────────────────
        Number("ServerPort",
            "Game Port", "26900", min: 1, max: 65530, unit: "port", category: "Networking",
            description: "Primary game port (TCP + UDP). The game also uses the next 3 UDP ports automatically.",
            helpText: "Ensure ports BasePort through BasePort+3 are open in your firewall and router. Default is 26900–26903.",
            recommendedValue: "26900 (default)"),

        Dropdown("ServerVisibility", "Server Visibility", "2",
            [
                "0:Not Listed (Private)",
                "1:Friends Only",
                "2:Public"
            ], category: "Networking",
            description: "Controls whether this server appears in the Steam server browser.",
            helpText: "Public servers are indexed globally. Friends Only requires Steam friends. Not Listed requires direct connection via IP."),

        Toggle("ServerAllowCrossplay",
            "Allow Console Crossplay", "False", category: "Networking",
            description: "Allows Xbox and PlayStation players to join. Requires ≤ 8 players, EAC enabled, and EOS sanctions active.",
            helpText: "Enabling crossplay imposes restrictions: max 8 players, EAC must be on, IgnoreEOSSanctions must be off. The manager validates these automatically.",
            recommendedValue: "Disabled for PC-only servers"),

        Number("ServerMaxWorldTransferSpeedKiBs",
            "World Transfer Speed", "512", min: 64, max: 131072, unit: "KiB/s", category: "Networking",
            description: "Maximum speed for transferring world chunks to connecting players.",
            helpText: "Higher values let players load in faster but increase server bandwidth. Reduce if your uplink is limited.",
            recommendedValue: "512 for most servers"),

        Text("ServerDisabledNetworkProtocols",
            "Disabled Network Protocols", "SteamNetworking", category: "Networking",
            description: "Comma-separated protocols to disable. 'SteamNetworking' disables Steam's relay (reduces latency but requires direct connection).",
            helpText: "Leave as 'SteamNetworking' to force direct connections. Remove the value to allow Steam relay (useful if players cannot connect directly)."),

        // ── Player Slots ─────────────────────────────────────────────────────────
        Number("ServerMaxPlayerCount",
            "Max Players", "8", min: 1, max: 64, unit: "players", category: "Player Slots",
            description: "Maximum simultaneous players. Must be 8 or fewer when crossplay is enabled.",
            helpText: "Higher player counts require more server RAM and CPU. Each player loads additional chunks and AI. Crossplay is locked to ≤ 8.",
            recommendedValue: "8–16 for balanced performance"),

        Number("ServerReservedSlots",
            "Reserved Slots", "0", min: 0, max: 64, unit: "slots", category: "Player Slots",
            description: "Slots held back for players with sufficient permission level (typically moderators or Patreon supporters).",
            helpText: "Reserved slots reduce the effective public capacity. If Max Players is 16 and Reserved Slots is 2, only 14 public slots are available."),

        Number("ServerReservedSlotsPermission",
            "Reserved Slot Permission Level", "100", min: 0, max: 1000, unit: "level", category: "Player Slots",
            description: "Minimum permission level required to use a reserved slot.",
            helpText: "Permission levels: 0 = owner, 1–100 = moderator/admin range, 1000 = regular player. Default 100 means moderators can use reserved slots.",
            recommendedValue: "100 (moderators and above)"),

        Number("ServerAdminSlots",
            "Admin Slots", "0", min: 0, max: 64, unit: "slots", category: "Player Slots",
            description: "This many admins can still join even when the server has reached Max Players.",
            helpText: "Unlike reserved slots, admin slots are on top of Max Players — they let staff join a full server."),

        Number("ServerAdminSlotsPermission",
            "Admin Slot Permission Level", "0", min: 0, max: 1000, unit: "level", category: "Player Slots",
            description: "Required permission level to use the admin slots.",
            helpText: "Default 0 restricts admin slots to the server owner. Raise it to include moderators."),

        Toggle("PersistentPlayerProfiles",
            "Persistent Player Profiles", "False", category: "Player Slots",
            description: "When enabled, player profiles are kept after character death instead of being reset.",
            helpText: "By default, dying in hardcore mode can wipe player progress. Enable this to preserve progress through deaths."),

        // ── Security ─────────────────────────────────────────────────────────────
        Password("ServerPassword",
            "Server Password", "", category: "Security",
            description: "Password required to join. Leave blank for a public open server.",
            helpText: "Passwords are case-sensitive. Communicate the password to your players through a separate channel. The password appears in serverconfig.xml — keep the file access-controlled."),

        Toggle("EACEnabled",
            "Easy Anti-Cheat (EAC)", "True", category: "Security",
            description: "Enables Easy Anti-Cheat. Required for crossplay. Disable only for private or heavily modded servers.",
            helpText: "EAC detects many common cheats and modded clients. Some mods require disabling EAC. Disabling it on a public server greatly increases cheating risk.",
            recommendedValue: "Enabled for public servers"),

        Toggle("IgnoreEOSSanctions",
            "Ignore EOS Sanctions", "False", category: "Security",
            description: "Skips Epic Online Services ban checks. Must be off when crossplay is enabled.",
            helpText: "EOS sanctions are applied to accounts banned through Epic's systems. Ignoring them is only appropriate for private servers where you trust all players.",
            recommendedValue: "Disabled (default)"),

        // Verified against the shipped V3 serverconfig.xml: this is a 0–3 mode,
        // not a boolean. Writing True/False here would hand the game an invalid value.
        Dropdown("HideCommandExecutionLog", "Hide Command Log", "0",
            [
                "0:Show everything",
                "1:Hide from Telnet / control panel",
                "2:Also hide from remote game clients",
                "3:Hide everything"
            ], category: "Security",
            description: "Controls where executed command names are hidden from logging.",
            helpText: "Admin commands still execute at every level — this only controls whether their names appear in the log output. Useful to keep admin activity private on shared hosting."),

        Text("AdminFileName",
            "Admin File Name", "serveradmin.xml", category: "Security",
            description: "File name for the admin list containing bans, whitelists, and permission levels.",
            helpText: "This file is stored in the UserData folder. Do not rename it unless you have a specific reason — the game expects 'serveradmin.xml' by default."),

        // Web Dashboard — replaced the old "Control Panel" (ControlPanelEnabled/
        // ControlPanelPort/ControlPanelPassword) back in Alpha 21. Those properties
        // are gone from the current schema and abort startup if written.
        Toggle("WebDashboardEnabled",
            "Enable Web Dashboard", "False", category: "Web & Dashboard",
            description: "Enables the modern web dashboard (available in newer server builds).",
            helpText: "The web dashboard replaces the older control panel in modern 7 Days to Die builds. Check your installed version to see which is supported."),

        Number("WebDashboardPort",
            "Web Dashboard Port", "8080", min: 1, max: 65535, unit: "port", category: "Web & Dashboard",
            description: "Port for the modern web dashboard.",
            advanced: true),

        Text("WebDashboardUrl",
            "Web Dashboard URL Override", "", category: "Web & Dashboard",
            description: "Custom URL for the web dashboard. Leave blank for automatic detection.",
            advanced: true),

        Toggle("EnableMapRendering",
            "Enable Map Rendering", "False", category: "Web & Dashboard",
            description: "Enables real-time map rendering in the web dashboard.",
            helpText: "Map rendering provides a live overhead view of the world in the dashboard. It has a performance cost — disable on low-spec servers.",
            advanced: true),

        // ── Telnet ────────────────────────────────────────────────────────────────
        Toggle("TelnetEnabled",
            "Enable Telnet", "False", category: "Remote Administration",
            description: "Enables Telnet remote access for server administration commands.",
            helpText: "Telnet provides a text-based admin console. If you expose this port externally, always set a strong password. Consider using an SSH tunnel instead of direct exposure."),

        Number("TelnetPort",
            "Telnet Port", "8081", min: 1, max: 65535, unit: "port", category: "Remote Administration",
            description: "Port for Telnet connections.",
            helpText: "Ensure this port does not conflict with the game port (default 26900) or web control panel (default 8080)."),

        Password("TelnetPassword",
            "Telnet Password", "", category: "Remote Administration",
            description: "Password for Telnet login. A blank password means no authentication — use only on private isolated networks.",
            helpText: "Always set a strong password if Telnet is reachable from the internet. Failed login attempts are rate-limited by TelnetFailedLoginLimit."),

        Number("TelnetFailedLoginLimit",
            "Max Failed Logins", "10", min: 0, max: 100, unit: "attempts", category: "Remote Administration",
            description: "Number of failed Telnet logins before the connecting IP is temporarily blocked.",
            helpText: "Set to 0 to disable rate limiting (not recommended on public servers). Typical values: 3–10."),

        Number("TelnetFailedLoginsBlocktime",
            "Login Block Duration", "10", min: 0, max: 3600, unit: "seconds", category: "Remote Administration",
            description: "How long (in seconds) a Telnet IP is blocked after exceeding the failed-login limit.",
            helpText: "Increase this value on public servers to slow brute-force attempts. A value of 60–300 seconds is recommended for internet-facing servers.",
            recommendedValue: "60–300 for public servers"),

        Toggle("TerminalWindowEnabled",
            "Terminal Window", "True", category: "Remote Administration",
            description: "Shows a terminal window for log output and command input (Windows only).",
            helpText: "The terminal window is the server's local console. Disable it when running the server as a background service."),

        // ── Data and Storage ──────────────────────────────────────────────────────
        Folder("UserDataFolder",
            "User Data Folder", "", category: "Data & Storage",
            description: "Custom directory for player data, saves, and logs. Leave blank to use '<InstallPath>/UserData'.",
            helpText: "Specifying an explicit path is recommended when running multiple servers — it ensures data isolation. The folder is passed via the -UserDataFolder launch flag, not via serverconfig.xml.",
            placeholder: "C:\\GameServers\\7dtd-server1\\UserData"),

        Number("MaxUncoveredMapChunksPerPlayer",
            "Max Map Chunks Per Player", "131072", min: 0, max: 1000000, unit: "chunks", category: "Data & Storage",
            description: "Maximum map chunks a single player can uncover. Lower values reduce server memory consumption.",
            helpText: "131072 chunks is the practical default. Reduce to 65536 or lower on memory-constrained servers. At 16 chunks per player coordinate, 131072 = ~512×512 chunk radius.",
            advanced: true),

        Number("MaxChunkAge",
            "Max Chunk Age", "-1", min: -1, max: 365, unit: "days", category: "Data & Storage",
            description: "Days before unvisited chunks are deleted from the save. -1 disables cleanup.",
            helpText: "Enabling cleanup reduces save file growth on active servers but removes player-built structures in unvisited areas. Use 30–90 days for long-running community servers.",
            recommendedValue: "-1 (off) for most servers",
            advanced: true),

        Number("SaveDataLimit",
            "Save Data Limit", "-1", min: -1, max: 100000, unit: "MB", category: "Data & Storage",
            description: "Maximum save folder size in MB. -1 disables the limit.",
            helpText: "When the limit is reached, older chunk data is pruned. Only relevant for very long-running servers with large worlds.",
            advanced: true),

        // ── World ─────────────────────────────────────────────────────────────────
        Dropdown("GameWorld", "Game World", "Navezgane",
            [
                "Navezgane:Navezgane (Hand-Crafted)",
                "RWG:Random World Generation"
            ], category: "World",
            description: "Which world to use. Navezgane is the official hand-crafted map; RWG generates a unique map from your seed.",
            helpText: "Navezgane has curated content and consistent quest locations. RWG provides endless variety but lacks hand-placed points of interest in some versions."),

        Text("WorldGenSeed",
            "World Gen Seed", "asdf", category: "World",
            description: "Seed string for Random World Generation. The same seed always produces the same map layout.",
            helpText: "Only used when Game World is set to RWG. Any string is valid. Share the seed with players so they can preview the map using third-party tools.",
            placeholder: "Enter any text — e.g. 'myseed2025'"),

        Dropdown("WorldGenSize", "World Gen Size", "6144",
            [
                "6144:Small (6144 m²)",
                "8192:Medium (8192 m²)",
                "10240:Large (10240 m²)"
            ], category: "World",
            description: "Size of the generated world. Larger worlds take more time to generate and use more disk space.",
            helpText: "6144 is recommended for most servers. 10240 provides much more space but can take 30+ minutes to generate on first launch and uses significantly more RAM.",
            recommendedValue: "6144 for most servers"),

        Text("GameName",
            "Save Game Name", "My Game", required: true, category: "World",
            description: "Name of the save folder on disk. Changing this on a running server creates a new save.",
            helpText: "The save is stored at UserDataFolder/Saves/GameWorld/GameName/. Changing this value on an existing server effectively starts a fresh world — the old save is not deleted but will not load until you change the name back.",
            recommendedValue: "Use a descriptive name; avoid special characters"),

        Dropdown("GameMode", "Game Mode", "GameModeSurvival",
            [
                "GameModeSurvival:Survival",
                "GameModeCreative:Creative"
            ], category: "World",
            description: "Survival mode enables all mechanics (hunger, thirst, zombie threat). Creative mode removes most survival restrictions.",
            helpText: "Creative mode is intended for building and testing. Most community servers run Survival."),

        // ── Gameplay (server-level rules that stayed outside SandboxCode in V3) ──
        Number("PlayerSafeZoneLevel",
            "Safe Zone Max Level", "5", min: 0, max: 300, unit: "level", category: "Gameplay",
            description: "Players at or below this level spawn inside a safe zone with no enemies.",
            helpText: "Protects brand-new players from being killed immediately after spawning."),

        Number("PlayerSafeZoneHours",
            "Safe Zone Duration", "5", min: 0, max: 100, unit: "world hours", category: "Gameplay",
            description: "How many in-game hours the new-player safe zone lasts."),

        Toggle("BuildCreate",
            "Cheat Mode (Build/Create)", "False", category: "Gameplay",
            description: "Enables cheat mode: free building and item creation for all players.",
            helpText: "Intended for creative/building servers. Leave off for survival play.",
            recommendedValue: "Disabled for survival servers"),

        Number("BedrollDeadZoneSize",
            "Bedroll Dead Zone Size", "15", min: 0, max: 200, unit: "blocks", category: "Gameplay",
            description: "Radius around a bedroll where zombies will not spawn and cleared sleeper volumes stay clear."),

        Number("BedrollExpiryTime",
            "Bedroll Expiry", "45", min: 0, max: 365, unit: "days", category: "Gameplay",
            description: "Real-world days a bedroll stays active after its owner was last online."),

        Dropdown("AllowSpawnNearFriend", "Spawn Near Friend", "2",
            [
                "0:Disabled",
                "1:Always allowed",
                "2:Only near friends in the forest biome"
            ], category: "Gameplay",
            description: "Whether players joining for the first time can spawn near a friend who is online."),

        Dropdown("CameraRestrictionMode", "Camera Restriction", "0",
            [
                "0:Free (first and third person)",
                "1:First person only",
                "2:Third person only"
            ], category: "Gameplay",
            description: "Restricts which camera modes players may use."),

        Dropdown("PlayerKillingMode", "PvP Mode", "3",
            [
                "0:No killing",
                "1:Kill allies only",
                "2:Kill strangers only",
                "3:Kill everyone"
            ], category: "Gameplay",
            description: "Who players are allowed to kill.",
            helpText: "0 makes the server fully PvE. 3 is open PvP. 1 and 2 gate killing by ally status."),

        Number("PartySharedKillRange",
            "Party Shared Kill Range", "100", min: 0, max: 10000, unit: "meters", category: "Gameplay",
            description: "Distance within which party members receive shared kill XP and quest kill credit."),

        // ── Land Claims ───────────────────────────────────────────────────────────
        Number("LandClaimCount",
            "Claims Per Player", "5", min: 0, max: 100, unit: "claims", category: "Land Claims",
            description: "Maximum allowed land claims per player."),

        Number("LandClaimSize",
            "Claim Size", "41", min: 1, max: 100, unit: "blocks", category: "Land Claims",
            description: "Size in blocks protected by a keystone."),

        Number("LandClaimDeadZone",
            "Claim Dead Zone", "30", min: 0, max: 200, unit: "blocks", category: "Land Claims",
            description: "Minimum distance between keystones of players who are not friends."),

        Number("LandClaimExpiryTime",
            "Claim Expiry", "7", min: 0, max: 365, unit: "days", category: "Land Claims",
            description: "Real-world days a player can be offline before their claims expire and lose protection."),

        Dropdown("LandClaimDecayMode", "Claim Decay Mode", "0",
            [
                "0:Slow (linear)",
                "1:Fast (exponential)",
                "2:None (full protection until expiry)"
            ], category: "Land Claims",
            description: "How claim protection decays while the owner is offline."),

        Number("LandClaimOnlineDurabilityModifier",
            "Online Durability Modifier", "4", min: 0, max: 256, unit: "×", category: "Land Claims",
            description: "Block hardness multiplier inside a claim while the owner is online. 0 = infinite (no damage possible).",
            recommendedValue: "4 (default)"),

        Number("LandClaimOfflineDurabilityModifier",
            "Offline Durability Modifier", "4", min: 0, max: 256, unit: "×", category: "Land Claims",
            description: "Block hardness multiplier inside a claim while the owner is offline. 0 = infinite (no damage possible).",
            recommendedValue: "4 (default)"),

        Number("LandClaimOfflineDelay",
            "Offline Delay", "0", min: 0, max: 1440, unit: "minutes", category: "Land Claims",
            description: "Minutes after logout before claim hardness transitions from the online to the offline modifier."),

        // ── Performance ───────────────────────────────────────────────────────────
        Number("MaxSpawnedZombies",
            "Max Spawned Zombies", "64", min: 1, max: 256, unit: "zombies", category: "Performance",
            description: "World-wide zombie cap, scaled per use case (×1.9 blood moons, ×2.1 sleepers).",
            helpText: "Changing this has a huge impact on performance. Raise only on strong hardware.",
            recommendedValue: "64 (default)"),

        Number("MaxSpawnedAnimals",
            "Max Spawned Animals", "50", min: 1, max: 256, unit: "animals", category: "Performance",
            description: "World-wide wildlife cap. Animals cost less CPU than zombies.",
            helpText: "Only worth raising on servers with many players spread across the map — biome spawning is per-area."),

        Number("ServerMaxAllowedViewDistance",
            "Max Client View Distance", "12", min: 6, max: 12, unit: "chunks", category: "Performance",
            description: "Maximum view distance a client may request (6–12). High impact on memory and performance.",
            recommendedValue: "12; lower to 8 on memory-constrained servers"),

        Number("MaxQueuedMeshLayers",
            "Max Queued Mesh Layers", "1000", min: 100, max: 10000, unit: "layers", category: "Performance",
            description: "Maximum chunk mesh layers queued during mesh generation. Lower uses less memory but slows chunk generation.",
            advanced: true),

        Toggle("DynamicMeshEnabled",
            "Dynamic Mesh", "True", category: "Performance",
            description: "Enables the Dynamic Mesh system (distant rendering of player-built structures)."),

        Toggle("DynamicMeshLandClaimOnly",
            "Dynamic Mesh in Claims Only", "True", category: "Performance",
            description: "Restricts Dynamic Mesh to player land-claim areas."),

        Number("DynamicMeshLandClaimBuffer",
            "Dynamic Mesh Claim Buffer", "3", min: 0, max: 32, unit: "chunks", category: "Performance",
            description: "Chunk radius around land claims covered by Dynamic Mesh.",
            advanced: true),

        Number("DynamicMeshMaxItemCache",
            "Dynamic Mesh Item Cache", "3", min: 1, max: 100, unit: "items", category: "Performance",
            description: "How many Dynamic Mesh items are processed concurrently. Higher values use more RAM.",
            advanced: true),

        // ── Twitch Integration ────────────────────────────────────────────────────
        Number("TwitchServerPermission",
            "Twitch Permission Level", "90", min: 0, max: 1000, unit: "level", category: "Twitch Integration",
            description: "Required permission level to use Twitch integration on the server."),

        Toggle("TwitchBloodMoonAllowed",
            "Twitch Actions During Blood Moon", "False", category: "Twitch Integration",
            description: "Allows Twitch actions during a blood moon. Extra spawned zombies can cause server lag.",
            recommendedValue: "Disabled (default)"),

        // ── V3.0 Sandbox ──────────────────────────────────────────────────────────
        Text("SandboxCode",
            "Sandbox Code", "", category: "Sandbox (V3)",
            description: "V3 gameplay settings encoded as a compact string (difficulty, zombie speed, loot, etc.). Paste a code to apply a configuration.",
            helpText: "In V3, gameplay parameters (game difficulty, zombie speed, loot abundance, etc.) are bundled into a single 'Sandbox Code' string. Generate a code from the game's main menu or using community tools, then paste it here. Leave blank for default V3 settings. The encoding format is proprietary and not publicly documented — the manager stores it as-is without modification.",
            placeholder: "Paste a V3 Sandbox Code here"),

        // ── Installation ──────────────────────────────────────────────────────────
        Dropdown("SteamBranch", "Steam Branch", "stable",
            [
                "stable:Stable (Public Release)",
                "latest_experimental:Experimental",
                "latest_experimental_fallback:Experimental Fallback",
                "custom:Custom Branch"
            ], category: "Installation",
            description: "Steam update branch. Use 'stable' for production servers.",
            helpText: "Experimental branches receive updates before the stable release and may contain bugs. Only use experimental if you specifically want to test pre-release features.",
            recommendedValue: "stable",
            advanced: true),

        Text("CustomSteamBranch",
            "Custom Branch Name", "", category: "Installation",
            description: "Branch name when 'Custom Branch' is selected above.",
            helpText: "Only fill this in if you selected 'Custom Branch'. Contact your server host or refer to Steam documentation for valid branch names.",
            placeholder: "Enter the exact branch name",
            advanced: true),
    ];

    public override ServerLaunchCommand BuildStartCommand(ServerProfile profile)
    {
        var installPath = profile.InstallPath;
        var executablePath = !string.IsNullOrWhiteSpace(profile.ExecutablePath)
            ? profile.ExecutablePath
            : Path.Combine(installPath, ExecutableRelativePath);

        var configPath = Path.Combine(installPath, "serverconfig.xml");

        var userDataFolder = Get(profile, "UserDataFolder");
        if (string.IsNullOrWhiteSpace(userDataFolder))
            userDataFolder = Path.Combine(installPath, "UserData");

        var logPath = Path.Combine(installPath, "logs",
            $"server_{DateTime.UtcNow:yyyyMMdd_HHmmss}.log");

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
            args.Add(profile.LaunchArgs.Trim());

        return new ServerLaunchCommand
        {
            ExecutablePath = executablePath,
            WorkingDirectory = installPath,
            Arguments = string.Join(' ', args)
        };
    }

    private static string Get(ServerProfile profile, string key)
        => profile.Settings.TryGetValue(key, out var v) ? v : string.Empty;

    // ── Helper factories ──────────────────────────────────────────────────────────

    protected static ServerSettingDefinition Text(
        string key, string displayName, string defaultValue,
        bool required = false, string? category = null, bool advanced = false,
        string? description = null, string? helpText = null,
        string? placeholder = null, string? recommendedValue = null)
        => new()
        {
            SettingKey = key, DisplayName = displayName, DefaultValue = defaultValue,
            ControlType = SettingControlType.TextBox, IsRequired = required,
            Category = category, IsAdvanced = advanced, RequiresRestart = true,
            Description = description, HelpText = helpText,
            Placeholder = placeholder, RecommendedValue = recommendedValue
        };

    protected static ServerSettingDefinition Password(
        string key, string displayName, string defaultValue,
        bool required = false, string? category = null,
        string? description = null, string? helpText = null)
        => new()
        {
            SettingKey = key, DisplayName = displayName, DefaultValue = defaultValue,
            ControlType = SettingControlType.PasswordField, IsRequired = required,
            Category = category, RequiresRestart = true,
            Description = description, HelpText = helpText
        };

    protected static ServerSettingDefinition Number(
        string key, string displayName, string defaultValue,
        int min, int max, string? unit = null, string? category = null, bool advanced = false,
        string? description = null, string? helpText = null, string? recommendedValue = null)
        => new()
        {
            SettingKey = key, DisplayName = displayName, DefaultValue = defaultValue,
            ControlType = SettingControlType.NumberBox, MinValue = min, MaxValue = max,
            Unit = unit, Category = category, IsAdvanced = advanced, RequiresRestart = true,
            Description = description, HelpText = helpText, RecommendedValue = recommendedValue
        };

    protected static ServerSettingDefinition Toggle(
        string key, string displayName, string defaultValue,
        string? category = null, bool advanced = false,
        string? description = null, string? helpText = null, string? recommendedValue = null)
        => new()
        {
            SettingKey = key, DisplayName = displayName, DefaultValue = defaultValue,
            ControlType = SettingControlType.Toggle, Category = category,
            IsAdvanced = advanced, RequiresRestart = true,
            Description = description, HelpText = helpText, RecommendedValue = recommendedValue
        };

    protected static ServerSettingDefinition Dropdown(
        string key, string displayName, string defaultValue,
        string[] options, string? category = null, bool advanced = false,
        string? description = null, string? helpText = null, string? recommendedValue = null)
        => new()
        {
            SettingKey = key, DisplayName = displayName, DefaultValue = defaultValue,
            ControlType = SettingControlType.Dropdown,
            Options = new List<string>(options),
            Category = category, IsAdvanced = advanced, RequiresRestart = true,
            Description = description, HelpText = helpText, RecommendedValue = recommendedValue
        };

    protected static ServerSettingDefinition Folder(
        string key, string displayName, string defaultValue,
        bool required = false, string? category = null,
        string? description = null, string? helpText = null, string? placeholder = null)
        => new()
        {
            SettingKey = key, DisplayName = displayName, DefaultValue = defaultValue,
            ControlType = SettingControlType.FolderPicker, IsRequired = required,
            Category = category, RequiresRestart = true,
            Description = description, HelpText = helpText, Placeholder = placeholder
        };
}
