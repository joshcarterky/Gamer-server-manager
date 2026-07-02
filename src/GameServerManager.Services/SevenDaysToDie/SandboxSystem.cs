using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace GameServerManager.Services.SevenDaysToDie;

// ─── V3 Sandbox system ────────────────────────────────────────────────────────
//
// V3 moved most gameplay rules into the single SandboxCode property. The code's
// binary encoding is proprietary and undocumented, so this system is built
// around three hard rules:
//
//   1. NEVER guess the encoding. Game codes stay opaque unless a codec that has
//      been verified against the installed build is registered. The codec is a
//      plug-in point (ISandboxCodec) so a future game update can supply a new
//      implementation without touching the UI.
//   2. NEVER replace a valid code when decoding fails — TryDecode either
//      succeeds fully or reports an error and leaves the original untouched.
//   3. The server's own `getsandboxoptions` (gso) console output is the
//      authoritative decoded view — the parser below turns it into the same
//      SandboxSettings model the editor uses.

// ── Decoded settings model ────────────────────────────────────────────────────

public sealed class SandboxSettings
{
    /// <summary>Decoded option values, keyed by option name.</summary>
    public SortedDictionary<string, string> Values { get; } =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Where these values came from: "gso", "codec:<id>", "preset", …</summary>
    public string Source { get; set; } = string.Empty;

    /// <summary>Game version the values were captured on, when known.</summary>
    public string? GameVersion { get; set; }

    public int Count => Values.Count;

    public SandboxSettings Clone()
    {
        var copy = new SandboxSettings { Source = Source, GameVersion = GameVersion };
        foreach (var (k, v) in Values)
            copy.Values[k] = v;
        return copy;
    }
}

// ── Versioned option schema ───────────────────────────────────────────────────

public sealed class SandboxOptionDefinition
{
    public string Key { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public string Category { get; init; } = string.Empty;

    /// <summary>
    /// Optional typed metadata. Only populated when verified against a real
    /// game build (via schema override file or gso capture) — the built-in
    /// schema deliberately ships names and categories only, because inventing
    /// allowed values for an unverified build would be worse than none.
    /// </summary>
    public string? DataType { get; init; }
    public string? DefaultValue { get; init; }
    public List<string>? AllowedValues { get; init; }
    public string? Description { get; init; }
    public string? FirstSupportedVersion { get; init; }
    public string? LastSupportedVersion { get; init; }
}

public sealed class SandboxSchema
{
    public string SchemaVersion { get; init; } = "3.0-builtin";
    public IReadOnlyList<SandboxOptionDefinition> Options { get; init; } = [];

    public SandboxOptionDefinition? Find(string key) =>
        Options.FirstOrDefault(o => o.Key.Equals(key, StringComparison.OrdinalIgnoreCase));

    public IEnumerable<string> Categories => Options.Select(o => o.Category).Distinct();

    /// <summary>
    /// Loads a replacement schema from a JSON file (dropped in by a future app
    /// or game update), falling back to the built-in schema when absent/invalid.
    /// </summary>
    public static async Task<SandboxSchema> LoadAsync(string? overridePath = null)
    {
        if (overridePath != null && File.Exists(overridePath))
        {
            try
            {
                var json = await File.ReadAllTextAsync(overridePath);
                var loaded = JsonSerializer.Deserialize<SandboxSchema>(json, _jsonOptions);
                if (loaded is { Options.Count: > 0 })
                    return loaded;
            }
            catch (Exception ex) when (ex is JsonException or IOException)
            {
                // Corrupt override — the built-in schema is always a safe fallback.
            }
        }

        return BuiltIn;
    }

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip
    };

    // Built-in V3.0 option catalog: names and categories only (see note on
    // SandboxOptionDefinition). Display names are derived from the keys.
    public static SandboxSchema BuiltIn { get; } = new()
    {
        SchemaVersion = "3.0-builtin",
        Options = BuildCatalog()
    };

