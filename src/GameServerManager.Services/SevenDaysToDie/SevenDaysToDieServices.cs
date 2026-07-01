using System.Text;
using System.Xml;
using System.Xml.Linq;
using GameServerManager.Core.Models;

namespace GameServerManager.Services.SevenDaysToDie;

// ─── SteamCMD argument builder ────────────────────────────────────────────────
//
// Builds the +app_update command for the 7 Days to Die dedicated server (294420).
// Supports anonymous login (works for the public dedicated server package) and
// optional Steam credentials for branches that require ownership or auth.
//
public sealed class SevenDaysToDieSteamCmdService
{
    public const int AppId = 294420;

    /// <summary>
    /// Builds the SteamCMD argument string for install or update.
    /// </summary>
    /// <param name="installPath">Absolute path to the server installation directory.</param>
    /// <param name="validate">Append "validate" to verify file integrity.</param>
    /// <param name="branch">Steam branch name, or null/empty for the default stable branch.</param>
    /// <param name="username">Steam username, or null for anonymous login.</param>
    public string BuildArguments(string installPath, bool validate = false,
        string? branch = null, string? username = null)
    {
        var login = string.IsNullOrWhiteSpace(username) ? "anonymous" : username;

        var update = $"+app_update {AppId}";
        if (!string.IsNullOrWhiteSpace(branch) &&
            !branch.Equals("stable", StringComparison.OrdinalIgnoreCase) &&
            !branch.Equals("public", StringComparison.OrdinalIgnoreCase))
        {
            update += $" -beta \"{branch}\"";
        }

        if (validate)
        {
            update += " validate";
        }

        return $"+force_install_dir \"{installPath}\" +login {login} {update} +quit";
    }

    /// <summary>
    /// Returns a display-safe version of the arguments with credentials redacted.
    /// </summary>
    public string BuildArgumentsForDisplay(string installPath, bool validate = false,
        string? branch = null, bool hasCredentials = false)
    {
        var login = hasCredentials ? "****" : "anonymous";
        var update = $"+app_update {AppId}";
        if (!string.IsNullOrWhiteSpace(branch) &&
            !branch.Equals("stable", StringComparison.OrdinalIgnoreCase))
        {
            update += $" -beta \"{branch}\"";
        }

        if (validate)
        {
            update += " validate";
        }

        return $"+force_install_dir \"{installPath}\" +login {login} {update} +quit";
    }
}

// ─── serverconfig.xml document ────────────────────────────────────────────────
//
// Reads and writes 7 Days to Die's XML configuration file while preserving the
// original structure, unknown <property> elements, comments, and whitespace.
// The file format is:
//
//   <?xml version="1.0" encoding="UTF-8" standalone="true"?>
//   <ServerSettings>
//     <!-- comment -->
//     <property name="ServerName" value="My Server" />
//     ...
//   </ServerSettings>
//
public sealed class ServerConfigXmlDocument
{
    private XDocument _doc;

    private ServerConfigXmlDocument(XDocument doc)
    {
        _doc = doc;
    }

    /// <summary>Parses serverconfig.xml from a string.</summary>
    public static ServerConfigXmlDocument Parse(string xml)
    {
        var doc = XDocument.Parse(xml, LoadOptions.PreserveWhitespace);
        return new ServerConfigXmlDocument(doc);
    }

    /// <summary>Loads serverconfig.xml from disk, or returns an empty document.</summary>
    public static async Task<ServerConfigXmlDocument> LoadAsync(string path)
    {
        if (!File.Exists(path))
        {
            return CreateDefault();
        }

        var text = await File.ReadAllTextAsync(path, Encoding.UTF8);
        return Parse(text);
    }

    /// <summary>Creates a minimal valid serverconfig.xml with no properties.</summary>
    public static ServerConfigXmlDocument CreateDefault()
    {
        var doc = new XDocument(
            new XDeclaration("1.0", "UTF-8", "true"),
            new XElement("ServerSettings")
        );
        return new ServerConfigXmlDocument(doc);
    }

    /// <summary>Gets the value of a property by name, or null if not present.</summary>
    public string? GetValue(string name)
    {
        return FindElement(name)?.Attribute("value")?.Value;
    }

    /// <summary>Gets the value of a property, falling back to <paramref name="fallback"/>.</summary>
    public string GetValue(string name, string fallback)
        => GetValue(name) ?? fallback;

