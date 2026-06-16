using System.IO.Compression;
using System.Net.NetworkInformation;
using System.Text;
using GameServerManager.Core.Models;

namespace GameServerManager.Services.Palworld;

// ── Profile Mapper ────────────────────────────────────────────────────────────

public sealed class PalworldProfileMapper
{
    public PalworldServerProfile FromServerProfile(ServerProfile profile)
    {
        var pal = new PalworldServerProfile();

        pal.Basic.ServerName = Val(profile.ServerName, "Default Palworld Server");
        pal.Basic.ServerDescription = profile.Notes;
        pal.Basic.InstallPath = profile.InstallPath;
        pal.Basic.InstanceName = Val(profile.ProfileName, "Palworld");
        pal.Basic.ServerPlayerMaxNum = profile.MaxPlayers <= 0 ? 32 : profile.MaxPlayers;
        pal.Basic.ServerPassword = profile.Password;
        pal.Basic.AdminPassword = profile.AdminPassword;
        pal.Basic.AutoStart = Bool(profile, "AutoStart");
        pal.Basic.AutoRestartOnCrash = Bool(profile, "AutoRestartOnCrash", true);
        pal.Basic.RestartSchedule = profile.RestartSchedule ?? string.Empty;

        pal.Network.GamePort = Port(profile, "Game", 8211);
        pal.Network.PublicPort = Int(profile, "PublicPort", 8211);
        pal.Network.PublicIP = Get(profile, "PublicIP");
        pal.Network.RCONEnabled = Bool(profile, "RCONEnabled");
        pal.Network.RCONPort = Port(profile, "RCON", 25575);
        pal.Network.RESTAPIEnabled = Bool(profile, "RESTAPIEnabled");
        pal.Network.RESTAPIPort = Int(profile, "RESTAPIPort", 8212);
        pal.Network.PublicLobbyEnabled = Bool(profile, "PublicLobbyEnabled");
        pal.Network.LANOnly = Bool(profile, "LANOnly");
        pal.Network.CrossplayPlatforms = Get(profile, "CrossplayPlatforms", "(\"Steam\",\"Xbox\")");

        pal.Launch.PerformanceMode = Bool(profile, "PerformanceMode");
        pal.Launch.WorkerThreadCount = Int(profile, "WorkerThreadCount", 0);
        pal.Launch.NoMods = Bool(profile, "NoMods");
        pal.Launch.LogFormat = Get(profile, "LogFormat", "Text");
        pal.Launch.WorkshopDir = Get(profile, "WorkshopDir");
        pal.Launch.CustomLaunchArguments = profile.LaunchArgs;

        pal.Mods.GlobalModsEnabled = Bool(profile, "GlobalModsEnabled", true);
        pal.Mods.WorkshopRootDir = Get(profile, "WorkshopRootDir");
        pal.Mods.NoModsLaunchFlag = Bool(profile, "NoModsLaunchFlag");

        pal.Backup.ScheduledBackupsEnabled = profile.AutoBackupEnabled;
        pal.Backup.UseBuiltInBackup = Bool(profile, "UseBuiltInBackup", true);

        HydratePaths(pal);
        return pal;
    }

    public void HydratePaths(PalworldServerProfile profile)
    {
        var root = profile.Basic.InstallPath;
        profile.Paths.ServerRootPath = root;
        profile.Paths.ExecutablePath = Path.Combine(root, PalworldServerProfile.DefaultExecutableRelativePath);
        profile.Paths.DefaultPalWorldSettingsPath = Path.Combine(root, PalworldServerProfile.DefaultPalWorldSettingsRelativePath);
        profile.Paths.PalWorldSettingsPath = Path.Combine(root, PalworldServerProfile.ActiveConfigRelativePath.Replace('/', Path.DirectorySeparatorChar));
        profile.Paths.ConfigFolderPath = Path.GetDirectoryName(profile.Paths.PalWorldSettingsPath)!;
        profile.Paths.SaveGamesPath = Path.Combine(root, "Pal", "Saved", "SaveGames");
        profile.Paths.LogsPath = Path.Combine(root, "Pal", "Saved", "Logs");
        profile.Paths.ModsPath = Path.Combine(root, "Mods");
        profile.Paths.PalModSettingsPath = Path.Combine(root, PalworldServerProfile.PalModSettingsRelativePath.Replace('/', Path.DirectorySeparatorChar));
        profile.Paths.WorkshopModsPath = Path.Combine(root, "Mods", "Workshop");
        profile.Paths.ManagedModsPath = Path.Combine(root, "Mods", "ManagedMods");
        profile.Paths.BackupPath = Path.Combine(root, "Backups");
    }

