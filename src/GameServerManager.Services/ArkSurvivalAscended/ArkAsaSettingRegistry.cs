using GameServerManager.Core.Models;

namespace GameServerManager.Services.ArkSurvivalAscended;

public static class ArkAsaSettingRegistry
{
    public const string ServerSettingsSection = "ServerSettings";
    public const string GameModeSection = "/script/shootergame.shootergamemode";

    private static readonly IReadOnlyList<ArkSettingDefinition> Definitions = BuildDefinitions();

    public static IReadOnlyList<ArkSettingDefinition> All => Definitions;

    public static ArkSettingDefinition? Find(string key)
    {
        return Definitions.FirstOrDefault(setting => setting.Key.Equals(key, StringComparison.OrdinalIgnoreCase));
    }

    public static IReadOnlyList<ArkSettingDefinition> ForLocation(ArkSettingFileLocation location)
    {
        return Definitions.Where(setting => setting.FileLocation == location).ToArray();
    }

    private static IReadOnlyList<ArkSettingDefinition> BuildDefinitions()
    {
        var settings = new List<ArkSettingDefinition>();

        AddLaunch(settings, "MapName", "Map", "Internal map launch name, such as TheIsland_WP.", "Maps", ArkSettingDataType.String, "TheIsland_WP");
        AddLaunch(settings, "SessionName", "Session Name", "Public server name shown in the ARK browser.", "Server Identity", ArkSettingDataType.String, "ARK ASA Server");
        AddLaunch(settings, "ServerPassword", "Server Password", "Optional password players must enter to join.", "Admin / Passwords", ArkSettingDataType.Password, string.Empty, advanced: false);
        AddLaunch(settings, "ServerAdminPassword", "Admin Password", "Password used for admin/RCON commands.", "Admin / Passwords", ArkSettingDataType.Password, string.Empty, warning: "Do not share this password.");
        AddLaunch(settings, "Port", "Game Port", "UDP game traffic port.", "Network / Ports", ArkSettingDataType.Integer, "7777", 1, 65535);
        AddLaunch(settings, "QueryPort", "Query Port", "UDP server query port.", "Network / Ports", ArkSettingDataType.Integer, "27015", 1, 65535);
        AddLaunch(settings, "RCONPort", "RCON Port", "TCP remote console port.", "RCON / Console", ArkSettingDataType.Integer, "27020", 1, 65535);
        AddLaunch(settings, "RCONEnabled", "Enable RCON", "Allow remote console connections.", "RCON / Console", ArkSettingDataType.Boolean, "True");
        AddLaunch(settings, "MaxPlayers", "Max Players", "Maximum player slots.", "Admin / Passwords", ArkSettingDataType.Integer, "70", 1, 200);
        AddLaunch(settings, "AltSaveDirectoryName", "Alt Save Directory", "Per-instance save folder name.", "Maps", ArkSettingDataType.String, string.Empty);
        AddLaunch(settings, "ClusterID", "Cluster ID", "Shared ID used by clustered ARK maps.", "Cluster", ArkSettingDataType.String, string.Empty, advanced: true);
        AddLaunch(settings, "ClusterDirOverride", "Cluster Directory Override", "Shared cluster transfer directory.", "Cluster", ArkSettingDataType.String, string.Empty, advanced: true);
        AddLaunch(settings, "NoTransferFromFiltering", "No Transfer From Filtering", "Allow transfers without source filtering.", "Cluster", ArkSettingDataType.Boolean, "False", advanced: true);
        AddLaunch(settings, "mods", "Mod IDs", "Comma-separated CurseForge/ASA mod IDs.", "Mods", ArkSettingDataType.StringList, string.Empty);
        AddLaunch(settings, "NoBattlEye", "Disable BattlEye", "Adds -NoBattlEye when enabled.", "Startup", ArkSettingDataType.Boolean, "False", warning: "Disabling anti-cheat can affect server trust.");
        AddLaunch(settings, "log", "Console Log", "Adds -log for live console output.", "Startup", ArkSettingDataType.Boolean, "True");

        AddGameUserSettings(settings, new (string Key, string Name, ArkSettingDataType Type, string Default, string Category, decimal? Min, decimal? Max)[]
        {
            ("SessionName", "Session Name", ArkSettingDataType.String, "ARK ASA Server", "Server Identity", null, null),
            ("ServerPassword", "Server Password", ArkSettingDataType.Password, "", "Server Identity", null, null),
            ("ServerAdminPassword", "Server Admin Password", ArkSettingDataType.Password, "", "Server Identity", null, null),
            ("SpectatorPassword", "Spectator Password", ArkSettingDataType.Password, "", "Server Identity", null, null),
            ("AdminLogging", "Admin Logging", ArkSettingDataType.Boolean, "False", "Server Identity", null, null),
            ("ServerPVE", "PvE Server", ArkSettingDataType.Boolean, "True", "PvE / PvP Rules", null, null),
            ("ShowMapPlayerLocation", "Show Map Player Location", ArkSettingDataType.Boolean, "False", "Player Settings", null, null),
            ("ServerCrosshair", "Server Crosshair", ArkSettingDataType.Boolean, "True", "Player Settings", null, null),
            ("AllowThirdPersonPlayer", "Allow Third Person", ArkSettingDataType.Boolean, "True", "Player Settings", null, null),
            ("EnablePVPGamma", "Enable PvP Gamma", ArkSettingDataType.Boolean, "False", "PvE / PvP Rules", null, null),
            ("DisablePvEGamma", "Disable PvE Gamma", ArkSettingDataType.Boolean, "False", "PvE / PvP Rules", null, null),
            ("AllowHitMarkers", "Allow Hit Markers", ArkSettingDataType.Boolean, "True", "Player Settings", null, null),
            ("AllowFlyerCarryPvE", "Allow Flyer Carry PvE", ArkSettingDataType.Boolean, "False", "Dino Settings", null, null),
            ("AllowCaveBuildingPvE", "Allow Cave Building PvE", ArkSettingDataType.Boolean, "False", "Structures", null, null),
            ("XPMultiplier", "XP Multiplier", ArkSettingDataType.Decimal, "1.0", "Rates", 0m, 100m),
            ("TamingSpeedMultiplier", "Taming Speed", ArkSettingDataType.Decimal, "1.0", "Rates", 0m, 1000m),
            ("HarvestAmountMultiplier", "Harvest Amount", ArkSettingDataType.Decimal, "1.0", "Harvesting / Resources", 0m, 1000m),
            ("HarvestHealthMultiplier", "Harvest Health", ArkSettingDataType.Decimal, "1.0", "Harvesting / Resources", 0m, 1000m),
            ("ResourcesRespawnPeriodMultiplier", "Resource Respawn Period", ArkSettingDataType.Decimal, "1.0", "Harvesting / Resources", 0m, 1000m),
            ("PlayerDamageMultiplier", "Player Damage", ArkSettingDataType.Decimal, "1.0", "Rates", 0m, 100m),
            ("PlayerResistanceMultiplier", "Player Resistance", ArkSettingDataType.Decimal, "1.0", "Rates", 0m, 100m),
            ("DinoDamageMultiplier", "Dino Damage", ArkSettingDataType.Decimal, "1.0", "Dino Settings", 0m, 100m),
            ("DinoResistanceMultiplier", "Dino Resistance", ArkSettingDataType.Decimal, "1.0", "Dino Settings", 0m, 100m),
            ("StructureDamageMultiplier", "Structure Damage", ArkSettingDataType.Decimal, "1.0", "Structures", 0m, 100m),
            ("StructureResistanceMultiplier", "Structure Resistance", ArkSettingDataType.Decimal, "1.0", "Structures", 0m, 100m),
            ("PlayerCharacterWaterDrainMultiplier", "Player Water Drain", ArkSettingDataType.Decimal, "1.0", "Player Settings", 0m, 100m),
            ("PlayerCharacterFoodDrainMultiplier", "Player Food Drain", ArkSettingDataType.Decimal, "1.0", "Player Settings", 0m, 100m),
            ("PlayerCharacterStaminaDrainMultiplier", "Player Stamina Drain", ArkSettingDataType.Decimal, "1.0", "Player Settings", 0m, 100m),
            ("PlayerCharacterHealthRecoveryMultiplier", "Player Health Recovery", ArkSettingDataType.Decimal, "1.0", "Player Settings", 0m, 100m),
            ("OxygenSwimSpeedStatMultiplier", "Oxygen Swim Speed", ArkSettingDataType.Decimal, "1.0", "Player Settings", 0m, 100m),
            ("DinoCharacterFoodDrainMultiplier", "Dino Food Drain", ArkSettingDataType.Decimal, "1.0", "Dino Settings", 0m, 100m),
            ("DinoCharacterStaminaDrainMultiplier", "Dino Stamina Drain", ArkSettingDataType.Decimal, "1.0", "Dino Settings", 0m, 100m),
            ("DinoCharacterHealthRecoveryMultiplier", "Dino Health Recovery", ArkSettingDataType.Decimal, "1.0", "Dino Settings", 0m, 100m),
            ("MaxTamedDinos", "Max Tamed Dinos", ArkSettingDataType.Integer, "5000", "Dino Settings", 1m, 50000m),
            ("DayCycleSpeedScale", "Day Cycle Speed", ArkSettingDataType.Decimal, "1.0", "Rates", 0m, 100m),
            ("DayTimeSpeedScale", "Day Time Speed", ArkSettingDataType.Decimal, "1.0", "Rates", 0m, 100m),
            ("NightTimeSpeedScale", "Night Time Speed", ArkSettingDataType.Decimal, "1.0", "Rates", 0m, 100m),
            ("DifficultyOffset", "Difficulty Offset", ArkSettingDataType.Decimal, "1.0", "Rates", 0m, 10m),
            ("OverrideOfficialDifficulty", "Override Official Difficulty", ArkSettingDataType.Decimal, "5.0", "Rates", 0m, 50m),
            ("PreventOfflinePvP", "Prevent Offline PvP", ArkSettingDataType.Boolean, "False", "PvE / PvP Rules", null, null),
            ("PreventOfflinePvPInterval", "Offline PvP Interval", ArkSettingDataType.Decimal, "900", "PvE / PvP Rules", 0m, 86400m),
            ("PvPStructureDecay", "PvP Structure Decay", ArkSettingDataType.Boolean, "False", "PvE / PvP Rules", null, null),
            ("DisableStructureDecayPvE", "Disable Structure Decay PvE", ArkSettingDataType.Boolean, "False", "Structures", null, null),
            ("PvEStructureDecayPeriodMultiplier", "PvE Structure Decay Period", ArkSettingDataType.Decimal, "1.0", "Structures", 0m, 100m),
            ("AutoPvETimer", "Auto PvE Timer", ArkSettingDataType.Boolean, "False", "PvE / PvP Rules", null, null),
            ("AutoPvEStartTimeSeconds", "Auto PvE Start Seconds", ArkSettingDataType.Integer, "0", "PvE / PvP Rules", 0m, 86400m),
            ("AutoPvEStopTimeSeconds", "Auto PvE Stop Seconds", ArkSettingDataType.Integer, "0", "PvE / PvP Rules", 0m, 86400m),
            ("PerPlatformMaxStructuresMultiplier", "Platform Structure Limit", ArkSettingDataType.Decimal, "1.0", "Structures", 0m, 100m),
            ("TheMaxStructuresInRange", "Max Structures In Range", ArkSettingDataType.Integer, "10500", "Structures", 1m, 100000m),
            ("StructurePickupTimeAfterPlacement", "Pickup Window", ArkSettingDataType.Decimal, "30", "Structures", 0m, 86400m),
            ("StructurePickupHoldDuration", "Pickup Hold Duration", ArkSettingDataType.Decimal, "0.5", "Structures", 0m, 60m),
            ("AlwaysAllowStructurePickup", "Always Allow Structure Pickup", ArkSettingDataType.Boolean, "False", "Structures", null, null),
            ("OverrideStructurePlatformPrevention", "Override Platform Prevention", ArkSettingDataType.Boolean, "False", "Structures", null, null),
            ("AutoSavePeriodMinutes", "Autosave Minutes", ArkSettingDataType.Decimal, "15", "Backups", 1m, 120m),
            ("RCONEnabled", "RCON Enabled", ArkSettingDataType.Boolean, "True", "RCON / Console", null, null),
            ("RCONPort", "RCON Port", ArkSettingDataType.Integer, "27020", "RCON / Console", 1m, 65535m)
        });

        AddGameIni(settings, new (string Key, string Name, ArkSettingDataType Type, string Default, string Category, decimal? Min, decimal? Max)[]
        {
            ("MatingIntervalMultiplier", "Mating Interval", ArkSettingDataType.Decimal, "1.0", "Breeding / Imprinting", 0m, 1000m),
            ("EggHatchSpeedMultiplier", "Egg Hatch Speed", ArkSettingDataType.Decimal, "1.0", "Breeding / Imprinting", 0m, 1000m),
            ("BabyMatureSpeedMultiplier", "Baby Mature Speed", ArkSettingDataType.Decimal, "1.0", "Breeding / Imprinting", 0m, 1000m),
            ("BabyFoodConsumptionSpeedMultiplier", "Baby Food Consumption", ArkSettingDataType.Decimal, "1.0", "Breeding / Imprinting", 0m, 1000m),
            ("BabyCuddleIntervalMultiplier", "Baby Cuddle Interval", ArkSettingDataType.Decimal, "1.0", "Breeding / Imprinting", 0m, 1000m),
            ("BabyCuddleGracePeriodMultiplier", "Baby Cuddle Grace Period", ArkSettingDataType.Decimal, "1.0", "Breeding / Imprinting", 0m, 1000m),
            ("BabyCuddleLoseImprintQualitySpeedMultiplier", "Cuddle Lose Imprint Speed", ArkSettingDataType.Decimal, "1.0", "Breeding / Imprinting", 0m, 1000m),
            ("BabyImprintAmountMultiplier", "Baby Imprint Amount", ArkSettingDataType.Decimal, "1.0", "Breeding / Imprinting", 0m, 1000m),
            ("LayEggIntervalMultiplier", "Lay Egg Interval", ArkSettingDataType.Decimal, "1.0", "Breeding / Imprinting", 0m, 1000m),
            ("DinoCountMultiplier", "Dino Count", ArkSettingDataType.Decimal, "1.0", "Dino Settings", 0m, 100m),
            ("MaxTamedDinos", "Max Tamed Dinos", ArkSettingDataType.Integer, "5000", "Dino Settings", 1m, 50000m),
            ("DestroyUnconnectedWaterPipes", "Destroy Unconnected Water Pipes", ArkSettingDataType.Boolean, "False", "Structures", null, null),
            ("GlobalSpoilingTimeMultiplier", "Spoiling Time", ArkSettingDataType.Decimal, "1.0", "Harvesting / Resources", 0m, 1000m),
            ("GlobalItemDecompositionTimeMultiplier", "Item Decomposition", ArkSettingDataType.Decimal, "1.0", "Harvesting / Resources", 0m, 1000m),
            ("GlobalCorpseDecompositionTimeMultiplier", "Corpse Decomposition", ArkSettingDataType.Decimal, "1.0", "Harvesting / Resources", 0m, 1000m),
            ("SupplyCrateLootQualityMultiplier", "Supply Crate Quality", ArkSettingDataType.Decimal, "1.0", "Engrams / Levels", 0m, 1000m),
            ("FishingLootQualityMultiplier", "Fishing Loot Quality", ArkSettingDataType.Decimal, "1.0", "Engrams / Levels", 0m, 1000m),
            ("bAutoUnlockAllEngrams", "Auto Unlock All Engrams", ArkSettingDataType.Boolean, "False", "Engrams / Levels", null, null),
            ("bOnlyAllowSpecifiedEngrams", "Only Allow Specified Engrams", ArkSettingDataType.Boolean, "False", "Engrams / Levels", null, null),
            ("OverrideNamedEngramEntries", "Override Named Engrams", ArkSettingDataType.RepeatedLine, "", "Engrams / Levels", null, null),
            ("EngramEntryAutoUnlocks", "Engram Auto Unlocks", ArkSettingDataType.RepeatedLine, "", "Engrams / Levels", null, null),
            ("LevelExperienceRampOverrides", "Level Experience Ramp", ArkSettingDataType.RepeatedLine, "", "Engrams / Levels", null, null),
            ("OverrideMaxExperiencePointsPlayer", "Max Player XP", ArkSettingDataType.Integer, "0", "Engrams / Levels", 0m, null),
            ("OverrideMaxExperiencePointsDino", "Max Dino XP", ArkSettingDataType.Integer, "0", "Engrams / Levels", 0m, null),
            ("PerLevelStatsMultiplier_Player", "Player Per-Level Stats", ArkSettingDataType.RepeatedLine, "", "Engrams / Levels", null, null),
            ("PerLevelStatsMultiplier_DinoTamed", "Tamed Dino Per-Level Stats", ArkSettingDataType.RepeatedLine, "", "Engrams / Levels", null, null),
            ("PerLevelStatsMultiplier_DinoWild", "Wild Dino Per-Level Stats", ArkSettingDataType.RepeatedLine, "", "Engrams / Levels", null, null),
            ("PlayerBaseStatMultipliers", "Player Base Stats", ArkSettingDataType.RepeatedLine, "", "Engrams / Levels", null, null),
            ("TamedDinoClassDamageMultipliers", "Tamed Dino Damage Multipliers", ArkSettingDataType.RepeatedLine, "", "Dino Settings", null, null),
            ("TamedDinoClassResistanceMultipliers", "Tamed Dino Resistance Multipliers", ArkSettingDataType.RepeatedLine, "", "Dino Settings", null, null),
            ("ConfigAddNPCSpawnEntriesContainer", "Add Spawn Entries", ArkSettingDataType.RepeatedLine, "", "Dino Settings", null, null),
            ("ConfigSubtractNPCSpawnEntriesContainer", "Subtract Spawn Entries", ArkSettingDataType.RepeatedLine, "", "Dino Settings", null, null),
            ("ConfigOverrideNPCSpawnEntriesContainer", "Override Spawn Entries", ArkSettingDataType.RepeatedLine, "", "Dino Settings", null, null),
            ("NPCReplacements", "NPC Replacements", ArkSettingDataType.RepeatedLine, "", "Dino Settings", null, null),
            ("ConfigOverrideItemMaxQuantity", "Override Item Stack Size", ArkSettingDataType.RepeatedLine, "", "Harvesting / Resources", null, null),
            ("ConfigOverrideSupplyCrateItems", "Override Supply Crates", ArkSettingDataType.RepeatedLine, "", "Engrams / Levels", null, null),
            ("ConfigOverrideItemCraftingCosts", "Override Crafting Costs", ArkSettingDataType.RepeatedLine, "", "Harvesting / Resources", null, null),
            ("CraftingSkillBonusMultiplier", "Crafting Skill Bonus", ArkSettingDataType.Decimal, "1.0", "Harvesting / Resources", 0m, 1000m)
        });

        AddExpandedDedicatedServerSettings(settings);

        return settings
            .GroupBy(setting => $"{setting.FileLocation}|{setting.IniSection}|{setting.Key}", StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToArray();
    }

    private static void AddExpandedDedicatedServerSettings(List<ArkSettingDefinition> settings)
    {
        AddLaunch(settings, "server", "Dedicated Server Mode", "Runs the executable as a dedicated server process.", "Startup", ArkSettingDataType.Boolean, "True");
        AddLaunch(settings, "UseBattlEye", "Use BattlEye", "Enables BattlEye anti-cheat when launching the server.", "Startup", ArkSettingDataType.Boolean, "True");
        AddLaunch(settings, "ActiveEvent", "Active Event", "Forces an ARK seasonal event by event identifier.", "Startup", ArkSettingDataType.String, string.Empty, advanced: true);
        AddLaunch(settings, "culture", "Culture", "Overrides server localization culture.", "Advanced", ArkSettingDataType.String, string.Empty, advanced: true);
        AddLaunch(settings, "exclusivejoin", "Exclusive Join", "Restricts joining to the exclusive join list.", "Admin / Passwords", ArkSettingDataType.Boolean, "False", advanced: true);
        AddLaunch(settings, "NoHangDetection", "Disable Hang Detection", "Disables server hang detection.", "Advanced", ArkSettingDataType.Boolean, "False", advanced: true, warning: "Disabling hang detection can hide server freezes.");
        AddLaunch(settings, "NoTimeout", "Disable Timeout", "Disables timeout behavior for some server operations.", "Advanced", ArkSettingDataType.Boolean, "False", advanced: true);
        AddLaunch(settings, "UseVivox", "Use Vivox Voice", "Enables Vivox voice integration when available.", "Advanced", ArkSettingDataType.Boolean, "False", advanced: true);

        AddGameUserSettings(settings, new (string Key, string Name, ArkSettingDataType Type, string Default, string Category, decimal? Min, decimal? Max)[]
        {
            ("ActiveMods", "Active Mods", ArkSettingDataType.StringList, "", "Mods", null, null),
            ("ActiveMapMod", "Active Map Mod", ArkSettingDataType.String, "", "Mods", null, null),
            ("AllowAnyoneBabyImprintCuddle", "Anyone Can Imprint Cuddle", ArkSettingDataType.Boolean, "False", "Breeding / Imprinting", null, null),
            ("AllowCrateSpawnsOnTopOfStructures", "Crates Spawn On Structures", ArkSettingDataType.Boolean, "False", "Structures", null, null),
            ("AllowFlyingStaminaRecovery", "Allow Flyer Stamina Recovery", ArkSettingDataType.Boolean, "False", "Dino Settings", null, null),
            ("AllowHideDamageSourceFromLogs", "Hide Damage Source From Logs", ArkSettingDataType.Boolean, "False", "Logs", null, null),
            ("AllowIntegratedSPlusStructures", "Allow Integrated S+ Structures", ArkSettingDataType.Boolean, "True", "Structures", null, null),
            ("AllowMultipleAttachedC4", "Allow Multiple Attached C4", ArkSettingDataType.Boolean, "False", "PvE / PvP Rules", null, null),
            ("AllowRaidDinoFeeding", "Allow Raid Dino Feeding", ArkSettingDataType.Boolean, "False", "Dino Settings", null, null),
            ("AllowSharedConnections", "Allow Shared Connections", ArkSettingDataType.Boolean, "False", "Network / Ports", null, null),
            ("AllowTekSuitPowersInGenesis", "Allow Tek Suit Powers", ArkSettingDataType.Boolean, "False", "Player Settings", null, null),
            ("alwaysNotifyPlayerJoined", "Notify Player Joined", ArkSettingDataType.Boolean, "False", "Player Settings", null, null),
            ("alwaysNotifyPlayerLeft", "Notify Player Left", ArkSettingDataType.Boolean, "False", "Player Settings", null, null),
            ("AutoDestroyDecayedDinos", "Auto Destroy Decayed Dinos", ArkSettingDataType.Boolean, "False", "Dino Settings", null, null),
            ("AutoDestroyOldStructuresMultiplier", "Old Structure Destroy Multiplier", ArkSettingDataType.Decimal, "1.0", "Structures", 0m, 1000m),
            ("BanListURL", "Ban List URL", ArkSettingDataType.String, "http://arkdedicated.com/banlist.txt", "Admin / Passwords", null, null),
            ("ClampItemSpoilingTimes", "Clamp Item Spoiling Times", ArkSettingDataType.Boolean, "False", "Harvesting / Resources", null, null),
            ("ClampItemStats", "Clamp Item Stats", ArkSettingDataType.Boolean, "False", "Engrams / Levels", null, null),
            ("CrossARKAllowForeignDinoDownloads", "Allow Foreign Dino Downloads", ArkSettingDataType.Boolean, "False", "Cluster", null, null),
            ("DisableDinoDecayPvE", "Disable Dino Decay PvE", ArkSettingDataType.Boolean, "False", "Dino Settings", null, null),
            ("DisableImprintDinoBuff", "Disable Imprint Dino Buff", ArkSettingDataType.Boolean, "False", "Breeding / Imprinting", null, null),
            ("DisableWeatherFog", "Disable Weather Fog", ArkSettingDataType.Boolean, "False", "Player Settings", null, null),
            ("EnableCryopodNerf", "Enable Cryopod Nerf", ArkSettingDataType.Boolean, "False", "Dino Settings", null, null),
            ("EnableExtraStructurePreventionVolumes", "Extra Structure Prevention Volumes", ArkSettingDataType.Boolean, "False", "Structures", null, null),
            ("EnableIdlePlayerKick", "Idle Player Kick", ArkSettingDataType.Boolean, "True", "Player Settings", null, null),
            ("FastDecayInterval", "Fast Decay Interval", ArkSettingDataType.Decimal, "43200", "Structures", 0m, null),
            ("ForceAllStructureLocking", "Force Structure Locking", ArkSettingDataType.Boolean, "False", "Structures", null, null),
            ("ForceAllowCaveFlyers", "Force Allow Cave Flyers", ArkSettingDataType.Boolean, "False", "Dino Settings", null, null),
            ("ForceCanRideFliers", "Force Can Ride Flyers", ArkSettingDataType.Boolean, "False", "Dino Settings", null, null),
            ("ForceFlyerExplosives", "Force Flyer Explosives", ArkSettingDataType.Boolean, "False", "PvE / PvP Rules", null, null),
            ("GlobalVoiceChat", "Global Voice Chat", ArkSettingDataType.Boolean, "False", "Player Settings", null, null),
            ("ItemStackSizeMultiplier", "Item Stack Size Multiplier", ArkSettingDataType.Decimal, "1.0", "Harvesting / Resources", 0m, 1000m),
            ("KickIdlePlayersPeriod", "Kick Idle Players Period", ArkSettingDataType.Decimal, "3600", "Player Settings", 0m, null),
            ("ListenServerTetherDistanceMultiplier", "Tether Distance Multiplier", ArkSettingDataType.Decimal, "1.0", "Advanced", 0m, null),
            ("MaxGateFrameOnSaddles", "Max Gate Frames On Saddles", ArkSettingDataType.Integer, "2", "Structures", 0m, 1000m),
            ("MaxPersonalTamedDinos", "Max Personal Tamed Dinos", ArkSettingDataType.Integer, "0", "Dino Settings", 0m, null),
            ("MaxPlatformSaddleStructureLimit", "Platform Saddle Structure Limit", ArkSettingDataType.Integer, "0", "Structures", 0m, null),
            ("MaxTributeDinos", "Max Tribute Dinos", ArkSettingDataType.Integer, "20", "Cluster", 0m, 1000m),
            ("MaxTributeItems", "Max Tribute Items", ArkSettingDataType.Integer, "50", "Cluster", 0m, 1000m),
            ("MinimumDinoReuploadInterval", "Minimum Dino Reupload Interval", ArkSettingDataType.Decimal, "0", "Cluster", 0m, null),
            ("NoTributeDownloads", "No Tribute Downloads", ArkSettingDataType.Boolean, "False", "Cluster", null, null),
            ("OnlyAutoDestroyCoreStructures", "Only Auto Destroy Core Structures", ArkSettingDataType.Boolean, "False", "Structures", null, null),
            ("OverrideAdminLogging", "Override Admin Logging", ArkSettingDataType.Boolean, "False", "Admin / Passwords", null, null),
            ("OverridePVEAllowStructuresAtSupplyDrops", "Allow PvE Structures At Supply Drops", ArkSettingDataType.Boolean, "False", "Structures", null, null),
            ("PlatformSaddleBuildAreaBoundsMultiplier", "Platform Saddle Build Area", ArkSettingDataType.Decimal, "1.0", "Structures", 0m, 100m),
            ("PreventDiseases", "Prevent Diseases", ArkSettingDataType.Boolean, "False", "Player Settings", null, null),
            ("PreventDownloadDinos", "Prevent Download Dinos", ArkSettingDataType.Boolean, "False", "Cluster", null, null),
            ("PreventDownloadItems", "Prevent Download Items", ArkSettingDataType.Boolean, "False", "Cluster", null, null),
            ("PreventDownloadSurvivors", "Prevent Download Survivors", ArkSettingDataType.Boolean, "False", "Cluster", null, null),
            ("PreventSpawnAnimations", "Prevent Spawn Animations", ArkSettingDataType.Boolean, "False", "Dino Settings", null, null),
            ("PreventTribeAlliances", "Prevent Tribe Alliances", ArkSettingDataType.Boolean, "False", "PvE / PvP Rules", null, null),
            ("PreventUploadDinos", "Prevent Upload Dinos", ArkSettingDataType.Boolean, "False", "Cluster", null, null),
            ("PreventUploadItems", "Prevent Upload Items", ArkSettingDataType.Boolean, "False", "Cluster", null, null),
            ("PreventUploadSurvivors", "Prevent Upload Survivors", ArkSettingDataType.Boolean, "False", "Cluster", null, null),
            ("ProximityChat", "Proximity Chat", ArkSettingDataType.Boolean, "False", "Player Settings", null, null),
            ("PvEDinoDecayPeriodMultiplier", "PvE Dino Decay Period", ArkSettingDataType.Decimal, "1.0", "Dino Settings", 0m, 1000m),
            ("PvPDinoDecay", "PvP Dino Decay", ArkSettingDataType.Boolean, "False", "Dino Settings", null, null),
            ("RCONServerGameLogBuffer", "RCON Game Log Buffer", ArkSettingDataType.Integer, "600", "RCON / Console", 0m, 100000m),
            ("RaidDinoCharacterFoodDrainMultiplier", "Raid Dino Food Drain", ArkSettingDataType.Decimal, "1.0", "Dino Settings", 0m, 1000m),
            ("RandomSupplyCratePoints", "Random Supply Crate Points", ArkSettingDataType.Boolean, "False", "Engrams / Levels", null, null),
            ("ServerAutoForceRespawnWildDinosInterval", "Auto Force Respawn Wild Dinos", ArkSettingDataType.Decimal, "0", "Dino Settings", 0m, null),
            ("ServerForceNoHud", "Force No HUD", ArkSettingDataType.Boolean, "False", "Player Settings", null, null),
            ("ServerHardcore", "Hardcore Server", ArkSettingDataType.Boolean, "False", "Player Settings", null, null),
            ("ShowFloatingDamageText", "Floating Damage Text", ArkSettingDataType.Boolean, "False", "Player Settings", null, null),
            ("TribeLogDestroyedEnemyStructures", "Tribe Log Destroyed Enemy Structures", ArkSettingDataType.Boolean, "False", "Logs", null, null),
            ("UseOptimizedHarvestingHealth", "Optimized Harvesting Health", ArkSettingDataType.Boolean, "False", "Harvesting / Resources", null, null)
        });

        AddGameIni(settings, new (string Key, string Name, ArkSettingDataType Type, string Default, string Category, decimal? Min, decimal? Max)[]
        {
            ("bAllowCustomRecipes", "Allow Custom Recipes", ArkSettingDataType.Boolean, "True", "Harvesting / Resources", null, null),
            ("bAllowFlyerSpeedLeveling", "Allow Flyer Speed Leveling", ArkSettingDataType.Boolean, "False", "Dino Settings", null, null),
            ("bAllowUnlimitedRespecs", "Allow Unlimited Respecs", ArkSettingDataType.Boolean, "False", "Player Settings", null, null),
            ("bAutoPvETimer", "Auto PvE Timer", ArkSettingDataType.Boolean, "False", "PvE / PvP Rules", null, null),
            ("bDisableDinoBreeding", "Disable Dino Breeding", ArkSettingDataType.Boolean, "False", "Breeding / Imprinting", null, null),
            ("bDisableDinoRiding", "Disable Dino Riding", ArkSettingDataType.Boolean, "False", "Dino Settings", null, null),
            ("bDisableDinoTaming", "Disable Dino Taming", ArkSettingDataType.Boolean, "False", "Dino Settings", null, null),
            ("bDisableFriendlyFire", "Disable Friendly Fire", ArkSettingDataType.Boolean, "False", "PvE / PvP Rules", null, null),
            ("bDisableLootCrates", "Disable Loot Crates", ArkSettingDataType.Boolean, "False", "Engrams / Levels", null, null),
            ("bDisablePhotoMode", "Disable Photo Mode", ArkSettingDataType.Boolean, "False", "Player Settings", null, null),
            ("bDisableStructurePlacementCollision", "Disable Structure Placement Collision", ArkSettingDataType.Boolean, "False", "Structures", null, null),
            ("bFlyerPlatformAllowUnalignedDinoBasing", "Flyer Platform Unaligned Dino Basing", ArkSettingDataType.Boolean, "False", "Structures", null, null),
            ("bHardLimitTurretsInRange", "Hard Limit Turrets In Range", ArkSettingDataType.Boolean, "True", "Structures", null, null),
            ("bIgnoreStructuresPreventionVolumes", "Ignore Structure Prevention Volumes", ArkSettingDataType.Boolean, "False", "Structures", null, null),
            ("bIncreasePvPRespawnInterval", "Increase PvP Respawn Interval", ArkSettingDataType.Boolean, "False", "PvE / PvP Rules", null, null),
            ("bLimitTurretsInRange", "Limit Turrets In Range", ArkSettingDataType.Boolean, "True", "Structures", null, null),
            ("bPassiveDefensesDamageRiderlessDinos", "Passive Defenses Damage Riderless Dinos", ArkSettingDataType.Boolean, "False", "PvE / PvP Rules", null, null),
            ("bPvEDisableFriendlyFire", "PvE Disable Friendly Fire", ArkSettingDataType.Boolean, "False", "PvE / PvP Rules", null, null),
            ("bUseCorpseLocator", "Use Corpse Locator", ArkSettingDataType.Boolean, "True", "Player Settings", null, null),
            ("CustomRecipeEffectivenessMultiplier", "Custom Recipe Effectiveness", ArkSettingDataType.Decimal, "1.0", "Harvesting / Resources", 0m, 1000m),
            ("CustomRecipeSkillMultiplier", "Custom Recipe Skill", ArkSettingDataType.Decimal, "1.0", "Harvesting / Resources", 0m, 1000m),
            ("DinoHarvestingDamageMultiplier", "Dino Harvesting Damage", ArkSettingDataType.Decimal, "3.2", "Harvesting / Resources", 0m, 1000m),
            ("DinoTurretDamageMultiplier", "Dino Turret Damage", ArkSettingDataType.Decimal, "1.0", "PvE / PvP Rules", 0m, 1000m),
            ("EggHatchSpeedMultiplier", "Egg Hatch Speed", ArkSettingDataType.Decimal, "1.0", "Breeding / Imprinting", 0m, 1000m),
            ("FuelConsumptionIntervalMultiplier", "Fuel Consumption Interval", ArkSettingDataType.Decimal, "1.0", "Harvesting / Resources", 0m, 1000m),
            ("GenericXPMultiplier", "Generic XP Multiplier", ArkSettingDataType.Decimal, "1.0", "Rates", 0m, 1000m),
            ("HarvestResourceItemAmountClassMultipliers", "Harvest Resource Class Multipliers", ArkSettingDataType.RepeatedLine, "", "Harvesting / Resources", null, null),
            ("IncreasePvPRespawnIntervalBaseAmount", "PvP Respawn Base Amount", ArkSettingDataType.Decimal, "60", "PvE / PvP Rules", 0m, null),
            ("IncreasePvPRespawnIntervalCheckPeriod", "PvP Respawn Check Period", ArkSettingDataType.Decimal, "300", "PvE / PvP Rules", 0m, null),
            ("IncreasePvPRespawnIntervalMultiplier", "PvP Respawn Multiplier", ArkSettingDataType.Decimal, "2.0", "PvE / PvP Rules", 0m, 1000m),
            ("KillXPMultiplier", "Kill XP Multiplier", ArkSettingDataType.Decimal, "1.0", "Rates", 0m, 1000m),
            ("LimitTurretsNum", "Turret Limit Count", ArkSettingDataType.Integer, "100", "Structures", 0m, 100000m),
            ("LimitTurretsRange", "Turret Limit Range", ArkSettingDataType.Decimal, "10000", "Structures", 0m, null),
            ("MatingSpeedMultiplier", "Mating Speed", ArkSettingDataType.Decimal, "1.0", "Breeding / Imprinting", 0m, 1000m),
            ("MaxAlliancesPerTribe", "Max Alliances Per Tribe", ArkSettingDataType.Integer, "10", "PvE / PvP Rules", 0m, 1000m),
            ("MaxNumberOfPlayersInTribe", "Max Players In Tribe", ArkSettingDataType.Integer, "0", "PvE / PvP Rules", 0m, 1000m),
            ("MaxTribesPerAlliance", "Max Tribes Per Alliance", ArkSettingDataType.Integer, "10", "PvE / PvP Rules", 0m, 1000m),
            ("OverrideMaxExperiencePointsPlayer", "Override Max Player XP", ArkSettingDataType.Integer, "0", "Engrams / Levels", 0m, null),
            ("OverrideMaxExperiencePointsDino", "Override Max Dino XP", ArkSettingDataType.Integer, "0", "Engrams / Levels", 0m, null),
            ("PassiveTameIntervalMultiplier", "Passive Tame Interval", ArkSettingDataType.Decimal, "1.0", "Dino Settings", 0m, 1000m),
            ("PoopIntervalMultiplier", "Poop Interval", ArkSettingDataType.Decimal, "1.0", "Player Settings", 0m, 1000m),
            ("PlayerHarvestingDamageMultiplier", "Player Harvesting Damage", ArkSettingDataType.Decimal, "1.0", "Harvesting / Resources", 0m, 1000m),
            ("PlayerHarvestingXPMultiplier", "Player Harvesting XP", ArkSettingDataType.Decimal, "1.0", "Rates", 0m, 1000m),
            ("PlayerKillXPMultiplier", "Player Kill XP", ArkSettingDataType.Decimal, "1.0", "Rates", 0m, 1000m),
            ("PlayerMaxExperiencePoints", "Player Max Experience Points", ArkSettingDataType.Integer, "0", "Engrams / Levels", 0m, null),
            ("PlayerXPForKillXPMultiplier", "Player XP For Kill", ArkSettingDataType.Decimal, "1.0", "Rates", 0m, 1000m),
            ("PreventBreedingForClassNames", "Prevent Breeding Classes", ArkSettingDataType.RepeatedLine, "", "Breeding / Imprinting", null, null),
            ("PreventDinoTameClassNames", "Prevent Tame Classes", ArkSettingDataType.RepeatedLine, "", "Dino Settings", null, null),
            ("ResourceNoReplenishRadiusPlayers", "Resource No Replenish Radius Players", ArkSettingDataType.Decimal, "1.0", "Harvesting / Resources", 0m, 1000m),
            ("ResourceNoReplenishRadiusStructures", "Resource No Replenish Radius Structures", ArkSettingDataType.Decimal, "1.0", "Harvesting / Resources", 0m, 1000m),
            ("SpecialXPMultiplier", "Special XP Multiplier", ArkSettingDataType.Decimal, "1.0", "Rates", 0m, 1000m),
            ("TamedDinoDamageMultiplier", "Tamed Dino Damage", ArkSettingDataType.Decimal, "1.0", "Dino Settings", 0m, 1000m),
            ("TamedDinoResistanceMultiplier", "Tamed Dino Resistance", ArkSettingDataType.Decimal, "1.0", "Dino Settings", 0m, 1000m),
            ("TamedDinoTorporDrainMultiplier", "Tamed Dino Torpor Drain", ArkSettingDataType.Decimal, "1.0", "Dino Settings", 0m, 1000m),
            ("TamedDinoCharacterFoodDrainMultiplier", "Tamed Dino Food Drain", ArkSettingDataType.Decimal, "1.0", "Dino Settings", 0m, 1000m),
            ("WildDinoCharacterFoodDrainMultiplier", "Wild Dino Food Drain", ArkSettingDataType.Decimal, "1.0", "Dino Settings", 0m, 1000m),
            ("WildDinoTorporDrainMultiplier", "Wild Dino Torpor Drain", ArkSettingDataType.Decimal, "1.0", "Dino Settings", 0m, 1000m)
        });
    }

    private static void AddLaunch(List<ArkSettingDefinition> settings, string key, string name, string description, string category, ArkSettingDataType type, string defaultValue, decimal? min = null, decimal? max = null, bool advanced = false, string warning = "")
    {
        settings.Add(new ArkSettingDefinition
        {
            Key = key,
            DisplayName = name,
            Description = description,
            Category = category,
            FileLocation = ArkSettingFileLocation.LaunchArguments,
            DataType = type,
            DefaultValue = defaultValue,
            Min = min,
            Max = max,
            AdvancedSetting = advanced,
            WarningText = warning
        });
    }

    private static void AddGameUserSettings(List<ArkSettingDefinition> settings, IEnumerable<(string Key, string Name, ArkSettingDataType Type, string Default, string Category, decimal? Min, decimal? Max)> entries)
    {
        foreach (var entry in entries)
        {
            AddIni(settings, entry, ArkSettingFileLocation.GameUserSettingsIni, ServerSettingsSection);
        }
    }

    private static void AddGameIni(List<ArkSettingDefinition> settings, IEnumerable<(string Key, string Name, ArkSettingDataType Type, string Default, string Category, decimal? Min, decimal? Max)> entries)
    {
        foreach (var entry in entries)
        {
            AddIni(settings, entry, ArkSettingFileLocation.GameIni, GameModeSection);
        }
    }

    private static void AddIni(List<ArkSettingDefinition> settings, (string Key, string Name, ArkSettingDataType Type, string Default, string Category, decimal? Min, decimal? Max) entry, ArkSettingFileLocation location, string section)
    {
        var dangerous = IsDangerousSetting(entry.Key);
        settings.Add(new ArkSettingDefinition
        {
            Key = entry.Key,
            DisplayName = entry.Name,
            Description = Describe(entry.Key, entry.Name, location),
            Category = entry.Category,
            FileLocation = location,
            IniSection = section,
            DataType = entry.Type,
            DefaultValue = entry.Default,
            Min = entry.Min,
            Max = entry.Max,
            AdvancedSetting = entry.Type == ArkSettingDataType.RepeatedLine || dangerous || entry.Category.Equals("Advanced", StringComparison.OrdinalIgnoreCase),
            WarningText = entry.Type == ArkSettingDataType.RepeatedLine
                ? "Repeated ARK config blocks are preserved and appended in order."
                : dangerous
                    ? "Changing this can alter saves, transfers, security, PvP balance, or player progression. Review before applying."
                    : string.Empty
        });
    }

    private static string Describe(string key, string name, ArkSettingFileLocation location)
    {
        var destination = location == ArkSettingFileLocation.GameIni
            ? "Game.ini"
            : location == ArkSettingFileLocation.GameUserSettingsIni
                ? "GameUserSettings.ini"
                : "the launch command";
        return $"{name} dedicated-server setting written to {destination}.";
    }

    private static bool IsDangerousSetting(string key)
    {
        var lowered = key.ToLowerInvariant();
        return lowered.Contains("destroy", StringComparison.Ordinal) ||
               lowered.Contains("delete", StringComparison.Ordinal) ||
               lowered.Contains("preventdownload", StringComparison.Ordinal) ||
               lowered.Contains("preventupload", StringComparison.Ordinal) ||
               lowered.Contains("notribute", StringComparison.Ordinal) ||
               lowered.Contains("override", StringComparison.Ordinal) ||
               lowered.Contains("experience", StringComparison.Ordinal) ||
               lowered.Contains("engram", StringComparison.Ordinal) ||
               lowered.Contains("spawn", StringComparison.Ordinal) ||
               lowered.Contains("replacement", StringComparison.Ordinal) ||
               lowered.Contains("hardcore", StringComparison.Ordinal) ||
               lowered.Contains("ban", StringComparison.Ordinal) ||
               lowered.Contains("password", StringComparison.Ordinal);
    }
}