    /// <summary>
    /// Sets the value of an existing property or appends a new one.
    /// Does not modify properties whose names it does not recognise — preserves
    /// unknown properties added by future server versions.
    /// </summary>
    public void SetValue(string name, string value)
    {
        var element = FindElement(name);
        if (element != null)
        {
            element.SetAttributeValue("value", value);
        }
        else
        {
            var root = RequireRoot();
            // Add a text node for consistent indentation before the new element.
            root.Add(new XText("\n  "));
            root.Add(new XElement("property",
                new XAttribute("name", name),
                new XAttribute("value", value)));
            root.Add(new XText("\n"));
        }
    }

    /// <summary>Removes a property if it exists. Unknown properties are unaffected.</summary>
    public void RemoveValue(string name)
    {
        FindElement(name)?.Remove();
    }

    /// <summary>Returns all property names present in the document.</summary>
    public IReadOnlyList<string> GetAllPropertyNames()
    {
        return RequireRoot()
            .Elements("property")
            .Select(e => e.Attribute("name")?.Value)
            .Where(n => n != null)
            .Cast<string>()
            .ToList();
    }

    /// <summary>Renders the document to UTF-8 bytes ready for disk.</summary>
    private byte[] RenderBytes()
    {
        using var ms = new MemoryStream();
        // Dispose the writer explicitly before reading from the MemoryStream so the
        // buffered end-of-document bytes are flushed before ms.ToArray() is called.
        using (var xw = XmlWriter.Create(ms, new XmlWriterSettings
        {
            Indent = true,
            IndentChars = "  ",
            OmitXmlDeclaration = false,
            Encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
            NewLineHandling = NewLineHandling.Replace
        }))
        {
            _doc.Save(xw);
        }

        return ms.ToArray();
    }

    /// <summary>Renders the document back to a UTF-8 XML string (no BOM).</summary>
    public string Render() => Encoding.UTF8.GetString(RenderBytes());

    /// <summary>
    /// Atomically writes the document to <paramref name="path"/>.
    /// Writes to a temp file first, backs up the original if requested, then
    /// renames — so the file is never partially written.
    /// </summary>
    public async Task SaveAtomicAsync(string path, bool createBackup = true)
    {
        var bytes = RenderBytes();

        if (createBackup && File.Exists(path))
        {
            var backupPath = path + $".{DateTime.UtcNow:yyyyMMdd_HHmmss}.bak";
            File.Copy(path, backupPath, overwrite: true);
        }

        var tempPath = path + ".tmp";
        await File.WriteAllBytesAsync(tempPath, bytes);

        // Validate before replacing the original.
        XDocument.Load(tempPath); // throws XmlException on malformed XML

        File.Move(tempPath, path, overwrite: true);
    }

    // ── internals ────────────────────────────────────────────────────────────────

    private XElement? FindElement(string name)
        => RequireRoot()
           .Elements("property")
           .FirstOrDefault(e =>
               string.Equals(e.Attribute("name")?.Value, name, StringComparison.OrdinalIgnoreCase));

    private XElement RequireRoot()
        => _doc.Root ?? throw new InvalidOperationException("serverconfig.xml has no root element.");
}