    private static string Get(ServerProfile p, string key, string fallback = "")
        => p.Settings.TryGetValue(key, out var v) ? v : fallback;

    private static string Val(string v, string fallback)
        => string.IsNullOrWhiteSpace(v) ? fallback : v;

    private static bool Bool(ServerProfile p, string key, bool fallback = false)
        => p.Settings.TryGetValue(key, out var v) && bool.TryParse(v, out var b) ? b : fallback;

    private static int Int(ServerProfile p, string key, int fallback)
        => p.Settings.TryGetValue(key, out var v) && int.TryParse(v, out var n) ? n : fallback;

    private static int Port(ServerProfile p, string name, int fallback)
        => p.Ports.FirstOrDefault(pp => pp.Name.Equals(name, StringComparison.OrdinalIgnoreCase))?.Port ?? fallback;
}

// ── SteamCMD ──────────────────────────────────────────────────────────────────

public sealed class PalworldSteamCmdService
{
    public string BuildInstallOrUpdateArguments(string installPath, bool validate = true)
    {
        var validateArg = validate ? " validate" : string.Empty;
        return $"+force_install_dir \"{installPath}\" +login anonymous +app_update {PalworldServerProfile.SteamCmdAppId}{validateArg} +quit";
    }
}

// ── Launch Builder ────────────────────────────────────────────────────────────

public sealed class PalworldLaunchBuilder
{
    public PalworldLaunchPreview Build(PalworldServerProfile profile, bool revealPasswords = false)
    {
        var exe = profile.Paths.ExecutablePath;
        var args = new List<string>
        {
            $"-port={profile.Network.GamePort}",
            $"-players={profile.Basic.ServerPlayerMaxNum}"
        };

        if (profile.Network.PublicLobbyEnabled)
            args.Add("-publiclobby");

        if (!string.IsNullOrWhiteSpace(profile.Network.PublicIP))
            args.Add($"-publicip={profile.Network.PublicIP}");

        if (profile.Network.PublicPort != profile.Network.GamePort)
            args.Add($"-publicport={profile.Network.PublicPort}");

        var logFormat = string.IsNullOrWhiteSpace(profile.Launch.LogFormat) ? "Text" : profile.Launch.LogFormat;
        args.Add($"-logformat={logFormat}");

        if (profile.Launch.PerformanceMode)
        {
            args.Add("-useperfthreads");
            args.Add("-NoAsyncLoadingThread");
            args.Add("-UseMultithreadForDS");
        }

        if (profile.Launch.WorkerThreadCount > 0)
            args.Add($"-NumberOfWorkerThreadsServer={profile.Launch.WorkerThreadCount}");

        if (!string.IsNullOrWhiteSpace(profile.Launch.WorkshopDir))
            args.Add($"-workshopdir=\"{profile.Launch.WorkshopDir}\"");

        if (profile.Launch.NoMods || profile.Mods.NoModsLaunchFlag)
            args.Add("-NoMods");

        if (!string.IsNullOrWhiteSpace(profile.Launch.CustomLaunchArguments))
            args.Add(profile.Launch.CustomLaunchArguments);

        var arguments = string.Join(" ", args);
        return new PalworldLaunchPreview(exe, profile.Basic.InstallPath, arguments, $"\"{exe}\" {arguments}");
    }

    public static string Mask(string value)
        => string.IsNullOrEmpty(value) ? string.Empty : "********";
}

public sealed record PalworldLaunchPreview(
    string ExecutablePath, string WorkingDirectory, string Arguments, string CommandLine);

// ── Config Service ─────────────────────────────────────────────────────────────

