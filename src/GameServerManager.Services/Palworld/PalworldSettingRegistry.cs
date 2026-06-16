using GameServerManager.Core.Models;

namespace GameServerManager.Services.Palworld;

public static class PalworldSettingRegistry
{
    private static readonly IReadOnlyList<PalworldSettingDefinition> Definitions = Build();
    private static readonly HashSet<string> KnownKeySet;

    static PalworldSettingRegistry()
    {
        KnownKeySet = new HashSet<string>(
            Definitions.Select(d => d.Key),
            StringComparer.OrdinalIgnoreCase);
    }

    public static IReadOnlyList<PalworldSettingDefinition> All => Definitions;
    public static IReadOnlyCollection<string> KnownKeys => KnownKeySet;

    public static PalworldSettingDefinition? Find(string key)
        => Definitions.FirstOrDefault(d => d.Key.Equals(key, StringComparison.OrdinalIgnoreCase));

    public static bool IsKnown(string key) => KnownKeySet.Contains(key);

    public static IReadOnlyList<PalworldSettingDefinition> ForCategory(string category)
        => Definitions.Where(d => d.Category.Equals(category, StringComparison.OrdinalIgnoreCase)).ToArray();

    // ── Builder helpers ────────────────────────────────────────────────────────

    private static PalworldSettingDefinition Opt(
        string key, string displayName, string description, string category,
        PalworldSettingDataType type, string defaultValue,
        decimal? min = null, decimal? max = null,
        string[]? allowed = null,
        bool advanced = false, bool dangerous = false, bool perfImpact = false,
        string warning = "", string wiki = "")
        => new()
        {
            Key = key,
            DisplayName = displayName,
            Description = description,
            Category = category,
            FileLocation = PalworldSettingLocation.OptionSettings,
            DataType = type,
            DefaultValue = defaultValue,
            Min = min,
            Max = max,
            AllowedValues = allowed ?? Array.Empty<string>(),
            RestartRequired = true,
            AdvancedSetting = advanced,
            DangerousSetting = dangerous,
            PerformanceImpact = perfImpact,
            WarningText = warning,
            WikiReference = wiki
        };

    private static PalworldSettingDefinition Str(string key, string name, string desc, string cat,
        string def = "", bool pw = false, bool adv = false, bool danger = false, string warn = "")
        => Opt(key, name, desc, cat,
            pw ? PalworldSettingDataType.Password : PalworldSettingDataType.String,
            def, advanced: adv, dangerous: danger, warning: warn);

    private static PalworldSettingDefinition Bool(string key, string name, string desc, string cat,
        bool def = false, bool adv = false, bool danger = false, string warn = "")
        => Opt(key, name, desc, cat, PalworldSettingDataType.Boolean, def ? "True" : "False",
            advanced: adv, dangerous: danger, warning: warn);

    private static PalworldSettingDefinition Dec(string key, string name, string desc, string cat,
        decimal def = 1m, decimal? min = null, decimal? max = null, bool adv = false,
        bool perf = false, string warn = "")
        => Opt(key, name, desc, cat, PalworldSettingDataType.Decimal,
            def.ToString("0.000000", System.Globalization.CultureInfo.InvariantCulture),
            min, max, advanced: adv, perfImpact: perf, warning: warn);

    private static PalworldSettingDefinition Int(string key, string name, string desc, string cat,
        int def = 0, int? min = null, int? max = null, bool adv = false, bool perf = false, string warn = "")
        => Opt(key, name, desc, cat, PalworldSettingDataType.Integer, def.ToString(),
            min, max, advanced: adv, perfImpact: perf, warning: warn);

    private static PalworldSettingDefinition Enum(string key, string name, string desc, string cat,
        string def, string[] opts, bool adv = false, bool danger = false, string warn = "")
        => Opt(key, name, desc, cat, PalworldSettingDataType.Enum, def,
            allowed: opts, advanced: adv, dangerous: danger, warning: warn);

    private static PalworldSettingDefinition SArr(string key, string name, string desc, string cat,
        string def = "(\"Steam\",\"Xbox\")", bool adv = false, string warn = "")
        => Opt(key, name, desc, cat, PalworldSettingDataType.StringArray, def,
            advanced: adv, warning: warn);

    // ── Definitions ────────────────────────────────────────────────────────────