    private static List<SandboxOptionDefinition> BuildCatalog()
    {
        var catalog = new (string Category, string[] Keys)[]
        {
            ("Player", new[]
            {
                "RangedDamage", "MeleeDamage", "BlockDamage", "TerrainDamage",
                "HeadshotMultiplier", "PlayerDamageIn", "WalkSpeed", "RunSpeed",
                "CrouchSpeed", "JumpStrength", "StaminaRegeneration", "StaminaUsage",
                "XPMultiplier", "ShowXP", "LevelStatBonus", "SkillGainRate",
                "SkillGainAmount", "DeathPenalty", "LoseOnDeath", "DeathLossCount",
                "DeathDegradation", "DeathDegradationAmount", "DropOnDeath",
                "DropOnQuit", "InfectionRate", "NewPlayerBuff", "EncumbranceSlots",
                "JarRefund"
            }),
            ("Entity", new[]
            {
                "EnemySpawning", "MaximumEnemyTier", "EnemyDensity", "EnemyRespawn",
                "BiomeAnimalRespawn", "EnemyDamageDealt", "EntityDamageIn",
                "EnemyBlockDamage", "BloodMoonBlockDamage", "HeadshotMode",
                "EntityHealthBars", "ShowEntityDamage", "ZombieDaySpeed",
                "ZombieNightSpeed", "ZombieFeralSpeed", "ZombieBloodMoonSpeed",
                "ZombieFeralSense", "AISmellMode", "ZombieRage", "ZombieDigging",
                "ZombiesEatAnimals"
            }),
            ("World", new[]
            {
                "GlobalGameStage", "BiomeGameStage", "BiomeProgression",
                "TemperatureSurvival", "MaximumTechnologyType", "WorkstationsInTheWild",
                "BloodMoonFrequency", "BloodMoonRange", "BloodMoonEnemyCount",
                "BloodMoonWarning", "AirDrops", "AirDropVariance", "StormFrequency",
                "StormWarning", "HeatMapSensitivity", "TwentyFourHourCycle",
                "DaylightLength", "MarkAirDrops", "AllowMap", "AllowCompass",
                "AllowScreenMarkers", "ShowLocationInformation", "ShowDayAndTime"
            }),
            ("Resources", new[]
            {
                "MaximumLootTier", "GlobalLootStage", "BiomeLootStage", "POILootStage",
                "LootRespawnDays", "LootTimer", "LootBagDrop", "LootAbundance",
                "FoodAbundance", "DrinkAbundance", "MedicalAbundance", "AmmoAbundance",
                "ResourceAbundance", "ArmorAbundance", "MeleeAbundance",
                "RangedAbundance", "CurrencyAbundance", "MagazineAbundance",
                "TreasureMapChance", "MiningYield", "CropYield", "SeedDrop",
                "ResourceYield", "CropGrowth"
            }),
            ("Crafting", new[]
            {
                "CraftingProgression", "CraftingMaximumTier", "MagazineProgress",
                "BackpackCrafting", "WorkstationCrafting", "SmelterType",
                "CraftingTimer", "CraftingCost", "CraftingYield", "ScrappingYield",
                "DewCollectorTime", "DewCollectorYield", "DewCollectorInput",
                "ApiaryTime", "ApiaryYield", "ApiaryInput", "ItemDegradation",
                "RepairMethod", "RepairDegradation"
            }),
            ("Traders", new[]
            {
                "TradingEnabled", "VendingEnabled", "TraderHours", "TraderProtection",
                "TradingDialog", "GlobalTraderStage", "TraderMaximumTier",
                "TraderItemCount", "VendingItemCount", "TraderReset", "VendingReset",
                "SellPrice", "BuyPrice", "TraderBuyLimit"
            }),
            ("Tasks", new[]
            {
                "Challenges", "Quests", "IntroChallenges", "IntroQuest", "TradeRoutes",
                "BuriedQuests", "POIQuests", "QuestsPerTier", "QuestsPerDay",
                "BaseSkillPoints"
            }),
            ("Miscellaneous", new[]
            {
                "VehicleFuelUsage", "VehicleEntityDamage", "VehicleBlockDamage",
                "VehicleSelfDamage", "ElectricalOutput", "CelebrateKills", "BigHeads",
                "TinyZombies", "Gravity", "SillySounds", "BlackAndWhite"
            })
        };

        return catalog
            .SelectMany(c => c.Keys.Select(k => new SandboxOptionDefinition
            {
                Key = k,
                DisplayName = Humanize(k),
                Category = c.Category
            }))
            .ToList();
    }

    /// <summary>"BloodMoonFrequency" → "Blood Moon Frequency", keeping XP/POI/AI intact.</summary>
    internal static string Humanize(string key)
    {
        var spaced = Regex.Replace(key, "(?<=[a-z0-9])(?=[A-Z])|(?<=[A-Z])(?=[A-Z][a-z])", " ");
        return spaced
            .Replace("Twenty Four Hour Cycle", "24-Hour Day Cycle")
            .Replace("XPMultiplier", "XP Multiplier");
    }
}

// ── Replaceable codec ─────────────────────────────────────────────────────────

public interface ISandboxCodec
{
    /// <summary>Stable identifier, e.g. "v3.0-b259".</summary>
    string CodecId { get; }