public sealed class PalworldConfigService
{
    // Reads PalWorldSettings.ini into the profile's OptionSettings maps.
    public async Task<PalworldConfigLoadResult> LoadAsync(PalworldServerProfile profile)
    {
        var path = profile.Paths.PalWorldSettingsPath;
        var defaultPath = profile.Paths.DefaultPalWorldSettingsPath;
        var warnings = new List<string>();

        if (!File.Exists(path))
        {
            if (File.Exists(defaultPath))
            {
                warnings.Add(
                    $"PalWorldSettings.ini not found. The template at DefaultPalWorldSettings.ini is read-only — " +
                    $"it will not apply until you copy it to: {path}");
            }
            else
            {
                warnings.Add($"PalWorldSettings.ini not found at: {path}. The server must be started once to create it.");
            }
            return new PalworldConfigLoadResult(false, warnings, PalworldConfigDocument.CreateEmpty());
        }

        var doc = await PalworldConfigDocument.LoadAsync(path);
        SyncFromDocument(doc, profile);
        return new PalworldConfigLoadResult(true, warnings, doc);
    }

    // Copies DefaultPalWorldSettings.ini → active config path (safe first-time setup).
    public async Task<bool> CopyTemplateToActiveConfigAsync(PalworldServerProfile profile)
    {
        var src = profile.Paths.DefaultPalWorldSettingsPath;
        var dst = profile.Paths.PalWorldSettingsPath;
        if (!File.Exists(src)) return false;
        if (File.Exists(dst)) return false; // safety: never overwrite

        Directory.CreateDirectory(profile.Paths.ConfigFolderPath);
        await Task.Run(() => File.Copy(src, dst, overwrite: false));
        return true;
    }

    // Saves the profile's OptionSettings back to PalWorldSettings.ini.
    public async Task<PalworldConfigSaveResult> SaveAsync(PalworldServerProfile profile, bool createBackup = true)
    {
        var validator = new PalworldValidator();
        var validation = validator.Validate(profile);
        if (validation.Errors.Count > 0)
            throw new InvalidOperationException(string.Join(Environment.NewLine, validation.Errors));

        var path = profile.Paths.PalWorldSettingsPath;

        // Load existing document to preserve unknown settings
        var doc = await PalworldConfigDocument.LoadAsync(path);

        // Apply known settings from profile
        SyncToDocument(profile, doc);

        // Apply known-settings dictionary
        foreach (var kv in profile.OptionSettings.KnownSettings)
            doc.SetRaw(kv.Key, kv.Value);

        // Unknown settings are already in the document from LoadAsync — no action needed

        await doc.SaveAsync(path, createBackup);
        profile.Advanced.MarkServerNeedsRestart = true;
        profile.Advanced.LastConfigSave = DateTime.UtcNow;
        return new PalworldConfigSaveResult(path, createBackup);
    }

    // ── Sync helpers ──────────────────────────────────────────────────────────

    // Reads core server identity settings from the document into the profile model.
    private static void SyncFromDocument(PalworldConfigDocument doc, PalworldServerProfile profile)
    {
        profile.Basic.ServerName = doc.GetString("ServerName", "Default Palworld Server");
        profile.Basic.ServerDescription = doc.GetString("ServerDescription");
        profile.Basic.ServerPassword = doc.GetString("ServerPassword");
        profile.Basic.AdminPassword = doc.GetString("AdminPassword");
        profile.Basic.ServerPlayerMaxNum = doc.GetInt("ServerPlayerMaxNum", 32);

        profile.Network.PublicPort = doc.GetInt("PublicPort", 8211);
        profile.Network.PublicIP = doc.GetString("PublicIP");
        profile.Network.RCONEnabled = doc.GetBool("RCONEnabled");
        profile.Network.RCONPort = doc.GetInt("RCONPort", 25575);
        profile.Network.RESTAPIEnabled = doc.GetBool("RESTAPIEnabled");
        profile.Network.RESTAPIPort = doc.GetInt("RESTAPIPort", 8212);
        profile.Network.CrossplayPlatforms = doc.GetRaw("CrossplayPlatforms");
        profile.Network.AllowConnectPlatform = doc.GetRaw("AllowConnectPlatform");
        profile.Network.Region = doc.GetString("Region");

        // Populate known and unknown settings maps
        var knownKeys = PalworldSettingRegistry.KnownKeys;
        foreach (var key in doc.Keys)
        {
            var raw = doc.GetRaw(key);
            if (PalworldSettingRegistry.IsKnown(key))
                profile.OptionSettings.KnownSettings[key] = raw;
            else
                profile.OptionSettings.UnknownSettings[key] = raw;
        }
    }