    private static IReadOnlyList<PalworldSettingDefinition> Build()
    {
        var s = new List<PalworldSettingDefinition>
        {
            // ── Server Identity ──────────────────────────────────────────────────
            Str("ServerName",         "Server Name",        "Public display name of the server.", "Server Identity", "Default Palworld Server"),
            Str("ServerDescription",  "Server Description", "Short description shown in the server browser.", "Server Identity"),
            Str("ServerPassword",     "Server Password",    "Password players must enter to join. Leave empty for public server.", "Server Identity", pw: true),
            Str("AdminPassword",      "Admin Password",     "Password required for admin commands and RCON. Must be set before admin features work.", "Admin / Passwords",
                pw: true, danger: false, warn: "AdminPassword is required before in-game admin commands and REST/RCON actions will work."),
            Int("ServerPlayerMaxNum", "Max Players",        "Maximum number of simultaneous players.", "Server Identity", 32, 1, 32),
            Int("CoopPlayerMaxNum",   "Co-op Max Players",  "Maximum players in a co-op session.", "Server Identity", 4, 1, 32, adv: true),
            Bool("bShowPlayerList",   "Show Player List",   "Allow players to see the full online player list.", "Server Identity"),
            Bool("bIsShowJoinLeftMessage", "Show Join/Leave Messages", "Display chat messages when players join or leave.", "Server Identity"),
            Int("ChatPostLimitPerMinute", "Chat Rate Limit", "Maximum chat messages a player can send per minute.", "Server Identity", 0, 0, 60, adv: true),
            Enum("LogFormatType",     "Log Format",         "Server log file format.", "Server Identity",
                "Text", new[] { "Text", "Json" }, adv: true),

            // ── Network / Ports ──────────────────────────────────────────────────
            Int("PublicPort",         "Public Port",        "Port announced to the Steam master server. Does NOT change the actual listening port — use -port launch argument for that.", "Network / Ports", 8211, 1, 65535,
                warn: "This does not change which port the server listens on. Use -port= in launch arguments to change the listening port."),
            Str("PublicIP",           "Public IP",          "Override IP announced to Steam. Leave empty to auto-detect.", "Network / Ports"),
            Bool("RCONEnabled",       "Enable RCON",        "Allow remote console connections.", "RCON"),
            Int("RCONPort",           "RCON Port",          "TCP port for RCON connections.", "RCON", 25575, 1, 65535),
            Bool("RESTAPIEnabled",    "Enable REST API",    "Enable the Palworld REST API for server management.", "REST API",
                warn: "The Palworld REST API should not be exposed directly to the internet. Use localhost or LAN access only."),
            Int("RESTAPIPort",        "REST API Port",      "HTTP port for the REST API.", "REST API", 8212, 1, 65535),
            Str("Region",             "Region",             "Server region tag for filtering.", "Network / Ports", adv: true),
            Bool("bUseAuth",          "Use Auth",           "Enable Steam/Xbox authentication. Disable only for LAN testing.", "Network / Ports", true, adv: true,
                warn: "Disabling authentication allows anyone to connect without a valid Steam/Xbox session."),
            Str("BanListURL",         "Ban List URL",       "URL of the ban list fetched at startup.", "Network / Ports",
                "https://api.palworldgame.com/api/banlist.txt", adv: true),

            // ── Crossplay ────────────────────────────────────────────────────────
            SArr("CrossplayPlatforms","Crossplay Platforms","Platforms allowed to connect. Example: (\"Steam\",\"Xbox\")", "Crossplay",
                "(\"Steam\",\"Xbox\")",
                warn: "AllowConnectPlatform is deprecated. Use CrossplayPlatforms instead."),
            Enum("AllowConnectPlatform", "Allow Connect Platform (deprecated)", "Deprecated — use CrossplayPlatforms instead.", "Crossplay",
                "Steam", new[] { "Steam", "Xbox", "PSN" }, adv: true,
                warn: "Deprecated since Palworld update. Use CrossplayPlatforms instead."),

            // ── World Rates ──────────────────────────────────────────────────────
            Dec("DayTimeSpeedRate",   "Day Speed",          "Multiplier for daytime speed. Higher = shorter days.", "World Rates", 1m, 0.1m, 20m),
            Dec("NightTimeSpeedRate", "Night Speed",        "Multiplier for nighttime speed. Higher = shorter nights.", "World Rates", 1m, 0.1m, 20m),
            Dec("ExpRate",            "XP Rate",            "Experience point gain multiplier.", "World Rates", 1m, 0.1m, 20m),
            Dec("DropItemMaxNum",     "Max Drop Items",     "Maximum number of dropped items on the ground.", "World Rates", 3000, 100, 10000, perf: true),
            Dec("DropItemAliveMaxHours","Drop Item Lifetime","How many hours dropped items remain on the ground.", "World Rates", 1m, 0.1m, 72m),
            Dec("CollectionDropRate", "Gather Drop Rate",   "Multiplier for resource drops from gathering.", "World Rates", 1m, 0.1m, 10m),
            Dec("CollectionObjectHpRate", "Gather Object HP","HP multiplier for gathering objects.", "World Rates", 1m, 0.1m, 10m),
            Dec("CollectionObjectRespawnSpeedRate", "Gather Respawn Speed", "Respawn speed for gathered objects. Higher = faster.", "World Rates", 1m, 0.1m, 10m),
            Dec("EnemyDropItemRate",  "Enemy Drop Rate",    "Multiplier for items dropped by enemies.", "World Rates", 1m, 0.1m, 10m),
            Int("SupplyDropSpan",     "Supply Drop Interval", "Minutes between supply drops.", "World Rates", 180, 10, 1440),

            // ── Player Settings ──────────────────────────────────────────────────
            Dec("PlayerDamageRateAttack",  "Player Damage",          "Damage dealt by players.", "Player Settings", 1m, 0.1m, 10m),
            Dec("PlayerDamageRateDefense", "Player Defense",         "Damage taken by players.", "Player Settings", 1m, 0.1m, 10m),
            Dec("PlayerStaminaDecreaceRate", "Player Stamina Drain", "How fast player stamina decreases.", "Player Settings", 1m, 0.1m, 10m),
            Dec("PlayerAutoHPRegeneRate",  "Player HP Regen",        "Player HP regeneration rate.", "Player Settings", 1m, 0m, 10m),
            Dec("PlayerAutoHpRegeneRateInSleep", "Player HP Regen (Sleep)", "HP regeneration rate while sleeping.", "Player Settings", 1m, 0m, 10m),
            Dec("PlayerStomachDecreaceRate", "Player Hunger Drain",  "How fast player hunger decreases.", "Player Settings", 1m, 0.1m, 10m),
            Dec("ItemWeightRate",     "Item Weight Rate",    "Item weight multiplier. Lower = carry more.", "Player Settings", 1m, 0.1m, 10m),
            Bool("bAllowEnhanceStat_Attack",    "Allow Enhance: Attack",    "Allow players to enhance attack stat.", "Player Settings", false, adv: true),
            Bool("bAllowEnhanceStat_Health",    "Allow Enhance: Health",    "Allow players to enhance health stat.", "Player Settings", false, adv: true),
            Bool("bAllowEnhanceStat_Stamina",   "Allow Enhance: Stamina",   "Allow players to enhance stamina stat.", "Player Settings", false, adv: true),
            Bool("bAllowEnhanceStat_Weight",    "Allow Enhance: Weight",    "Allow players to enhance carry weight stat.", "Player Settings", false, adv: true),
            Bool("bAllowEnhanceStat_WorkSpeed", "Allow Enhance: Work Speed","Allow players to enhance work speed stat.", "Player Settings", false, adv: true),

            // ── Pal Settings ─────────────────────────────────────────────────────
            Dec("PalCaptureRate",     "Pal Capture Rate",   "Multiplier for Pal capture chance.", "Pal Settings", 1m, 0.1m, 10m),
            Dec("PalSpawnNumRate",    "Pal Spawn Rate",     "Multiplier for how many Pals spawn. High values impact performance.", "Pal Settings", 1m, 0.1m, 10m, perf: true),
            Dec("PalDamageRateAttack","Pal Damage",         "Damage dealt by Pals.", "Pal Settings", 1m, 0.1m, 10m),
            Dec("PalDamageRateDefense","Pal Defense",       "Damage taken by Pals.", "Pal Settings", 1m, 0.1m, 10m),
            Dec("PalStaminaDecreaceRate","Pal Stamina Drain","How fast Pal stamina decreases.", "Pal Settings", 1m, 0.1m, 10m),
            Dec("PalAutoHPRegeneRate","Pal HP Regen",       "Pal HP regeneration rate.", "Pal Settings", 1m, 0m, 10m),
            Dec("PalAutoHpRegeneRateInSleep","Pal HP Regen (Sleep)", "Pal HP regen rate while in Palbox.", "Pal Settings", 1m, 0m, 10m),
            Dec("PalEggDefaultHatchingTime","Egg Hatch Time","Default egg hatching time in hours.", "Pal Settings", 72m, 0m, 720m),
            Dec("WorkSpeedRate",      "Work Speed Rate",    "Multiplier for Pal work speed.", "Pal Settings", 1m, 0.1m, 10m),
            Bool("bEnableInvaderEnemy","Enable Raider Pals","Enable raider Pal events.", "Pal Settings", true),
            Bool("bPalLost",          "Pal Lost on Death",  "Pals are permanently lost when they die in the world.", "Pal Settings", false, danger: true,
                warn: "Enabling this means Pals that die outside of Palbox are gone permanently."),

            // ── Base / Guild Settings ─────────────────────────────────────────────
            Int("BaseCampMaxNum",     "Max Base Camps",     "Maximum number of base camps in the world.", "Base / Guild Settings", 128, 1, 512),
            Int("BaseCampWorkerMaxNum","Max Base Workers",  "Maximum workers per base camp. Higher values increase server load.", "Base / Guild Settings", 15, 1, 20,
                perf: true, warn: "High values significantly increase server CPU load."),
            Int("BaseCampMaxNumInGuild","Max Bases per Guild","Maximum number of base camps a single guild can own.", "Base / Guild Settings", 4, 1, 10,
                perf: true, warn: "High values increase server load."),
            Int("GuildPlayerMaxNum",  "Max Guild Members",  "Maximum players allowed in a single guild.", "Base / Guild Settings", 20, 1, 100),
            Int("GuildRejoinCooldownMinutes", "Guild Rejoin Cooldown", "Minutes before a player can rejoin a guild after leaving.", "Base / Guild Settings", 0, 0, 10080, adv: true),
            Bool("bAutoResetGuildNoOnlinePlayers","Auto Reset Inactive Guilds","Reset guilds with no online players after timeout.", "Base / Guild Settings", false, adv: true),
            Dec("AutoResetGuildTimeNoOnlinePlayers","Guild Auto-Reset Time","Hours of inactivity before auto-reset triggers.", "Base / Guild Settings", 72m, 1m, 720m, adv: true),
            Bool("bAllowGlobalPalboxExport","Allow Global Palbox Export","Allow players to export Pals from the global Palbox.", "Base / Guild Settings", false, adv: true),
            Bool("bAllowGlobalPalboxImport","Allow Global Palbox Import","Allow players to import Pals into the global Palbox.", "Base / Guild Settings", false, adv: true),

            // ── Building / Decay ─────────────────────────────────────────────────
            Dec("BuildObjectDamageRate",           "Build Damage Rate",     "Damage multiplier for structures.", "Building / Decay", 1m, 0m, 10m),
            Dec("BuildObjectDeteriorationDamageRate","Structure Decay Rate","How fast structures decay.", "Building / Decay", 1m, 0m, 10m),
            Int("MaxBuildingLimitNum","Max Structures",     "Maximum number of structures in the world (0 = unlimited).", "Building / Decay", 0, 0, 1000000, adv: true, perf: true),
            Bool("bBuildAreaLimit",  "Enforce Build Area Limit","Restrict building to specific areas.", "Building / Decay", false, adv: true),
            Int("BlockRespawnTime",   "Block Respawn Time", "How fast blocks respawn after being destroyed (seconds).", "Building / Decay", 0, 0, 3600, adv: true),
            Bool("bInvisibleOtherGuildBaseCampAreaFX","Hide Other Guild Camp FX","Hide other guild base camp visual effects.", "Building / Decay", false, adv: true),
            Bool("bDisplayPvPItemNumOnWorldMap_BaseCamp","Show PvP Items on Map (Base)","Show PvP item count on world map for bases.", "Building / Decay", false, adv: true),
            Bool("bDisplayPvPItemNumOnWorldMap_Player","Show PvP Items on Map (Player)","Show PvP item count on world map for players.", "Building / Decay", false, adv: true),

            // ── PvP Settings ──────────────────────────────────────────────────────
            Bool("bIsPvP",            "PvP Mode",           "Enable player vs. player combat.", "PvP Settings"),
            Bool("bEnablePlayerToPlayerDamage","Player Damage (PvP)","Allow players to damage each other.", "PvP Settings"),
            Bool("bEnableFriendlyFire","Friendly Fire",     "Allow guild members to damage each other.", "PvP Settings", false, danger: true,
                warn: "Enabling friendly fire can lead to grief between guild members."),
            Bool("bEnableDefenseOtherGuildPlayer","Defend Against Other Guilds","Allow attacking players from other guilds.", "PvP Settings"),
            Bool("bCanPickupOtherGuildDeathPenaltyDrop","Pick Up Other Guild Death Drops","Allow picking up items dropped by other guild players on death.", "PvP Settings"),
            Int("AdditionalDropItemNumWhenPlayerKillingInPvPMode","PvP Kill Drop Count","Extra item drops when killing a player in PvP.", "PvP Settings", 0, 0, 100),
            Bool("bAdditionalDropItemWhenPlayerKillingInPvPMode","Enable PvP Kill Drops","Drop extra items when killing a player in PvP.", "PvP Settings"),
            Bool("AdditionalDropItemWhenPlayerKillingInPvPMode","PvP Kill Drop (legacy)","Legacy duplicate of PvP kill drop setting.", "PvP Settings", false, adv: true),

            // ── Death / Penalty ───────────────────────────────────────────────────
            Enum("DeathPenalty",      "Death Penalty",      "What is dropped when a player dies.", "Death / Penalty",
                "All", new[] { "None", "Item", "ItemAndEquipment", "All" },
                warn: "DeathPenalty=All drops all items AND Pals. Use None for casual servers."),
            Int("RespawnPenaltyDurationThreshold","Respawn Penalty Threshold","Deaths before respawn penalty kicks in.", "Death / Penalty", 0, 0, 100, adv: true),
            Dec("RespawnPenaltyTimeScale","Respawn Penalty Time Scale","Multiplier for the respawn penalty duration.", "Death / Penalty", 1m, 0m, 10m, adv: true),
            Dec("EquipmentDurabilityDamageRate","Equipment Durability Loss","Multiplier for equipment durability damage.", "Death / Penalty", 1m, 0m, 10m),
            Dec("ItemCorruptionMultiplier","Item Corruption Rate","Multiplier for item corruption on death.", "Death / Penalty", 0m, 0m, 10m, adv: true),

            // ── Features ─────────────────────────────────────────────────────────
            Bool("bEnableFastTravel",    "Enable Fast Travel","Allow players to use fast travel.", "Features", true),
            Bool("bEnableFastTravelOnlyBaseCamp","Fast Travel to Base Only","Restrict fast travel to base camp waypoints only.", "Features", false),
            Bool("bIsStartLocationSelectByMap","Map Start Location Select","Let players choose their spawn location on the map.", "Features", true),
            Bool("bExistPlayerAfterLogout","Player Body Persists","Player character stays in the world after logging out.", "Features", false),
            Bool("bEnableNonLoginPenalty","Offline Penalty",  "Apply penalties for players who haven't logged in.", "Features", true),
            Bool("bIsUseBackupSaveData","Use Built-in Backups","Enable the server's built-in save data backups (increases disk usage).", "Features", true,
                warn: "Enabling built-in backups increases disk load. Monitor disk space."),
            Bool("bActiveUNKO",         "Enable UNKO",       "Enable UNKO feature.", "Features", false, adv: true),
            Bool("bCharacterRecreateInHardcore","Hardcore Respawn","Force character recreation on death in hardcore mode.", "Features", false, danger: true,
                warn: "Enabling this in a non-hardcore server will permanently delete player characters on death."),

            // ── Hardcore ──────────────────────────────────────────────────────────
            Bool("bHardcore",         "Hardcore Mode",      "Enable hardcore mode (character lost on death).", "Features", false, danger: true,
                warn: "Hardcore mode permanently deletes player characters on death. Not recommended for casual servers."),

            // ── Performance ───────────────────────────────────────────────────────
            Int("ServerReplicatePawnCullDistance", "Pawn Cull Distance",
                "Distance at which pawns are culled from replication. Higher values increase network traffic.", "Performance",
                15000, 1000, 100000, adv: true, perf: true),
            Dec("ItemContainerForceMarkDirtyInterval", "Container Dirty Interval",
                "How often item container changes are synced (seconds). Lower = more responsive but more load.", "Performance",
                1m, 0.01m, 10m, adv: true, perf: true),

            // ── Randomizer ────────────────────────────────────────────────────────
            Enum("RandomizerType",    "Randomizer Mode",    "Enable and set the randomizer mode.", "Randomizer",
                "None", new[] { "None", "Random", "Preset" }),
            Str("RandomizerSeed",     "Randomizer Seed",    "Seed value for the randomizer mode.", "Randomizer", adv: true),
            Bool("bIsRandomizerPalLevelRandom","Randomize Pal Levels","Randomize Pal levels in randomizer mode.", "Randomizer", false, adv: true),

            // ── Technology Restrictions ──────────────────────────────────────────
            Str("DenyTechnologyList", "Deny Technology List","Comma-separated list of technology IDs to block. Leave empty to allow all.", "Technology Restrictions",
                adv: true, warn: "DenyTechnologyList requires valid Palworld technology IDs. Invalid IDs are silently ignored."),
        };

        return s;
    }
}
