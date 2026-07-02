namespace GameServerManager.Services.SevenDaysToDie;

// ─── Configuration drift analysis ─────────────────────────────────────────────
//
// Compares the ACTIVE serverconfig.xml (source of truth) against the schema the
// app knows about, and reports the differences instead of silently "fixing"
// them. Nothing here modifies the file.
//
//   Unknown   — on disk, not in schema: preserved verbatim, surfaced in the UI
//               under "Unrecognized" with a generic editor (new game versions
//               add properties before the app learns about them).
//   Legacy    — V2 gameplay keys superseded by SandboxCode: candidates for the
//               migration assistant.
//   Retired   — keys the current game build rejects at startup (or app-internal
//               keys leaked by an older app version): stripped on next save.
//   Missing   — in schema but not on disk: informational; the game applies its
//               own defaults for absent properties.

public sealed class SevenDaysConfigProperty
{
    public string Name { get; init; } = string.Empty;
    public string Value { get; init; } = string.Empty;
}

public sealed class SevenDaysConfigDriftReport
{
    public IReadOnlyList<SevenDaysConfigProperty> UnknownProperties { get; init; } = [];
    public IReadOnlyList<SevenDaysConfigProperty> LegacyProperties { get; init; } = [];
    public IReadOnlyList<SevenDaysConfigProperty> RetiredProperties { get; init; } = [];
    public IReadOnlyList<string> MissingFromFile { get; init; } = [];
    public SevenDaysGeneration Generation { get; init; } = SevenDaysGeneration.Unknown;
    public bool HasSandboxCode { get; init; }
    public bool ConfigFileExists { get; init; }

    public bool HasDrift => UnknownProperties.Count > 0 ||
                            LegacyProperties.Count > 0 ||
                            RetiredProperties.Count > 0;
}

public sealed class SevenDaysToDieConfigDriftService
{
    /// <summary>
    /// Analyzes the active config file against the given schema keys
    /// (typically the provider's SettingsDefinitions keys).
    /// </summary>
    public async Task<SevenDaysConfigDriftReport> AnalyzeAsync(
        string configPath, IEnumerable<string> schemaKeys)
    {
        if (!File.Exists(configPath))
            return new SevenDaysConfigDriftReport { ConfigFileExists = false };

        var doc = await ServerConfigXmlDocument.LoadAsync(configPath);
        return Analyze(doc, schemaKeys);
    }

    /// <summary>Pure analysis over an already-loaded document (testable headlessly).</summary>
    public SevenDaysConfigDriftReport Analyze(
        ServerConfigXmlDocument doc, IEnumerable<string> schemaKeys)
    {
        var schema = new HashSet<string>(schemaKeys, StringComparer.OrdinalIgnoreCase);
        // Profile-level fields are written by the config service even though some
        // providers model them outside SettingsDefinitions.
        schema.Add("ServerName");
        schema.Add("ServerPassword");
        schema.Add("ServerMaxPlayerCount");

        var onDisk = doc.GetAllPropertyNames();
        var unknown = new List<SevenDaysConfigProperty>();
        var legacy = new List<SevenDaysConfigProperty>();
        var retired = new List<SevenDaysConfigProperty>();

        foreach (var name in onDisk)
        {
            var value = doc.GetValue(name) ?? string.Empty;
            if (SevenDaysToDieConfigService.IsAppInternalKey(name))
                retired.Add(new SevenDaysConfigProperty { Name = name, Value = value });
            else if (SevenDaysToDieConfigService.IsLegacyV2Key(name))
                legacy.Add(new SevenDaysConfigProperty { Name = name, Value = value });
            else if (!schema.Contains(name))
                unknown.Add(new SevenDaysConfigProperty { Name = name, Value = value });
        }

        var onDiskSet = new HashSet<string>(onDisk, StringComparer.OrdinalIgnoreCase);
        var missing = schema
            .Where(k => !onDiskSet.Contains(k))
            .Where(k => !SevenDaysToDieConfigService.IsAppInternalKey(k))
            .OrderBy(k => k, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var sandbox = doc.GetValue("SandboxCode");

        return new SevenDaysConfigDriftReport
        {
            ConfigFileExists = true,
            UnknownProperties = unknown,
            LegacyProperties = legacy,
            RetiredProperties = retired,
            MissingFromFile = missing,
            Generation = SevenDaysToDieVersionService.ClassifyGeneration(onDisk),
            HasSandboxCode = !string.IsNullOrWhiteSpace(sandbox)
        };
    }
}