    // Writes the profile model's core fields back into the document.
    private static void SyncToDocument(PalworldServerProfile profile, PalworldConfigDocument doc)
    {
        doc.SetString("ServerName", profile.Basic.ServerName);
        doc.SetString("ServerDescription", profile.Basic.ServerDescription);
        doc.SetString("ServerPassword", profile.Basic.ServerPassword);
        doc.SetString("AdminPassword", profile.Basic.AdminPassword);
        doc.SetInt("ServerPlayerMaxNum", profile.Basic.ServerPlayerMaxNum);
        doc.SetInt("PublicPort", profile.Network.PublicPort);
        doc.SetString("PublicIP", profile.Network.PublicIP);
        doc.SetBool("RCONEnabled", profile.Network.RCONEnabled);
        doc.SetInt("RCONPort", profile.Network.RCONPort);
        doc.SetBool("RESTAPIEnabled", profile.Network.RESTAPIEnabled);
        doc.SetInt("RESTAPIPort", profile.Network.RESTAPIPort);

        if (!string.IsNullOrEmpty(profile.Network.CrossplayPlatforms))
            doc.SetRaw("CrossplayPlatforms", profile.Network.CrossplayPlatforms);

        // Write unknown settings back (preserves future 1.0 settings)
        foreach (var kv in profile.OptionSettings.UnknownSettings)
            doc.SetRaw(kv.Key, kv.Value);
    }
}

public sealed record PalworldConfigLoadResult(
    bool ConfigExists, IReadOnlyList<string> Warnings, PalworldConfigDocument Document);

public sealed record PalworldConfigSaveResult(string ConfigPath, bool BackupCreated);

// ── Validator ─────────────────────────────────────────────────────────────────

public sealed class PalworldValidator
{
    public PalworldValidationResult Validate(PalworldServerProfile profile)
    {
        var result = new PalworldValidationResult();

        Required(profile.Basic.InstallPath, "Install path is required.", result);
        PortRange(profile.Network.GamePort, "Game port", result);
        PortRange(profile.Network.RCONPort, "RCON port", result);
        PortRange(profile.Network.RESTAPIPort, "REST API port", result);
        IntRange(profile.Basic.ServerPlayerMaxNum, 1, 32, "Max players must be between 1 and 32.", result);

        var ports = new List<(int Port, string Name, string Protocol)>
        {
            (profile.Network.GamePort,    "Game",     "UDP"),
            (profile.Network.RCONPort,    "RCON",     "TCP"),
            (profile.Network.RESTAPIPort, "REST API", "TCP")
        };

        foreach (var dup in ports.GroupBy(p => p.Port).Where(g => g.Count() > 1))
            result.Errors.Add($"Duplicate port {dup.Key} assigned to: {string.Join(", ", dup.Select(p => p.Name))}.");

        if (string.IsNullOrWhiteSpace(profile.Basic.AdminPassword))
            result.Warnings.Add("AdminPassword is empty. In-game admin commands, RCON, and REST API admin actions will not work.");

        if (profile.Network.RESTAPIEnabled && string.IsNullOrWhiteSpace(profile.Basic.AdminPassword))
            result.Warnings.Add("REST API is enabled but AdminPassword is empty.");

        if (profile.Launch.PerformanceMode && profile.Launch.WorkerThreadCount > Environment.ProcessorCount - 1)
            result.Warnings.Add($"WorkerThreadCount ({profile.Launch.WorkerThreadCount}) exceeds recommended maximum of {Environment.ProcessorCount - 1} (CPU threads - 1).");

        foreach (var def in PalworldSettingRegistry.All.Where(d => d.DataType == PalworldSettingDataType.Decimal))
        {
            if (!profile.OptionSettings.KnownSettings.TryGetValue(def.Key, out var rawVal)) continue;
            if (!decimal.TryParse(rawVal, System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out var num)) continue;
            if (def.Min.HasValue && num < def.Min.Value)
                result.Warnings.Add($"{def.DisplayName} ({num}) is below the recommended minimum of {def.Min.Value}.");
            if (def.Max.HasValue && num > def.Max.Value)
                result.Warnings.Add($"{def.DisplayName} ({num}) is above the recommended maximum of {def.Max.Value}.");
        }

        return result;
    }

