using System.Text.Json;

namespace GameServerManager.Services.SevenDaysToDie;

// ─── V2.x → V3.0 configuration migration ─────────────────────────────────────
//
// V3 removed the individual gameplay <property> elements and folded them into
// SandboxCode. A V2 config booted on a V3 server aborts startup ("Unknown
// config option"), so migration means:
//
//   1. Build a PLAN: every legacy property found on disk, classified as an
//      Exact rename, an Approximate mapping (units/scale may differ — the old
//      value is carried verbatim, never silently rounded), or Unsupported.
//   2. APPLY: back up the original file, strip the legacy properties, write a
//      JSON migration report next to the backup, save atomically.
//   3. The proposed sandbox values go into the report and (when a verified
//      codec exists for the installed build) can be encoded into a SandboxCode.
//      Without a codec we NEVER fabricate a code — the plan tells the user the
//      target option for each old value instead.
//   4. ROLLBACK: restore the pre-migration backup byte-for-byte.

public enum MigrationKind
{
    Exact,        // same meaning, possibly renamed — value carries over as-is
    Approximate,  // target exists but scale/units/enum may differ — needs review
    Unsupported   // no V3 equivalent — value cannot carry over
}

public sealed record LegacyPropertyMapping(
    string LegacyKey, string? TargetOption, MigrationKind Kind, string Note);

public sealed class MigrationPlanEntry
{
    public string LegacyKey { get; init; } = string.Empty;
    public string OldValue { get; init; } = string.Empty;
    public string? TargetOption { get; init; }
    public string? ProposedValue { get; init; }
    public MigrationKind Kind { get; init; }
    public string Note { get; init; } = string.Empty;
}

public sealed class MigrationResult
{
    public string BackupPath { get; init; } = string.Empty;
    public string ReportPath { get; init; } = string.Empty;
    public int RemovedKeyCount { get; init; }
}

public sealed class SevenDaysToDieMigrationService
{
    // The complete set of legacy keys the V3 game rejects, mapped to their V3
    // sandbox equivalents. Target option names refer to SandboxSchema.BuiltIn.
    public static readonly IReadOnlyList<LegacyPropertyMapping> Mappings =
    [
        new("GameDifficulty",          null,                   MigrationKind.Unsupported,
            "V3 has no single difficulty property — pick an official preset instead."),
        new("BlockDamagePlayer",       "BlockDamage",          MigrationKind.Exact, "Renamed."),
        new("BlockDamageAI",           "EnemyBlockDamage",     MigrationKind.Exact, "Renamed."),
        new("BlockDamageAIBM",         "BloodMoonBlockDamage", MigrationKind.Exact, "Renamed."),
        new("XPMultiplier",            "XPMultiplier",         MigrationKind.Exact, "Same option."),
        new("DayNightLength",          "TwentyFourHourCycle",  MigrationKind.Approximate,
            "V2 stored minutes per day; verify the V3 unit before applying."),
        new("DayLightLength",          "DaylightLength",       MigrationKind.Exact, "Renamed."),
        new("BiomeProgression",        "BiomeProgression",     MigrationKind.Exact, "Same option."),
        new("StormFreq",               "StormFrequency",       MigrationKind.Exact, "Renamed."),
        new("DeathPenalty",            "DeathPenalty",         MigrationKind.Approximate,
            "V3 split the penalty into mode/count/degradation options; review."),
        new("DropOnDeath",             "DropOnDeath",          MigrationKind.Exact, "Same option."),
        new("DropOnQuit",              "DropOnQuit",           MigrationKind.Exact, "Same option."),
        new("JarRefund",               "JarRefund",            MigrationKind.Exact, "Same option."),
        new("EnemySpawnMode",          "EnemySpawning",        MigrationKind.Approximate,
            "V2 was on/off; V3 may expose modes."),
        new("EnemyDifficulty",         null,                   MigrationKind.Unsupported,
            "Folded into the damage-dealt/damage-taken options in V3."),
        new("ZombieFeralSense",        "ZombieFeralSense",     MigrationKind.Exact, "Same option."),
        new("ZombieMove",              "ZombieDaySpeed",       MigrationKind.Approximate,
            "V2 enum (0–4); verify against the V3 speed values."),
        new("ZombieMoveNight",         "ZombieNightSpeed",     MigrationKind.Approximate,
            "V2 enum (0–4); verify against the V3 speed values."),
        new("ZombieFeralMove",         "ZombieFeralSpeed",     MigrationKind.Approximate,
            "V2 enum (0–4); verify against the V3 speed values."),
        new("ZombieBMMove",            "ZombieBloodMoonSpeed", MigrationKind.Approximate,
            "V2 enum (0–4); verify against the V3 speed values."),
        new("AISmellMode",             "AISmellMode",          MigrationKind.Exact, "Same option."),
        new("BloodMoonFrequency",      "BloodMoonFrequency",   MigrationKind.Exact, "Same option."),
        new("BloodMoonRange",          "BloodMoonRange",       MigrationKind.Exact, "Same option."),
        new("BloodMoonWarning",        "BloodMoonWarning",     MigrationKind.Exact, "Same option."),
        new("BloodMoonEnemyCount",     "BloodMoonEnemyCount",  MigrationKind.Exact, "Same option."),
        new("LootAbundance",           "LootAbundance",        MigrationKind.Exact, "Same option."),
        new("LootRespawnDays",         "LootRespawnDays",      MigrationKind.Exact, "Same option."),
        new("AirDropFrequency",        "AirDrops",             MigrationKind.Approximate,
            "V2 stored hours between drops; V3 may use an enum."),
        new("AirDropMarker",           "MarkAirDrops",         MigrationKind.Exact, "Renamed."),
        new("QuestProgressionDailyLimit", "QuestsPerDay",      MigrationKind.Approximate,
            "Verify the V3 value range."),
    ];

