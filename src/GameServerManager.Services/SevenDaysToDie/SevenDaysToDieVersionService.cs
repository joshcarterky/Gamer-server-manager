using System.Text.RegularExpressions;

namespace GameServerManager.Services.SevenDaysToDie;

// ─── Installed-version detection ──────────────────────────────────────────────
//
// The installed server files are the source of truth, not hardcoded settings.
// Three independent signals, in order of authority:
//
//   1. The server's own log files      — "Version: V 3.0.0 (b259)" printed at boot
//   2. steamapps/appmanifest_294420.acf — buildid + betakey written by SteamCMD
//   3. The active serverconfig.xml      — SandboxCode ⇒ V3, legacy keys ⇒ V2
//
// A log line beats the manifest (the manifest can lag a botched update), and
// both beat config-shape inference.

public enum SevenDaysGeneration
{
    Unknown,
    V2,     // pre-SandboxCode: gameplay rules as individual XML properties
    V3      // SandboxCode era: gameplay rules encoded in one property
}

public sealed class SevenDaysToDieInstallInfo
{
    public bool ExecutableFound { get; init; }
    public bool ManifestFound { get; init; }

    /// <summary>Steam build id from appmanifest_294420.acf, or null.</summary>
    public string? BuildId { get; init; }

    /// <summary>Steam branch (betakey) from the manifest; "stable" when absent.</summary>
    public string Branch { get; init; } = "stable";

    /// <summary>Game version parsed from server logs, e.g. "3.0.0", or null.</summary>
    public string? GameVersion { get; init; }

    /// <summary>Build number parsed from server logs, e.g. "259", or null.</summary>
    public string? GameBuild { get; init; }

    public SevenDaysGeneration Generation { get; init; } = SevenDaysGeneration.Unknown;

    /// <summary>Which file provided the version signal, for the UI/diagnostics.</summary>
    public string VersionSource { get; init; } = "none";

    public string Summary
    {
        get
        {
            var parts = new List<string>();
            if (GameVersion != null)
                parts.Add($"V{GameVersion}" + (GameBuild != null ? $" (b{GameBuild})" : ""));
            else if (BuildId != null)
                parts.Add($"build {BuildId}");
            if (!Branch.Equals("stable", StringComparison.OrdinalIgnoreCase))
                parts.Add(Branch);
            if (Generation != SevenDaysGeneration.Unknown)
                parts.Add(Generation.ToString());
            return parts.Count > 0 ? string.Join(" · ", parts) : "version unknown";
        }
    }
}

public sealed class SevenDaysToDieVersionService
{
    // "Version: V 3.0.0 (b259)" / "Version: Alpha 21.2 (b30)" — tolerant of both.
    private static readonly Regex _logVersionPattern = new(
        @"Version:\s*(?:V\s*|Alpha\s*)?(?<ver>\d+(?:\.\d+){0,3})\s*\(b(?<build>\d+)\)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex _acfBuildIdPattern = new(
        "\"buildid\"\\s*\"(?<id>\\d+)\"", RegexOptions.Compiled);

    private static readonly Regex _acfBetaKeyPattern = new(
        "\"betakey\"\\s*\"(?<key>[^\"]*)\"", RegexOptions.Compiled);

    /// <summary>
    /// Inspects an installation directory and returns everything that can be
    /// determined from the files on disk. Never throws for missing files.
    /// </summary>
    public async Task<SevenDaysToDieInstallInfo> DetectAsync(string installPath)
    {
        var executableFound = File.Exists(Path.Combine(installPath, "7DaysToDieServer.exe"));

        // 1. Steam manifest
        string? buildId = null;
        var branch = "stable";
        var manifestPath = Path.Combine(installPath, "steamapps", "appmanifest_294420.acf");
        var manifestFound = File.Exists(manifestPath);
        if (manifestFound)
        {
            var acf = await File.ReadAllTextAsync(manifestPath);
            var buildMatch = _acfBuildIdPattern.Match(acf);
            if (buildMatch.Success)
                buildId = buildMatch.Groups["id"].Value;

            var betaMatch = _acfBetaKeyPattern.Match(acf);
            if (betaMatch.Success && !string.IsNullOrWhiteSpace(betaMatch.Groups["key"].Value))
                branch = betaMatch.Groups["key"].Value;
        }

        // 2. Newest server log
        string? gameVersion = null, gameBuild = null;
        var versionSource = "none";
        var logLine = FindVersionLineInLogs(Path.Combine(installPath, "logs"));
        if (logLine != null)
        {
            var m = _logVersionPattern.Match(logLine);
            if (m.Success)
            {
                gameVersion = m.Groups["ver"].Value;
                gameBuild = m.Groups["build"].Value;
                versionSource = "server log";
            }
        }
        if (versionSource == "none" && buildId != null)
            versionSource = "steam manifest";

        // 3. Config-shape inference
        var generation = SevenDaysGeneration.Unknown;
        if (gameVersion != null && int.TryParse(gameVersion.Split('.')[0], out var major))
        {
            generation = major >= 3 ? SevenDaysGeneration.V3 : SevenDaysGeneration.V2;
        }
        else
        {
            var configPath = Path.Combine(installPath, "serverconfig.xml");
            if (File.Exists(configPath))
            {
                try
                {
                    var doc = await ServerConfigXmlDocument.LoadAsync(configPath);
                    generation = ClassifyGeneration(doc.GetAllPropertyNames());
                }
                catch (Exception)
                {
                    // Malformed config — leave Unknown; the drift/validation layers report it.
                }
            }
        }

        return new SevenDaysToDieInstallInfo
        {
            ExecutableFound = executableFound,
            ManifestFound = manifestFound,
            BuildId = buildId,
            Branch = branch,
            GameVersion = gameVersion,
            GameBuild = gameBuild,
            Generation = generation,
            VersionSource = versionSource
        };
    }

    /// <summary>
    /// Classifies a config's generation from the property names it contains:
    /// SandboxCode ⇒ V3, any legacy gameplay key ⇒ V2, neither ⇒ Unknown.
    /// </summary>
    public static SevenDaysGeneration ClassifyGeneration(IEnumerable<string> propertyNames)
    {
        var names = new HashSet<string>(propertyNames, StringComparer.OrdinalIgnoreCase);
        if (names.Contains("SandboxCode"))
            return SevenDaysGeneration.V3;
        if (SevenDaysToDieConfigService.LegacyV2GameplayKeys.Overlaps(names))
            return SevenDaysGeneration.V2;
        return SevenDaysGeneration.Unknown;
    }

    /// <summary>Extracts the version line from the newest log that has one.</summary>
    public static string? FindVersionLineInLogs(string logsDirectory)
    {
        if (!Directory.Exists(logsDirectory))
            return null;

        try
        {
            var logs = Directory.EnumerateFiles(logsDirectory, "*.log")
                .Select(f => new FileInfo(f))
                .OrderByDescending(f => f.LastWriteTimeUtc)
                .Take(5); // the version line is near the top of every boot log

            foreach (var log in logs)
            {
                // ponytail: read only the first 200 lines — the banner is always there,
                // and server logs can grow to hundreds of MB.
                using var reader = new StreamReader(log.FullName);
                for (var i = 0; i < 200 && reader.ReadLine() is { } line; i++)
                {
                    if (_logVersionPattern.IsMatch(line))
                        return line;
                }
            }
        }
        catch (IOException) { /* log locked by a running server — fall back to manifest */ }
        catch (UnauthorizedAccessException) { }

        return null;
    }
}