    public PalworldPortConflictResult CheckPortConflicts(PalworldServerProfile profile)
    {
        var props = IPGlobalProperties.GetIPGlobalProperties();
        var tcp = props.GetActiveTcpListeners().Select(e => e.Port).ToHashSet();
        var udp = props.GetActiveUdpListeners().Select(e => e.Port).ToHashSet();
        var conflicts = new List<string>();
        if (udp.Contains(profile.Network.GamePort))
            conflicts.Add($"UDP game port {profile.Network.GamePort} is already listening.");
        if (tcp.Contains(profile.Network.RCONPort))
            conflicts.Add($"TCP RCON port {profile.Network.RCONPort} is already listening.");
        if (tcp.Contains(profile.Network.RESTAPIPort))
            conflicts.Add($"TCP REST API port {profile.Network.RESTAPIPort} is already listening.");
        return new PalworldPortConflictResult(conflicts);
    }

    private static void Required(string v, string msg, PalworldValidationResult r)
    { if (string.IsNullOrWhiteSpace(v)) r.Errors.Add(msg); }

    private static void PortRange(int v, string name, PalworldValidationResult r)
    { if (v is < 1 or > 65535) r.Errors.Add($"{name} must be between 1 and 65535."); }

    private static void IntRange(int v, int min, int max, string msg, PalworldValidationResult r)
    { if (v < min || v > max) r.Errors.Add(msg); }
}

public sealed class PalworldValidationResult
{
    public List<string> Errors { get; } = new();
    public List<string> Warnings { get; } = new();
    public bool IsValid => Errors.Count == 0;
}

public sealed record PalworldPortConflictResult(IReadOnlyList<string> Conflicts);

// ── Backup Service ────────────────────────────────────────────────────────────

public sealed class PalworldBackupService
{
    public async Task<PalworldBackupMetadata> CreateBackupAsync(
        PalworldServerProfile profile, PalworldBackupType type = PalworldBackupType.Full, string notes = "")
    {
        Directory.CreateDirectory(profile.Paths.BackupPath);
        var timestamp = DateTime.UtcNow;
        var safeName = SafeName(profile.Basic.ServerName);
        var name = $"{safeName}-{type}-{timestamp:yyyyMMddHHmmss}.zip";
        var path = Path.Combine(profile.Paths.BackupPath, name);

        using var archive = ZipFile.Open(path, ZipArchiveMode.Create);

        if (type is PalworldBackupType.Full or PalworldBackupType.SaveOnly)
            AddDirectory(archive, profile.Paths.SaveGamesPath, "SaveGames");

        if (type is PalworldBackupType.Full or PalworldBackupType.ConfigOnly)
            AddFile(archive, profile.Paths.PalWorldSettingsPath, "Config/PalWorldSettings.ini");

        if (type is PalworldBackupType.Full or PalworldBackupType.ModsOnly)
        {
            AddFile(archive, profile.Paths.PalModSettingsPath, "Mods/PalModSettings.ini");
            AddDirectory(archive, profile.Paths.ManagedModsPath, "Mods/ManagedMods");
        }

        var metaText = new StringBuilder();
        metaText.AppendLine($"Date={timestamp:o}");
        metaText.AppendLine($"Server={profile.Basic.ServerName}");
        metaText.AppendLine($"Type={type}");
        metaText.AppendLine($"Notes={notes}");
        var entry = archive.CreateEntry("backup-metadata.txt");
        await using var stream = entry.Open();
        await using var writer = new StreamWriter(stream);
        await writer.WriteAsync(metaText.ToString());

        var size = new FileInfo(path).Length;
        profile.Backup.UseBuiltInBackup = profile.Backup.UseBuiltInBackup;
        profile.Advanced.LastBackupTime = timestamp;
        return new PalworldBackupMetadata(timestamp, profile.Basic.ServerName, type, size, notes, path);
    }

    private static void AddDirectory(ZipArchive archive, string dir, string root)
    {
        if (!Directory.Exists(dir)) return;
        foreach (var file in Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories))
        {
            var rel = Path.GetRelativePath(dir, file).Replace('\\', '/');
            archive.CreateEntryFromFile(file, $"{root}/{rel}", CompressionLevel.Optimal);
        }
    }

    private static void AddFile(ZipArchive archive, string path, string entryName)
    {
        if (File.Exists(path))
            archive.CreateEntryFromFile(path, entryName, CompressionLevel.Optimal);
    }

    private static string SafeName(string v)
    {
        var invalid = Path.GetInvalidFileNameChars();
        return new string(v.Select(c => invalid.Contains(c) ? '_' : c).ToArray());
    }
}