// ─── Config service ───────────────────────────────────────────────────────────
//
// Reads and writes 7 Days to Die server configuration, mapping between the
// flat ServerProfile.Settings dictionary and serverconfig.xml properties.
//
// V3 note: SandboxCode is treated as a first-class string property. Legacy V2
// gameplay properties (GameDifficulty, ZombieMove, etc.) are preserved in the
// Settings dictionary under their original names so they can be shown in a
// migration assistant, but this service never writes V2-only properties that
// have been superseded by SandboxCode back into a V3 server's config file.
//
public sealed class SevenDaysToDieConfigService
{
    // Properties managed through the launcher flags or the Settings dictionary,
    // NOT written back to serverconfig.xml (they would be overridden on start).
    // Also includes app-internal metadata that lives in profile.Settings but has
    // no meaning to the game itself — 7 Days to Die aborts startup on any
    // unrecognised <property>, so leaking one of these bricks the server.
    private static readonly HashSet<string> _launchOnlyKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "UserDataFolder",  // controlled via -UserDataFolder launch flag
        "SteamBranch",
        "CustomSteamBranch",
        // The Add/Edit Server wizard (AddServerWizardViewModel.CreateProfile) stores
        // its own form fields into profile.Settings under these keys, for every
        // game type. None of them are real 7 Days to Die config properties (the
        // real ones are ServerDescription, TelnetPassword, etc.) — keep this list
        // in sync with that dictionary.
        "ipAddress",           // app-side query/display host
        "description",         // app-side notes field (real key is ServerDescription)
        "tags",                // app-side server tags
        "serverPath",          // app-side wizard field
        "saveDirectory",       // app-side wizard field
        "backupDirectory",     // app-side wizard field
        "cpuLimitPercent",     // app-side wizard field
        "autoRestart",         // app-side wizard field
        "rconPassword",        // app-side RCON UI field (real key is TelnetPassword)
        "imported",            // app-side import marker
        "originalImportPath",  // app-side import marker
        "importMode",          // app-side import marker
        // ServerAdminPassword was never a real serverconfig.xml property (verified
        // against the current V3 property reference) and has been removed from
        // SettingsDefinitions. Kept here so a profile that already saved one gets
        // it silently stripped from serverconfig.xml instead of crashing on boot.
        "ServerAdminPassword",
        // ControlPanelEnabled/Port/Password were retired back in Alpha 21, replaced
        // by WebDashboardEnabled/Port/Url. Same self-heal treatment.
        "ControlPanelEnabled",
        "ControlPanelPort",
        "ControlPanelPassword",
        // SaveGameFolder is rejected as "Unknown config option" by the current
        // V3.0.0 (b259) build, confirmed directly from a live server's own log —
        // more authoritative than general docs, which may describe an older build.
        "SaveGameFolder"
    };

    // Legacy V2 gameplay properties superseded by SandboxCode in V3.
    // We read them (to aid migration) but never write them back to a V3 config.
    internal static readonly HashSet<string> LegacyV2GameplayKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "GameDifficulty", "BlockDamagePlayer", "BlockDamageAI", "BlockDamageAIBM",
        "XPMultiplier", "DayNightLength", "DayLightLength", "BiomeProgression",
        "StormFreq", "DeathPenalty", "DropOnDeath", "DropOnQuit", "JarRefund",
        "EnemySpawnMode", "EnemyDifficulty", "ZombieFeralSense",
        "ZombieMove", "ZombieMoveNight", "ZombieFeralMove", "ZombieBMMove",
        "AISmellMode", "BloodMoonFrequency", "BloodMoonRange", "BloodMoonWarning",
        "BloodMoonEnemyCount", "LootAbundance", "LootRespawnDays",
        "AirDropFrequency", "AirDropMarker", "QuestProgressionDailyLimit"
    };

    /// <summary>
    /// Writes current profile settings into an existing or new serverconfig.xml.
    /// Preserves unknown properties. Never writes launch-only or V2-only keys.
    /// </summary>
    public async Task SaveAsync(ServerProfile profile, string configPath, bool createBackup = true)
    {
        var doc = await ServerConfigXmlDocument.LoadAsync(configPath);

        // ServerName / ServerDescription come from the profile-level fields.
        doc.SetValue("ServerName", profile.ServerName);
        if (!string.IsNullOrWhiteSpace(profile.Password))
        {
            doc.SetValue("ServerPassword", profile.Password);
        }

        if (profile.MaxPlayers > 0)
        {
            doc.SetValue("ServerMaxPlayerCount", profile.MaxPlayers.ToString());
        }

        // Remaining settings from the Settings dictionary.
        foreach (var (key, value) in profile.Settings)
        {
            if (_launchOnlyKeys.Contains(key))
            {
                // Also strip it if an older version of the app already wrote it to
                // disk — an unrecognised <property> aborts 7DtD's startup entirely.
                doc.RemoveValue(key);
                continue;
            }

            // Don't write V2 gameplay keys into V3 configs when SandboxCode is set.
            // Also strip one already on disk (e.g. from an imported V2 config) —
            // V3 rejects all of these as unrecognised properties.
            if (IsV3Config(profile) && LegacyV2GameplayKeys.Contains(key))
            {
                doc.RemoveValue(key);
                continue;
            }

            doc.SetValue(key, value);
        }

        // Port: prefer the Ports list entry over the Settings dict.
        var gamePort = profile.Ports.FirstOrDefault(p =>
            p.Name.Equals("Game", StringComparison.OrdinalIgnoreCase))?.Port;
        if (gamePort.HasValue)
        {
            doc.SetValue("ServerPort", gamePort.Value.ToString());
        }

        await doc.SaveAtomicAsync(configPath, createBackup);
    }

    /// <summary>
    /// Loads serverconfig.xml properties into profile.Settings.
    /// Properties not in the known schema end up in Settings under their original
    /// names so they remain editable through the Advanced section.
    /// </summary>
    public async Task LoadAsync(ServerProfile profile, string configPath)
    {
        if (!File.Exists(configPath))
        {
            return;
        }

        var doc = await ServerConfigXmlDocument.LoadAsync(configPath);

        var serverName = doc.GetValue("ServerName");
        if (!string.IsNullOrWhiteSpace(serverName))
        {
            profile.ServerName = serverName;
        }

        var password = doc.GetValue("ServerPassword");
        if (!string.IsNullOrWhiteSpace(password))
        {
            profile.Password = password;
        }

        if (int.TryParse(doc.GetValue("ServerMaxPlayerCount"), out var maxPlayers) && maxPlayers > 0)
        {
            profile.MaxPlayers = maxPlayers;
        }

        foreach (var name in doc.GetAllPropertyNames())
        {
            // Already mapped above.
            if (name.Equals("ServerName", StringComparison.OrdinalIgnoreCase) ||
                name.Equals("ServerPassword", StringComparison.OrdinalIgnoreCase) ||
                name.Equals("ServerMaxPlayerCount", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var value = doc.GetValue(name);
            if (value != null)
            {
                profile.Settings[name] = value;
            }
        }
    }

    /// <summary>
    /// Creates a minimal serverconfig.xml if one does not already exist.
    /// Never overwrites an existing file.
    /// </summary>
    public static async Task EnsureConfigExistsAsync(string configPath, string serverName = "My 7 Days Server")
    {
        if (File.Exists(configPath))
        {
            return;
        }

        var doc = ServerConfigXmlDocument.CreateDefault();
        doc.SetValue("ServerName", serverName);
        doc.SetValue("ServerDescription", "Powered by Nexus Server Manager");
        doc.SetValue("ServerPort", "26900");
        doc.SetValue("ServerMaxPlayerCount", "8");
        doc.SetValue("GameWorld", "Navezgane");
        doc.SetValue("GameName", "My Game");
        doc.SetValue("EACEnabled", "true");
        doc.SetValue("ServerAllowCrossplay", "false");

        Directory.CreateDirectory(Path.GetDirectoryName(configPath)!);
        await doc.SaveAtomicAsync(configPath, createBackup: false);
    }

    private static bool IsV3Config(ServerProfile profile)
        => profile.Settings.ContainsKey("SandboxCode") &&
           !string.IsNullOrWhiteSpace(profile.Settings["SandboxCode"]);
}

// ─── Launch command builder ───────────────────────────────────────────────────
//
// Builds the full launch command for display (masked) or for process start.
//
public sealed class SevenDaysToDieLaunchBuilder
{
    public SevenDaysToDieLaunchPreview Build(ServerProfile profile, bool revealPasswords = false)
    {
        var installPath = profile.InstallPath;
        var executablePath = !string.IsNullOrWhiteSpace(profile.ExecutablePath)
            ? profile.ExecutablePath
            : Path.Combine(installPath, "7DaysToDieServer.exe");

        var configPath = Path.Combine(installPath, "serverconfig.xml");
        var userDataFolder = profile.Settings.TryGetValue("UserDataFolder", out var udf) && !string.IsNullOrWhiteSpace(udf)
            ? udf
            : Path.Combine(installPath, "UserData");

        var logPath = Path.Combine(installPath, "logs", "server.log");

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

        var arguments = string.Join(' ', args);
        var commandLine = $"\"{executablePath}\" {arguments}";

        return new SevenDaysToDieLaunchPreview
        {
            ExecutablePath = executablePath,
            Arguments = arguments,
            CommandLine = commandLine,
            WorkingDirectory = installPath
        };
    }
}

public sealed class SevenDaysToDieLaunchPreview
{
    public string ExecutablePath { get; init; } = string.Empty;
    public string Arguments { get; init; } = string.Empty;
    public string CommandLine { get; init; } = string.Empty;
    public string WorkingDirectory { get; init; } = string.Empty;
}

// ─── Profile validator ────────────────────────────────────────────────────────
//
// Validates a ServerProfile for 7 Days to Die before install or launch.
//
public sealed class SevenDaysToDieValidator
{
    public SevenDaysToDieValidationResult Validate(ServerProfile profile)
    {
        var result = new SevenDaysToDieValidationResult();

        if (string.IsNullOrWhiteSpace(profile.InstallPath))
        {
            result.AddError("Installation path is required.");
        }

        if (string.IsNullOrWhiteSpace(profile.ServerName))
        {
            result.AddError("Server name is required.");
        }

        // Port range check: 7 Days to Die needs 4 consecutive ports.
        var gamePort = profile.Ports.FirstOrDefault(p =>
            p.Name.Equals("Game", StringComparison.OrdinalIgnoreCase))?.Port ?? 26900;

        if (gamePort is < 1 or > 65530)
        {
            result.AddError($"Game port {gamePort} is invalid. Ports must be between 1 and 65530 (4 consecutive ports are used).");
        }

        // MaxPlayers for crossplay is limited.
        if (profile.Settings.TryGetValue("ServerAllowCrossplay", out var crossplay) &&
            bool.TryParse(crossplay, out var crossplayEnabled) && crossplayEnabled &&
            profile.MaxPlayers > 8)
        {
            result.AddWarning($"Crossplay requires ServerMaxPlayerCount ≤ 8 (currently {profile.MaxPlayers}).");
        }

        // EAC must be enabled for crossplay.
        if (profile.Settings.TryGetValue("ServerAllowCrossplay", out var cp) &&
            bool.TryParse(cp, out var cpEnabled) && cpEnabled &&
            profile.Settings.TryGetValue("EACEnabled", out var eac) &&
            bool.TryParse(eac, out var eacEnabled) && !eacEnabled)
        {
            result.AddError("EAC must be enabled when crossplay is active.");
        }

        // Warn about V2 gameplay settings without SandboxCode when server may be V3.
        var hasLegacyKeys = SevenDaysToDieConfigService.LegacyV2GameplayKeys
            .Any(key => profile.Settings.ContainsKey(key));
        var hasSandboxCode = profile.Settings.TryGetValue("SandboxCode", out var sc) &&
                             !string.IsNullOrWhiteSpace(sc);

        if (hasLegacyKeys && !hasSandboxCode)
        {
            result.AddWarning(
                "Legacy V2 gameplay settings are present but no SandboxCode is set. " +
                "If this is a V3 server, use the Sandbox editor to configure gameplay settings.");
        }

        // WorldGenSize must be a recognised value when GameWorld is RWG.
        if (profile.Settings.TryGetValue("GameWorld", out var world) &&
            world.Equals("RWG", StringComparison.OrdinalIgnoreCase))
        {
            if (profile.Settings.TryGetValue("WorldGenSize", out var sizeStr) &&
                int.TryParse(sizeStr, out var size) &&
                size != 6144 && size != 8192 && size != 10240)
            {
                result.AddWarning($"WorldGenSize {size} is not a standard value. Supported sizes are 6144, 8192, and 10240.");
            }
        }

        return result;
    }
}

public sealed class SevenDaysToDieValidationResult
{
    private readonly List<string> _errors = new();
    private readonly List<string> _warnings = new();

    public IReadOnlyList<string> Errors => _errors;
    public IReadOnlyList<string> Warnings => _warnings;
    public bool IsValid => _errors.Count == 0;
    public bool HasWarnings => _warnings.Count > 0;

    internal void AddError(string message) => _errors.Add(message);
    internal void AddWarning(string message) => _warnings.Add(message);
}

// ─── SandboxCode helpers ─────────────────────────────────────────────────────
//
// In 7 Days to Die V3, gameplay settings are encoded in the SandboxCode string.
// This class provides round-trip parsing for the code so the UI can display
// and edit individual settings without discarding unknown option IDs.
//
// The encoding format used by the game is a proprietary base-62–like scheme.
// Until the game exposes a stable public specification, this implementation
// stores the code as an opaque string but provides helpers for the UI to
// interact with it through the server's own introspection commands (getsandboxoptions).
//
public static class SandboxCodeHelpers
{
    // Sensitive keys that must never appear in log output or exported diagnostics.
    private static readonly HashSet<string> _sensitivePropertyNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "ServerPassword", "ServerAdminPassword", "ControlPanelPassword",
        "TelnetPassword", "AdminPassword"
    };

    /// <summary>
    /// Returns true if the property name contains sensitive data that must be masked.
    /// </summary>
    public static bool IsSensitive(string propertyName)
        => _sensitivePropertyNames.Contains(propertyName);

    /// <summary>
    /// Returns a copy of <paramref name="xml"/> with sensitive property values replaced by ********.
    /// </summary>
    public static string MaskSensitiveValues(string xml)
    {
        if (string.IsNullOrEmpty(xml))
        {
            return xml;
        }

        try
        {
            var doc = XDocument.Parse(xml);
            foreach (var element in doc.Descendants("property"))
            {
                var name = element.Attribute("name")?.Value;
                if (name != null && IsSensitive(name))
                {
                    element.SetAttributeValue("value", "********");
                }
            }

            return doc.ToString();
        }
        catch (XmlException)
        {
            // If the XML is malformed, return the original rather than crashing.
            return xml;
        }
    }
}