    /// <summary>Game versions this codec was verified against.</summary>
    string VerifiedAgainst { get; }

    /// <summary>Cheap syntactic pre-check (never a correctness guarantee).</summary>
    bool CanHandle(string code);

    /// <summary>
    /// Decodes a code into settings. On failure returns false with an error and
    /// MUST NOT return partial results.
    /// </summary>
    bool TryDecode(string code, out SandboxSettings settings, out string? error);

    /// <summary>Encodes settings into a code. Same all-or-nothing contract.</summary>
    bool TryEncode(SandboxSettings settings, out string code, out string? error);
}

public sealed class SandboxCodecRegistry
{
    private readonly List<ISandboxCodec> _codecs = [];

    public IReadOnlyList<ISandboxCodec> Codecs => _codecs;

    public void Register(ISandboxCodec codec) => _codecs.Add(codec);

    /// <summary>First codec whose pre-check accepts the code, or null.</summary>
    public ISandboxCodec? ResolveForCode(string code) =>
        string.IsNullOrWhiteSpace(code) ? null : _codecs.FirstOrDefault(c => c.CanHandle(code));

    /// <summary>
    /// The default registry ships EMPTY of game codecs: the game's encoding is
    /// proprietary and has not been verified against a live build, and rule 1
    /// forbids shipping a guessed implementation. Drop-in codecs register here
    /// once verified. The UI treats "no codec" as: keep the code opaque, offer
    /// gso import for the decoded view.
    /// </summary>
    public static SandboxCodecRegistry CreateDefault() => new();
}

// ── getsandboxoptions (gso) output parser ─────────────────────────────────────

public static class GetSandboxOptionsParser
{
    // Accepts the common console formats:  "Name = Value", "Name: Value",
    // "GameOption Name = Value" — tolerant because builds vary the banner text.
    private static readonly Regex _linePattern = new(
        @"^\s*(?:GameOption\s+)?(?<name>[A-Za-z][A-Za-z0-9_]{1,63})\s*[:=]\s*(?<value>.+?)\s*$",
        RegexOptions.Compiled);

    // Console noise that matches the shape of an option line but isn't one.
    private static readonly HashSet<string> _ignoredNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "Time", "INF", "WRN", "ERR", "Executing", "Command"
    };

    /// <summary>
    /// Parses `getsandboxoptions` console output into decoded settings.
    /// Returns false (with an error) when no option lines are found.
    /// </summary>
    public static bool TryParse(string consoleOutput, out SandboxSettings settings, out string? error)
    {
        settings = new SandboxSettings { Source = "gso" };
        error = null;

        if (string.IsNullOrWhiteSpace(consoleOutput))
        {
            error = "The pasted text is empty.";
            return false;
        }

        foreach (var rawLine in consoleOutput.Split('\n'))
        {
            var line = rawLine.TrimEnd('\r');
            var m = _linePattern.Match(line);
            if (!m.Success)
                continue;

            var name = m.Groups["name"].Value;
            if (_ignoredNames.Contains(name))
                continue;

            settings.Values[name] = m.Groups["value"].Value;
        }

        if (settings.Count == 0)
        {
            error = "No sandbox options found. Paste the full output of the " +
                    "'getsandboxoptions' (gso) console command.";
            return false;
        }

        return true;
    }
}

// ── Comparison ────────────────────────────────────────────────────────────────

public sealed record SandboxDiff(string Option, string? ValueA, string? ValueB);

public static class SandboxComparer
{
    /// <summary>
    /// All options that differ between two decoded sets, including options
    /// present on only one side (the other side reported as null).
    /// </summary>
    public static IReadOnlyList<SandboxDiff> Compare(SandboxSettings a, SandboxSettings b)
    {
        var keys = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        keys.UnionWith(a.Values.Keys);
        keys.UnionWith(b.Values.Keys);

        var diffs = new List<SandboxDiff>();
        foreach (var key in keys)
        {
            var inA = a.Values.TryGetValue(key, out var va);
            var inB = b.Values.TryGetValue(key, out var vb);
            if (!inA || !inB || !string.Equals(va, vb, StringComparison.Ordinal))
                diffs.Add(new SandboxDiff(key, inA ? va : null, inB ? vb : null));
        }

        return diffs;
    }
}

// ── Presets ───────────────────────────────────────────────────────────────────