public enum PalworldBackupType { Full, SaveOnly, ConfigOnly, ModsOnly }

public sealed record PalworldBackupMetadata(
    DateTime CreatedAtUtc, string ServerName, PalworldBackupType Type,
    long FileSize, string Notes, string Path);

// ── Preset Service ────────────────────────────────────────────────────────────

public sealed class PalworldPresetService
{
    public IReadOnlyList<PalworldPreset> GetPresets() => new[]
    {
        Preset("Official-like",      "Mirrors official server rates.",                    1m,  1m,  1m,  1m,  72m,  "All",  false, false),
        Preset("Small Friends",      "Relaxed rates for a small private group.",           2m,  3m,  2m,  3m,  24m,  "None", false, false),
        Preset("Casual PvE",         "Balanced PvE with slightly boosted rates.",          2m,  2m,  2m,  2m,  48m,  "None", false, false),
        Preset("Boosted PvE",        "Significantly boosted rates for faster progress.",   5m,  5m,  5m,  5m,  10m,  "None", false, false),
        Preset("Fast Leveling",      "Very high XP rate.",                                10m, 2m,  2m,  2m,  24m,  "None", false, false),
        Preset("Fast Capture",       "Very high Pal capture rate.",                       2m, 10m,  2m,  2m,  24m,  "None", false, false),
        Preset("Fast Egg Hatching",  "Very short egg hatch times.",                       2m,  2m,  2m,  2m,   1m,  "None", false, false),
        Preset("High Gathering",     "High drop rates from resources.",                   2m,  2m, 10m,  2m,  24m,  "None", false, false),
        Preset("PvP Test",           "Aggressive PvP server for testing.",                2m,  2m,  2m,  2m,  24m,  "All",  true,  false),
        Preset("Hardcore",           "Permadeath challenge. Items and Pals lost on death.",1m, 1m,  1m,  1m,  72m,  "All",  true,  true),
        Preset("Low-End PC",         "Reduced world rates for low-spec servers.",          1m,  1m,  1m,  0.5m,72m, "None", false, false),
        Preset("Palworld 1.0 Fresh", "Sensible defaults matching 1.0 recommended values.", 1m, 1m,  1m,  1m,  72m, "Item", false, false)
    };

    public PalworldPresetDiff PreviewApply(PalworldServerProfile profile, PalworldPreset preset)
    {
        var changes = BuildChanges(preset);
        return new PalworldPresetDiff(preset.Name, changes.Select(kv =>
        {
            var current = profile.OptionSettings.KnownSettings.TryGetValue(kv.Key, out var v) ? v : "(not set)";
            return new PalworldPresetChange(kv.Key, current, kv.Value);
        }).ToArray());
    }

    public void Apply(PalworldServerProfile profile, PalworldPreset preset)
    {
        foreach (var kv in BuildChanges(preset))
            profile.OptionSettings.KnownSettings[kv.Key] = kv.Value;
        profile.Advanced.MarkServerNeedsRestart = true;
    }

    private static Dictionary<string, string> BuildChanges(PalworldPreset preset)
    {
        var c = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["ExpRate"]          = F(preset.ExpRate),
            ["PalCaptureRate"]   = F(preset.CaptureRate),
            ["CollectionDropRate"] = F(preset.GatherRate),
            ["WorkSpeedRate"]    = F(preset.WorkRate),
            ["PalEggDefaultHatchingTime"] = F(preset.EggHatchHours),
            ["DeathPenalty"]     = preset.DeathPenalty,
            ["bIsPvP"]           = preset.PvP ? "True" : "False",
            ["bHardcore"]        = preset.Hardcore ? "True" : "False"
        };
        if (preset.Hardcore)
            c["bCharacterRecreateInHardcore"] = "True";
        return c;
    }

    private static string F(decimal v) =>
        v.ToString("0.000000", System.Globalization.CultureInfo.InvariantCulture);

    private static PalworldPreset Preset(
        string name, string desc,
        decimal xp, decimal capture, decimal gather, decimal work,
        decimal eggHatch, string deathPenalty, bool pvp, bool hardcore)
        => new(name, desc, xp, capture, gather, work, eggHatch, deathPenalty, pvp, hardcore);
}