    private static readonly JsonSerializerOptions _json = new() { WriteIndented = true };

    /// <summary>
    /// Builds the migration plan for a config file: one entry per legacy
    /// property actually present. Empty plan ⇒ nothing to migrate.
    /// </summary>
    public async Task<List<MigrationPlanEntry>> BuildPlanAsync(string configPath)
    {
        if (!File.Exists(configPath))
            return [];

        var doc = await ServerConfigXmlDocument.LoadAsync(configPath);
        return BuildPlan(doc);
    }

    /// <summary>Pure plan builder over a loaded document (testable headlessly).</summary>
    public List<MigrationPlanEntry> BuildPlan(ServerConfigXmlDocument doc)
    {
        var plan = new List<MigrationPlanEntry>();
        foreach (var mapping in Mappings)
        {
            var oldValue = doc.GetValue(mapping.LegacyKey);
            if (oldValue == null)
                continue;

            plan.Add(new MigrationPlanEntry
            {
                LegacyKey = mapping.LegacyKey,
                OldValue = oldValue,
                TargetOption = mapping.TargetOption,
                // Old value carried verbatim — the user chooses any adjustment.
                ProposedValue = mapping.Kind == MigrationKind.Unsupported ? null : oldValue,
                Kind = mapping.Kind,
                Note = mapping.Note
            });
        }

        return plan;
    }

    /// <summary>
    /// Converts a plan into decoded sandbox settings (Exact + Approximate
    /// entries only) — the input for a codec or for manual entry in-game.
    /// </summary>
    public static SandboxSettings ToSandboxSettings(IEnumerable<MigrationPlanEntry> plan)
    {
        var settings = new SandboxSettings { Source = "migration" };
        foreach (var entry in plan)
        {
            if (entry.TargetOption != null && entry.ProposedValue != null)
                settings.Values[entry.TargetOption] = entry.ProposedValue;
        }
        return settings;
    }

    /// <summary>
    /// Applies the migration: backs up the original config, removes every
    /// legacy property in the plan, writes a JSON report alongside the backup,
    /// and saves atomically. The original file is always recoverable.
    /// </summary>
    public async Task<MigrationResult> ApplyAsync(string configPath, List<MigrationPlanEntry> plan)
    {
        if (!File.Exists(configPath))
            throw new FileNotFoundException("serverconfig.xml not found.", configPath);
        if (plan.Count == 0)
            throw new InvalidOperationException("Nothing to migrate — the plan is empty.");

        var timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
        var backupPath = configPath + $".pre-migration-{timestamp}.bak";
        File.Copy(configPath, backupPath, overwrite: false);

        var reportPath = Path.Combine(
            Path.GetDirectoryName(configPath)!, $"migration-report-{timestamp}.json");
        await File.WriteAllTextAsync(reportPath, JsonSerializer.Serialize(new
        {
            migratedAtUtc = DateTime.UtcNow,
            configPath,
            backupPath,
            entries = plan
        }, _json));

        var doc = await ServerConfigXmlDocument.LoadAsync(configPath);
        foreach (var entry in plan)
            doc.RemoveValue(entry.LegacyKey);

        await doc.SaveAtomicAsync(configPath, createBackup: false); // we made our own backup

        return new MigrationResult
        {
            BackupPath = backupPath,
            ReportPath = reportPath,
            RemovedKeyCount = plan.Count
        };
    }

    /// <summary>Restores the pre-migration backup byte-for-byte.</summary>
    public void Rollback(MigrationResult result, string configPath)
    {
        if (!File.Exists(result.BackupPath))
            throw new FileNotFoundException("Pre-migration backup not found.", result.BackupPath);

        File.Copy(result.BackupPath, configPath, overwrite: true);
    }
}