public sealed class SandboxPreset
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// The raw game code. Official presets ship WITHOUT a code — a code is only
    /// stored once captured from a real game menu, because a code is only valid
    /// for the build it was generated on.
    /// </summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>Game version/build the code was captured on, e.g. "3.0.0 (b259)".</summary>
    public string? CapturedOnBuild { get; set; }

    public bool IsOfficial { get; set; }
    public bool IsFavorite { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime ModifiedAt { get; set; } = DateTime.UtcNow;

    [JsonIgnore]
    public bool HasCode => !string.IsNullOrWhiteSpace(Code);
}

public sealed class SandboxPresetStore
{
    private readonly string _filePath;

    private static readonly JsonSerializerOptions _json = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    /// <summary>Official V3.0 preset names, shown as placeholders until a code is captured.</summary>
    public static readonly string[] OfficialPresetNames =
    [
        "Scavenger", "Adventurer", "Nomad", "Warrior", "Survivalist", "Insane",
        "Undead Matinee", "Madmole's Mayhem", "Almost Creative Mode", "Bite Club",
        "Legacy Survival", "7 Days Later", "Caveman's Life", "Dumpster Diver",
        "Dying World", "Disaster Film", "Chibi Mode"
    ];

    public SandboxPresetStore(string settingsDirectory)
    {
        _filePath = Path.Combine(settingsDirectory, "7dtd-sandbox-presets.json");
    }

    /// <summary>
    /// Loads custom presets from disk merged with placeholders for any official
    /// preset that has not been captured yet.
    /// </summary>
    public async Task<List<SandboxPreset>> LoadAsync()
    {
        var custom = new List<SandboxPreset>();
        if (File.Exists(_filePath))
        {
            try
            {
                var json = await File.ReadAllTextAsync(_filePath);
                custom = JsonSerializer.Deserialize<List<SandboxPreset>>(json, _json) ?? [];
            }
            catch (Exception ex) when (ex is JsonException or IOException)
            {
                // Corrupt store: keep it on disk for inspection, start empty in memory.
            }
        }

        var known = new HashSet<string>(custom.Select(p => p.Name), StringComparer.OrdinalIgnoreCase);
        foreach (var name in OfficialPresetNames.Where(n => !known.Contains(n)))
        {
            custom.Add(new SandboxPreset
            {
                Name = name,
                IsOfficial = true,
                Description = "Official V3.0 gameplay preset. Capture its code from the " +
                              "in-game menu on your installed build to make it applicable."
            });
        }

        return custom
            .OrderByDescending(p => p.IsFavorite)
            .ThenBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>Adds or replaces a preset by name and persists the store.</summary>
    public async Task SaveAsync(SandboxPreset preset)
    {
        if (string.IsNullOrWhiteSpace(preset.Name))
            throw new ArgumentException("Preset name is required.");

        var all = await LoadAsync();
        var existing = all.FirstOrDefault(p =>
            p.Name.Equals(preset.Name, StringComparison.OrdinalIgnoreCase));
        if (existing != null)
        {
            preset.CreatedAt = existing.CreatedAt;
            preset.IsOfficial = existing.IsOfficial;
            all.Remove(existing);
        }
        preset.ModifiedAt = DateTime.UtcNow;
        all.Add(preset);

        await PersistAsync(all);
    }

    /// <summary>Deletes a preset. Official placeholders without a code cannot be deleted.</summary>
    public async Task DeleteAsync(string name)
    {
        var all = await LoadAsync();
        all.RemoveAll(p =>
            p.Name.Equals(name, StringComparison.OrdinalIgnoreCase) &&
            (!p.IsOfficial || p.HasCode));
        await PersistAsync(all);
    }

    public async Task<SandboxPreset> CloneAsync(string sourceName, string newName)
    {
        var all = await LoadAsync();
        var source = all.FirstOrDefault(p =>
                p.Name.Equals(sourceName, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException($"Preset '{sourceName}' not found.");

        var clone = new SandboxPreset
        {
            Name = newName,
            Description = source.Description,
            Code = source.Code,
            CapturedOnBuild = source.CapturedOnBuild,
            IsOfficial = false
        };
        await SaveAsync(clone);
        return clone;
    }

    private async Task PersistAsync(List<SandboxPreset> all)
    {
        // Official placeholders (no captured code) are regenerated on load;
        // persisting them would just duplicate static metadata.
        var toPersist = all.Where(p => !p.IsOfficial || p.HasCode).ToList();

        Directory.CreateDirectory(Path.GetDirectoryName(_filePath)!);
        var tempPath = _filePath + ".tmp";
        await File.WriteAllTextAsync(tempPath, JsonSerializer.Serialize(toPersist, _json));
        File.Move(tempPath, _filePath, overwrite: true);
    }
}