public sealed record PalworldPreset(
    string Name, string Description,
    decimal ExpRate, decimal CaptureRate, decimal GatherRate, decimal WorkRate,
    decimal EggHatchHours, string DeathPenalty, bool PvP, bool Hardcore);

public sealed record PalworldPresetDiff(string PresetName, IReadOnlyList<PalworldPresetChange> Changes);
public sealed record PalworldPresetChange(string Key, string CurrentValue, string NewValue);

// ── Health Service ─────────────────────────────────────────────────────────────

public sealed class PalworldHealthService
{
    public PalworldHealthReport Check(PalworldServerProfile profile)
    {
        var warnings = new List<string>();
        var errors = new List<string>();

        if (!File.Exists(profile.Paths.ExecutablePath))
            errors.Add($"PalServer.exe not found at: {profile.Paths.ExecutablePath}");

        if (!File.Exists(profile.Paths.DefaultPalWorldSettingsPath))
            warnings.Add($"DefaultPalWorldSettings.ini (template) not found at: {profile.Paths.DefaultPalWorldSettingsPath}");

        if (!File.Exists(profile.Paths.PalWorldSettingsPath))
            warnings.Add(
                $"PalWorldSettings.ini not found at: {profile.Paths.PalWorldSettingsPath}. " +
                "Start the server once to create it, or copy from DefaultPalWorldSettings.ini.");

        if (!Directory.Exists(profile.Paths.SaveGamesPath))
            warnings.Add("SaveGames folder does not exist. It will be created when the server runs for the first time.");

        if (string.IsNullOrWhiteSpace(profile.Basic.AdminPassword))
            warnings.Add("AdminPassword is not set. Admin commands and REST/RCON admin actions will be unavailable.");

        if (profile.Network.RESTAPIEnabled && string.IsNullOrWhiteSpace(profile.Basic.AdminPassword))
            warnings.Add("REST API is enabled but AdminPassword is empty. API calls requiring authentication will fail.");

        if (profile.Advanced.LastBackupTime.HasValue &&
            DateTime.UtcNow - profile.Advanced.LastBackupTime.Value > TimeSpan.FromHours(24))
            warnings.Add("No backup has been created in the last 24 hours.");

        if (profile.Mods.NoModsLaunchFlag && profile.Mods.ActiveMods.Count > 0)
            warnings.Add("-NoMods flag is enabled but mods are configured. Mods will be disabled at launch.");

        return new PalworldHealthReport(errors, warnings);
    }
}

public sealed record PalworldHealthReport(IReadOnlyList<string> Errors, IReadOnlyList<string> Warnings)
{
    public bool IsHealthy => Errors.Count == 0;
}

// ── Mod Manager ───────────────────────────────────────────────────────────────

public sealed class PalworldModManager
{
    // Reads PalModSettings.ini from the server.
    public async Task<PalworldModSettingsDocument> LoadModSettingsAsync(PalworldServerProfile profile)
    {
        var path = profile.Paths.PalModSettingsPath;
        if (!File.Exists(path))
            return new PalworldModSettingsDocument(false, new List<string>(), string.Empty);

        var lines = await File.ReadAllLinesAsync(path, Encoding.UTF8);
        var enabled = false;
        var packages = new List<string>();
        var workshopDir = string.Empty;

        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (trimmed.StartsWith("bGlobalEnableMod=", StringComparison.OrdinalIgnoreCase))
                enabled = trimmed[17..].Trim().Equals("true", StringComparison.OrdinalIgnoreCase);
            else if (trimmed.StartsWith("ActiveModList=", StringComparison.OrdinalIgnoreCase))
                packages.Add(trimmed[14..].Trim());
            else if (trimmed.StartsWith("WorkshopRootDir=", StringComparison.OrdinalIgnoreCase))
                workshopDir = trimmed[16..].Trim();
        }

        return new PalworldModSettingsDocument(enabled, packages, workshopDir);
    }

    // Writes PalModSettings.ini atomically with a backup.
    public async Task SaveModSettingsAsync(PalworldServerProfile profile, PalworldModSettingsDocument doc)
    {
        var path = profile.Paths.PalModSettingsPath;
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        if (profile.Mods.BackupBeforeModChanges && File.Exists(path))
        {
            var bak = $"{path}.{DateTime.UtcNow:yyyyMMddHHmmss}.bak";
            File.Copy(path, bak, overwrite: false);
        }

        var sb = new StringBuilder();
        sb.AppendLine("[PalModSettings]");
        sb.AppendLine($"bGlobalEnableMod={doc.GlobalEnabled.ToString().ToLowerInvariant()}");
        foreach (var pkg in doc.ActiveModList)
            sb.AppendLine($"ActiveModList={pkg}");
        if (!string.IsNullOrWhiteSpace(doc.WorkshopRootDir))
            sb.AppendLine($"WorkshopRootDir={doc.WorkshopRootDir}");

        var tmp = path + ".tmp";
        await File.WriteAllTextAsync(tmp, sb.ToString(), Encoding.UTF8);
        File.Move(tmp, path, overwrite: true);
        profile.Advanced.MarkServerNeedsRestart = true;
    }

    // Scans a directory for Palworld mods by looking for Info.json files.
    public async Task<List<PalworldMod>> ScanForModsAsync(string directory)
    {
        var mods = new List<PalworldMod>();
        if (!Directory.Exists(directory)) return mods;

        foreach (var modDir in Directory.EnumerateDirectories(directory))
        {
            var infoJson = Path.Combine(modDir, "Info.json");
            if (!File.Exists(infoJson)) continue;

            try
            {
                var text = await File.ReadAllTextAsync(infoJson, Encoding.UTF8);
                var mod = ParseInfoJson(text, modDir, infoJson);
                if (mod != null) mods.Add(mod);
            }
            catch
            {
                mods.Add(new PalworldMod
                {
                    FolderName = Path.GetFileName(modDir),
                    InstallPath = modDir,
                    InfoJsonPath = infoJson,
                    WarningMessages = { "Failed to parse Info.json." }
                });
            }
        }

        return mods;
    }

    private static PalworldMod? ParseInfoJson(string json, string modDir, string infoJsonPath)
    {
        // Minimal JSON extraction without a full JSON library dependency.
        static string? ExtractField(string text, string field)
        {
            var search = $"\"{field}\"";
            var idx = text.IndexOf(search, StringComparison.OrdinalIgnoreCase);
            if (idx < 0) return null;
            var colonIdx = text.IndexOf(':', idx + search.Length);
            if (colonIdx < 0) return null;
            var valueStart = text.IndexOf('"', colonIdx + 1);
            if (valueStart < 0) return null;
            var valueEnd = text.IndexOf('"', valueStart + 1);
            if (valueEnd < 0) return null;
            return text[(valueStart + 1)..valueEnd];
        }

        static bool CheckServerCompatible(string text)
        {
            var idx = text.IndexOf("InstallRules", StringComparison.OrdinalIgnoreCase);
            if (idx < 0) return false;
            var block = text[idx..Math.Min(idx + 200, text.Length)];
            return block.Contains("IsServer", StringComparison.OrdinalIgnoreCase) &&
                   block.Contains("true", StringComparison.OrdinalIgnoreCase);
        }

        var packageName = ExtractField(json, "PackageName");
        var displayName = ExtractField(json, "FriendlyName") ?? ExtractField(json, "DisplayName") ?? packageName;
        var version = ExtractField(json, "VersionName") ?? ExtractField(json, "Version");
        var author = ExtractField(json, "Author");
        var isServerCompatible = CheckServerCompatible(json);

        var warnings = new List<string>();
        if (string.IsNullOrWhiteSpace(packageName))
            warnings.Add("PackageName is missing from Info.json. This mod cannot be added to ActiveModList.");
        if (!isServerCompatible)
            warnings.Add("InstallRules in Info.json does not confirm server compatibility (IsServer: true not found).");

        return new PalworldMod
        {
            PackageName = packageName ?? string.Empty,
            FolderName = Path.GetFileName(modDir),
            DisplayName = displayName ?? Path.GetFileName(modDir),
            Version = version ?? string.Empty,
            Author = author ?? string.Empty,
            InstallPath = modDir,
            InfoJsonPath = infoJsonPath,
            IsServerCompatible = isServerCompatible,
            DateAdded = DateTime.UtcNow,
            WarningMessages = warnings
        };
    }
}

public sealed record PalworldModSettingsDocument(
    bool GlobalEnabled, List<string> ActiveModList, string WorkshopRootDir);

