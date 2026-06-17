using System.IO.Compression;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using GameServerManager.Core.Models;
using GameServerManager.Services.Configuration;

namespace GameServerManager.Services.ArkSurvivalAscended;

public sealed class ArkAsaProfileMapper
{
    public ArkSurvivalAscendedServerProfile FromServerProfile(ServerProfile profile)
    {
        var ark = new ArkSurvivalAscendedServerProfile();
        ark.Basic.ServerName = Value(profile.ServerName, "ARK ASA Server");
        ark.Basic.Description = profile.Notes;
        ark.Basic.InstallPath = profile.InstallPath;
        ark.Basic.InstanceName = Value(profile.ProfileName, "ARK ASA");
        ark.Basic.MapName = Value(profile.MapName, "TheIsland_WP");
        ark.Basic.AltSaveDirectoryName = Get(profile, "AltSaveDirectoryName");
        ark.Basic.MaxPlayers = profile.MaxPlayers <= 0 ? 70 : profile.MaxPlayers;
        ark.Basic.ServerPassword = profile.Password;
        ark.Basic.AdminPassword = profile.AdminPassword;
        ark.Basic.SpectatorPassword = Get(profile, "SpectatorPassword");
        ark.Basic.EnableBattlEye = !Bool(profile, "NoBattlEye");
        ark.Basic.EnableConsoleLog = Bool(profile, "EnableConsoleLog", true);
        ark.Basic.AutoStart = Bool(profile, "AutoStart");
        ark.Basic.AutoRestartOnCrash = Bool(profile, "AutoRestartOnCrash", true);
        ark.Basic.RestartSchedule = profile.RestartSchedule ?? string.Empty;

        ark.Network.ServerIP = Get(profile, "ServerIP");
        ark.Network.MultiHome = Get(profile, "MultiHome");
        ark.Network.GamePort = Port(profile, "Game", 7777);
        ark.Network.QueryPort = Port(profile, "Query", 27015);
        ark.Network.RCONPort = Port(profile, "RCON", 27020);
        ark.Network.RCONEnabled = Bool(profile, "RCONEnabled", true);
        ark.Network.RCONPassword = Value(Get(profile, "RCONPassword"), profile.AdminPassword);
        ark.Network.PublicServer = Bool(profile, "PublicServer", true);
        ark.Network.LANOnly = Bool(profile, "LANOnly");
        ark.Network.EnableCrossplay = Bool(profile, "EnableCrossplay", true);
        ark.Network.CreateFirewallRules = Bool(profile, "CreateFirewallRules");

        ark.Cluster.ClusterEnabled = ClusterEnabled(profile);
        ark.Cluster.ClusterID = First(profile, "Cluster.Id", "ClusterID");
        ark.Cluster.ClusterDirectoryOverride = First(profile, "Cluster.DirectoryOverride", "ClusterDirOverride");
        ark.Cluster.NoTransferFromFiltering = Bool(profile, "Cluster.NoTransferFromFiltering", Bool(profile, "NoTransferFromFiltering", true));
        ark.Cluster.PreventDownloadSurvivors = Bool(profile, "PreventDownloadSurvivors");
        ark.Cluster.PreventDownloadItems = Bool(profile, "PreventDownloadItems");
        ark.Cluster.PreventDownloadDinos = Bool(profile, "PreventDownloadDinos");
        ark.Cluster.PreventUploadSurvivors = Bool(profile, "PreventUploadSurvivors");
        ark.Cluster.PreventUploadItems = Bool(profile, "PreventUploadItems");
        ark.Cluster.PreventUploadDinos = Bool(profile, "PreventUploadDinos");
        ark.Cluster.AllowTributeDownloads = !Bool(profile, "noTributeDownloads") && !Bool(profile, "NoTributeDownloads") && Bool(profile, "AllowTributeDownloads", true);
        ark.Cluster.SharedClusterFolder = Get(profile, "SharedClusterFolder");
        ark.Cluster.ClusterMapGroup = Get(profile, "ClusterMapGroup");

        ark.Mods.EnabledMods = profile.Mods.Select(mod => new ArkModEntry
        {
            Id = mod.Id,
            Name = mod.Name,
            Enabled = mod.IsEnabled
        }).ToList();
        foreach (var modId in SplitCsv(Get(profile, "ModIDs")))
        {
            if (ark.Mods.EnabledMods.All(mod => !mod.Id.Equals(modId, StringComparison.OrdinalIgnoreCase)))
            {
                ark.Mods.EnabledMods.Add(new ArkModEntry { Id = modId, Enabled = true });
            }
        }

        ark.Mods.ModIDs = ark.Mods.EnabledMods.Where(mod => mod.Enabled).Select(mod => mod.Id).ToList();
        ark.Mods.ModLoadOrder = SplitCsv(Get(profile, "ModLoadOrder")).ToList();
        ark.Mods.AutoUpdateMods = Bool(profile, "AutoUpdateMods", true);
        ark.Mods.ValidateModsBeforeStart = Bool(profile, "ValidateModsBeforeStart", true);
        ark.Mods.WarnOnDuplicateMods = Bool(profile, "WarnOnDuplicateMods", true);
        ark.Mods.CustomModArguments = Get(profile, "CustomModArguments");

        ark.Backup.ScheduledBackupsEnabled = profile.AutoBackupEnabled;
        ark.Backup.Schedule = profile.Settings.TryGetValue("BackupSchedule", out var schedule) ? schedule : ark.Backup.Schedule;
        ark.Launch.CustomLaunchArguments = profile.LaunchArgs;

        HydratePaths(ark);
        return ark;
    }

    public void HydratePaths(ArkSurvivalAscendedServerProfile profile)
    {
        var root = profile.Basic.InstallPath;
        profile.Paths.ServerRootPath = root;
        profile.Paths.ExecutablePath = Path.Combine(root, ArkSurvivalAscendedServerProfile.DefaultExecutableRelativePath);
        profile.Paths.SavedPath = Path.Combine(root, "ShooterGame", "Saved");
        profile.Paths.ConfigPath = Path.Combine(profile.Paths.SavedPath, "Config", "WindowsServer");
        profile.Paths.GameUserSettingsPath = Path.Combine(profile.Paths.ConfigPath, "GameUserSettings.ini");
        profile.Paths.GameIniPath = Path.Combine(profile.Paths.ConfigPath, "Game.ini");
        profile.Paths.LogsPath = Path.Combine(profile.Paths.SavedPath, "Logs");
        profile.Paths.SavesPath = Path.Combine(profile.Paths.SavedPath, "SavedArks");
        profile.Paths.ModsPath = Path.Combine(root, "ShooterGame", "Binaries", "Win64", "ShooterGame", "Mods");
        profile.Paths.BackupPath = Path.Combine(root, "Backups");
        profile.Paths.ClusterPath = !string.IsNullOrWhiteSpace(profile.Cluster.ClusterDirectoryOverride)
            ? profile.Cluster.ClusterDirectoryOverride
            : Path.Combine(root, "Cluster");
    }

    private static string Get(ServerProfile profile, string key)
    {
        return profile.Settings.TryGetValue(key, out var value) ? value : string.Empty;
    }

    private static string First(ServerProfile profile, params string[] keys)
    {
        foreach (var key in keys)
        {
            var value = Get(profile, key);
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return string.Empty;
    }

    private static string Value(string value, string fallback)
    {
        return string.IsNullOrWhiteSpace(value) ? fallback : value;
    }

    private static bool Bool(ServerProfile profile, string key, bool fallback = false)
    {
        return profile.Settings.TryGetValue(key, out var value) && bool.TryParse(value, out var parsed) ? parsed : fallback;
    }

    private static bool ClusterEnabled(ServerProfile profile)
    {
        if (profile.Settings.TryGetValue("Cluster.Enabled", out var modern) && bool.TryParse(modern, out var modernEnabled))
        {
            return modernEnabled;
        }

        if (profile.Settings.TryGetValue("ClusterEnabled", out var legacy) && bool.TryParse(legacy, out var legacyEnabled))
        {
            return legacyEnabled;
        }

        return !string.IsNullOrWhiteSpace(First(profile, "Cluster.Id", "ClusterID")) ||
               !string.IsNullOrWhiteSpace(First(profile, "Cluster.DirectoryOverride", "ClusterDirOverride"));
    }

    private static int Port(ServerProfile profile, string name, int fallback)
    {
        return profile.Ports.FirstOrDefault(port => port.Name.Equals(name, StringComparison.OrdinalIgnoreCase))?.Port ?? fallback;
    }

    private static IEnumerable<string> SplitCsv(string value)
    {
        return value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }
}

public sealed class ArkAsaSteamCmdService
{
    public string BuildInstallOrUpdateArguments(string installPath, bool validate = true)
    {
        var validateArg = validate ? " validate" : string.Empty;
        return $"+force_install_dir \"{installPath}\" +login anonymous +app_update {ArkSurvivalAscendedServerProfile.SteamCmdAppId}{validateArg} +quit";
    }
}

public sealed class ArkAsaLaunchBuilder
{
    public ArkLaunchPreview Build(ArkSurvivalAscendedServerProfile profile, bool revealPasswords = false)
    {
        var query = new List<string>
        {
            "listen",
            $"SessionName=\"{Escape(profile.Basic.ServerName)}\"",
            $"Port={profile.Network.GamePort}",
            $"QueryPort={profile.Network.QueryPort}",
            $"RCONPort={profile.Network.RCONPort}",
            $"RCONEnabled={ToArkBool(profile.Network.RCONEnabled)}",
            $"ServerAdminPassword=\"{Escape(revealPasswords ? profile.Basic.AdminPassword : Mask(profile.Basic.AdminPassword))}\"",
            $"MaxPlayers={profile.Basic.MaxPlayers}"
        };

        if (!string.IsNullOrWhiteSpace(profile.Basic.ServerPassword))
        {
            query.Add($"ServerPassword=\"{Escape(revealPasswords ? profile.Basic.ServerPassword : Mask(profile.Basic.ServerPassword))}\"");
        }

        if (!string.IsNullOrWhiteSpace(profile.Basic.SpectatorPassword))
        {
            query.Add($"SpectatorPassword=\"{Escape(revealPasswords ? profile.Basic.SpectatorPassword : Mask(profile.Basic.SpectatorPassword))}\"");
        }

        if (!string.IsNullOrWhiteSpace(profile.Basic.AltSaveDirectoryName))
        {
            query.Add($"AltSaveDirectoryName={profile.Basic.AltSaveDirectoryName}");
        }

        if (!string.IsNullOrWhiteSpace(profile.Network.MultiHome))
        {
            query.Add($"MultiHome={profile.Network.MultiHome}");
        }

        var flags = new List<string>();
        if (profile.Cluster.ClusterEnabled)
        {
            if (!string.IsNullOrWhiteSpace(profile.Cluster.ClusterID))
            {
                flags.Add($"-clusterid={profile.Cluster.ClusterID}");
            }

            if (!string.IsNullOrWhiteSpace(profile.Cluster.ClusterDirectoryOverride))
            {
                flags.Add($"-ClusterDirOverride=\"{profile.Cluster.ClusterDirectoryOverride}\"");
            }

            if (profile.Cluster.NoTransferFromFiltering)
            {
                flags.Add("-NoTransferFromFiltering");
            }
        }

        var enabledMods = profile.Mods.EnabledMods.Where(mod => mod.Enabled).Select(mod => mod.Id).Where(id => !string.IsNullOrWhiteSpace(id)).ToArray();
        if (enabledMods.Length > 0)
        {
            flags.Add($"-mods={string.Join(',', enabledMods)}");
        }

        if (!profile.Basic.EnableBattlEye)
        {
            flags.Add("-NoBattlEye");
        }

        if (profile.Basic.EnableConsoleLog)
        {
            flags.Add("-log");
        }

        if (!string.IsNullOrWhiteSpace(profile.Mods.CustomModArguments))
        {
            flags.Add(profile.Mods.CustomModArguments);
        }

        if (!string.IsNullOrWhiteSpace(profile.Launch.CustomLaunchArguments))
        {
            flags.Add(profile.Launch.CustomLaunchArguments);
        }

        var arguments = $"{profile.Basic.MapName}?{string.Join('?', query)} {string.Join(' ', flags)}".Trim();
        var executable = profile.Paths.ExecutablePath;
        return new ArkLaunchPreview(executable, profile.Basic.InstallPath, arguments, $"\"{executable}\" {arguments}");
    }

    private static string ToArkBool(bool value)
    {
        return value ? "True" : "False";
    }

    private static string Escape(string value)
    {
        return value.Replace("\"", "\\\"", StringComparison.Ordinal);
    }

    public static string Mask(string value)
    {
        return string.IsNullOrEmpty(value) ? string.Empty : "********";
    }
}

public sealed record ArkLaunchPreview(string ExecutablePath, string WorkingDirectory, string Arguments, string CommandLine);

public sealed record ArkServerConfigurationState(
    string ServerId,
    string ServerInstallPath,
    string GameUserSettingsPath,
    string GameIniPath,
    string LaunchArguments,
    IniDocument RawGameUserSettingsDocument,
    IniDocument RawGameIniDocument,
    IReadOnlyDictionary<string, string> SavedValues,
    IReadOnlyDictionary<string, string> PendingValues,
    DateTimeOffset LastLoadedAt,
    string GameUserSettingsRawText,
    string GameIniRawText)
{
    public bool HasUnsavedChanges =>
        SavedValues.Count != PendingValues.Count ||
        PendingValues.Any(entry => !SavedValues.TryGetValue(entry.Key, out var saved) || !string.Equals(saved, entry.Value, StringComparison.Ordinal));
}

public sealed class ArkAsaConfigurationStateService
{
    private readonly ArkAsaProfileMapper _mapper = new();

    public async Task<ArkServerConfigurationState> LoadAsync(string serverId, ArkSurvivalAscendedServerProfile profile)
    {
        _mapper.HydratePaths(profile);
        var gusText = File.Exists(profile.Paths.GameUserSettingsPath)
            ? await File.ReadAllTextAsync(profile.Paths.GameUserSettingsPath)
            : string.Empty;
        var gameText = File.Exists(profile.Paths.GameIniPath)
            ? await File.ReadAllTextAsync(profile.Paths.GameIniPath)
            : string.Empty;

        var gus = IniDocument.Parse(gusText);
        var game = IniDocument.Parse(gameText);
        ApplyDocumentsToProfile(profile, gus, game);

        profile.RawSettings.GameUserSettingsRawText = gusText;
        profile.RawSettings.GameIniRawText = gameText;
        var values = SnapshotValues(profile);

        return new ArkServerConfigurationState(
            serverId,
            profile.Basic.InstallPath,
            profile.Paths.GameUserSettingsPath,
            profile.Paths.GameIniPath,
            profile.Launch.CustomLaunchArguments,
            gus,
            game,
            values,
            new Dictionary<string, string>(values, StringComparer.OrdinalIgnoreCase),
            DateTimeOffset.UtcNow,
            gusText,
            gameText);
    }

    public ArkServerConfigurationState LoadFromRawText(string serverId, ArkSurvivalAscendedServerProfile profile, string gameUserSettingsText, string gameIniText)
    {
        _mapper.HydratePaths(profile);
        var gus = IniDocument.Parse(gameUserSettingsText);
        var game = IniDocument.Parse(gameIniText);
        ApplyDocumentsToProfile(profile, gus, game);
        profile.RawSettings.GameUserSettingsRawText = gameUserSettingsText;
        profile.RawSettings.GameIniRawText = gameIniText;
        var values = SnapshotValues(profile);

        return new ArkServerConfigurationState(
            serverId,
            profile.Basic.InstallPath,
            profile.Paths.GameUserSettingsPath,
            profile.Paths.GameIniPath,
            profile.Launch.CustomLaunchArguments,
            gus,
            game,
            values,
            new Dictionary<string, string>(values, StringComparer.OrdinalIgnoreCase),
            DateTimeOffset.UtcNow,
            gameUserSettingsText,
            gameIniText);
    }

    public static IReadOnlyDictionary<string, string> SnapshotValues(ArkSurvivalAscendedServerProfile profile)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var setting in ArkAsaSettingRegistry.All)
        {
            var identity = SettingIdentity(setting);
            var value = setting.FileLocation switch
            {
                ArkSettingFileLocation.GameUserSettingsIni when profile.GameUserSettings.ServerSettings.TryGetValue(setting.Key, out var saved) => saved,
                ArkSettingFileLocation.GameIni when setting.DataType == ArkSettingDataType.RepeatedLine && profile.GameIni.RepeatedSettings.TryGetValue(setting.Key, out var repeated) => string.Join(Environment.NewLine, repeated),
                ArkSettingFileLocation.GameIni when profile.GameIni.ShooterGameModeSettings.TryGetValue(setting.Key, out var saved) => saved,
                ArkSettingFileLocation.LaunchArguments => GetLaunchValue(profile, setting),
                _ => setting.DefaultValue
            };

            values[identity] = value;
        }

        return values;
    }

    private static void ApplyDocumentsToProfile(ArkSurvivalAscendedServerProfile profile, IniDocument gus, IniDocument game)
    {
        profile.GameUserSettings.ServerSettings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        profile.GameIni.ShooterGameModeSettings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        profile.GameIni.RepeatedSettings = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var setting in ArkAsaSettingRegistry.All)
        {
            if (setting.FileLocation == ArkSettingFileLocation.GameUserSettingsIni)
            {
                var value = gus.GetValue(setting.IniSection, setting.Key);
                if (value is not null)
                {
                    profile.GameUserSettings.ServerSettings[setting.Key] = value;
                    ApplyKnownScalarToProfile(profile, setting.Key, value);
                }
            }
            else if (setting.FileLocation == ArkSettingFileLocation.GameIni)
            {
                var values = game.GetValues(setting.IniSection, setting.Key);
                if (setting.DataType == ArkSettingDataType.RepeatedLine)
                {
                    if (values.Count > 0)
                    {
                        profile.GameIni.RepeatedSettings[setting.Key] = values.ToList();
                    }
                }
                else
                {
                    var value = values.LastOrDefault();
                    if (value is not null)
                    {
                        profile.GameIni.ShooterGameModeSettings[setting.Key] = value;
                    }
                }
            }
        }
    }

    private static void ApplyKnownScalarToProfile(ArkSurvivalAscendedServerProfile profile, string key, string value)
    {
        if (key.Equals("SessionName", StringComparison.OrdinalIgnoreCase)) profile.Basic.ServerName = value;
        else if (key.Equals("ServerPassword", StringComparison.OrdinalIgnoreCase)) profile.Basic.ServerPassword = value;
        else if (key.Equals("ServerAdminPassword", StringComparison.OrdinalIgnoreCase)) profile.Basic.AdminPassword = value;
        else if (key.Equals("SpectatorPassword", StringComparison.OrdinalIgnoreCase)) profile.Basic.SpectatorPassword = value;
        else if (key.Equals("MaxPlayers", StringComparison.OrdinalIgnoreCase) && int.TryParse(value, out var maxPlayers)) profile.Basic.MaxPlayers = maxPlayers;
        else if (key.Equals("RCONEnabled", StringComparison.OrdinalIgnoreCase) && bool.TryParse(value, out var rconEnabled)) profile.Network.RCONEnabled = rconEnabled;
        else if (key.Equals("RCONPort", StringComparison.OrdinalIgnoreCase) && int.TryParse(value, out var rconPort)) profile.Network.RCONPort = rconPort;
        else if (key.Equals("ActiveMods", StringComparison.OrdinalIgnoreCase))
        {
            profile.Mods.ModIDs = value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
        }
        else if (key.Equals("ActiveMapMod", StringComparison.OrdinalIgnoreCase))
        {
            profile.Mods.ActiveMapModId = value;
        }
    }

    private static string GetLaunchValue(ArkSurvivalAscendedServerProfile profile, ArkSettingDefinition setting)
    {
        return setting.Key.ToLowerInvariant() switch
        {
            "mapname" => profile.Basic.MapName,
            "sessionname" => profile.Basic.ServerName,
            "serverpassword" => profile.Basic.ServerPassword,
            "serveradminpassword" => profile.Basic.AdminPassword,
            "port" => profile.Network.GamePort.ToString(System.Globalization.CultureInfo.InvariantCulture),
            "queryport" => profile.Network.QueryPort.ToString(System.Globalization.CultureInfo.InvariantCulture),
            "rconport" => profile.Network.RCONPort.ToString(System.Globalization.CultureInfo.InvariantCulture),
            "rconenabled" => profile.Network.RCONEnabled ? "True" : "False",
            "maxplayers" => profile.Basic.MaxPlayers.ToString(System.Globalization.CultureInfo.InvariantCulture),
            "altsavedirectoryname" => profile.Basic.AltSaveDirectoryName,
            "clusterid" => profile.Cluster.ClusterID,
            "clusterdiroverride" => profile.Cluster.ClusterDirectoryOverride,
            "notransferfromfiltering" => profile.Cluster.NoTransferFromFiltering ? "True" : "False",
            "mods" => string.Join(',', profile.Mods.EnabledMods.Where(mod => mod.Enabled).Select(mod => mod.Id)),
            "nobattleye" => profile.Basic.EnableBattlEye ? "False" : "True",
            "log" => profile.Basic.EnableConsoleLog ? "True" : "False",
            _ => profile.Launch.CustomLaunchArguments
        };
    }

    private static string SettingIdentity(ArkSettingDefinition setting) =>
        $"{setting.FileLocation}|{setting.IniSection}|{setting.Key}";
}

public sealed class ArkAsaConfigService
{
    public async Task<ArkConfigSaveResult> SaveAsync(ArkSurvivalAscendedServerProfile profile, bool createBackup = true)
    {
        var validator = new ArkAsaValidator();
        var validation = validator.Validate(profile);
        if (validation.Errors.Count > 0)
        {
            throw new InvalidOperationException(string.Join(Environment.NewLine, validation.Errors));
        }

        var gus = await IniDocument.LoadAsync(profile.Paths.GameUserSettingsPath);
        foreach (var setting in profile.GameUserSettings.ServerSettings)
        {
            gus.SetValue(ArkAsaSettingRegistry.ServerSettingsSection, setting.Key, setting.Value);
        }

        gus.SetValue(ArkAsaSettingRegistry.ServerSettingsSection, "SessionName", profile.Basic.ServerName);
        gus.SetValue(ArkAsaSettingRegistry.ServerSettingsSection, "ServerPassword", profile.Basic.ServerPassword);
        gus.SetValue(ArkAsaSettingRegistry.ServerSettingsSection, "ServerAdminPassword", profile.Basic.AdminPassword);
        gus.SetValue(ArkAsaSettingRegistry.ServerSettingsSection, "SpectatorPassword", profile.Basic.SpectatorPassword);
        gus.SetValue(ArkAsaSettingRegistry.ServerSettingsSection, "MaxPlayers", profile.Basic.MaxPlayers.ToString());
        gus.SetValue(ArkAsaSettingRegistry.ServerSettingsSection, "RCONEnabled", profile.Network.RCONEnabled ? "True" : "False");
        gus.SetValue(ArkAsaSettingRegistry.ServerSettingsSection, "RCONPort", profile.Network.RCONPort.ToString());
        foreach (var line in profile.GameUserSettings.CustomGameUserSettingsLines)
        {
            gus.AddRawLine(ArkAsaSettingRegistry.ServerSettingsSection, line);
        }

        var game = await IniDocument.LoadAsync(profile.Paths.GameIniPath);
        foreach (var setting in profile.GameIni.ShooterGameModeSettings)
        {
            game.SetValue(ArkAsaSettingRegistry.GameModeSection, setting.Key, setting.Value);
        }

        foreach (var repeated in profile.GameIni.RepeatedSettings)
        {
            game.SetRepeatedValues(ArkAsaSettingRegistry.GameModeSection, repeated.Key, repeated.Value);
        }

        foreach (var line in profile.GameIni.CustomGameIniLines)
        {
            game.AddRawLine(ArkAsaSettingRegistry.GameModeSection, line);
        }

        await gus.SaveAtomicAsync(profile.Paths.GameUserSettingsPath, createBackup);
        await game.SaveAtomicAsync(profile.Paths.GameIniPath, createBackup);
        await VerifySavedAsync(profile);
        profile.Advanced.MarkServerNeedsRestart = true;
        return new ArkConfigSaveResult(profile.Paths.GameUserSettingsPath, profile.Paths.GameIniPath);
    }

    private static async Task VerifySavedAsync(ArkSurvivalAscendedServerProfile profile)
    {
        var gus = await IniDocument.LoadAsync(profile.Paths.GameUserSettingsPath);
        foreach (var setting in profile.GameUserSettings.ServerSettings)
        {
            var saved = gus.GetValue(ArkAsaSettingRegistry.ServerSettingsSection, setting.Key);
            if (!string.Equals(saved, setting.Value, StringComparison.Ordinal))
            {
                throw new InvalidOperationException($"Save verification failed for GameUserSettings.ini key {setting.Key}.");
            }
        }

        var game = await IniDocument.LoadAsync(profile.Paths.GameIniPath);
        foreach (var setting in profile.GameIni.ShooterGameModeSettings)
        {
            var saved = game.GetValue(ArkAsaSettingRegistry.GameModeSection, setting.Key);
            if (!string.Equals(saved, setting.Value, StringComparison.Ordinal))
            {
                throw new InvalidOperationException($"Save verification failed for Game.ini key {setting.Key}.");
            }
        }
    }
}

public sealed record ArkConfigSaveResult(string GameUserSettingsPath, string GameIniPath);

public sealed class ArkAsaValidator
{
    public ArkValidationResult Validate(ArkSurvivalAscendedServerProfile profile)
    {
        var result = new ArkValidationResult();
        Required(profile.Basic.InstallPath, "Install path is required.", result);
        Required(profile.Basic.MapName, "Map name is required.", result);
        Range(profile.Basic.MaxPlayers, 1, 200, "Max players must be between 1 and 200.", result);
        Port(profile.Network.GamePort, "Game port", result);
        Port(profile.Network.QueryPort, "Query port", result);
        Port(profile.Network.RCONPort, "RCON port", result);

        var duplicates = new[] { profile.Network.GamePort, profile.Network.QueryPort, profile.Network.RCONPort }
            .GroupBy(port => port)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToArray();
        foreach (var duplicate in duplicates)
        {
            result.Errors.Add($"Duplicate ARK port configured: {duplicate}.");
        }

        foreach (var setting in ArkAsaSettingRegistry.All)
        {
            var value = GetConfiguredValue(profile, setting);
            if (string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            ValidateSettingValue(setting, value, result);
        }

        foreach (var duplicateMod in profile.Mods.EnabledMods.Where(mod => mod.Enabled)
                     .GroupBy(mod => mod.Id, StringComparer.OrdinalIgnoreCase)
                     .Where(group => group.Count() > 1))
        {
            result.Warnings.Add($"Duplicate mod ID: {duplicateMod.Key}.");
        }

        if (string.IsNullOrWhiteSpace(profile.Basic.AdminPassword))
        {
            result.Warnings.Add("Admin password is empty. RCON and admin commands will be unsafe or unavailable.");
        }

        if (profile.Cluster.ClusterEnabled)
        {
            if (string.IsNullOrWhiteSpace(profile.Cluster.ClusterID))
            {
                result.Errors.Add("Cluster ID is required when clustering is enabled.");
            }

            if (string.IsNullOrWhiteSpace(profile.Cluster.ClusterDirectoryOverride))
            {
                result.Errors.Add("Cluster Directory Override is required when clustering is enabled.");
            }
            else if (profile.Cluster.ClusterDirectoryOverride.IndexOfAny(Path.GetInvalidPathChars()) >= 0)
            {
                result.Errors.Add("Cluster Directory Override contains invalid path characters.");
            }
        }

        return result;
    }

    public void ValidateSettingValue(ArkSettingDefinition setting, string value, ArkValidationResult result)
    {
        if (setting.DataType == ArkSettingDataType.Boolean &&
            !value.Equals("True", StringComparison.OrdinalIgnoreCase) &&
            !value.Equals("False", StringComparison.OrdinalIgnoreCase))
        {
            result.Errors.Add($"{setting.DisplayName} must be True or False.");
        }

        if ((setting.DataType == ArkSettingDataType.Integer || setting.DataType == ArkSettingDataType.Decimal) &&
            !decimal.TryParse(value, out var number))
        {
            result.Errors.Add($"{setting.DisplayName} must be numeric.");
            return;
        }

        if ((setting.DataType == ArkSettingDataType.Integer || setting.DataType == ArkSettingDataType.Decimal) &&
            decimal.TryParse(value, out number))
        {
            if (setting.Min.HasValue && number < setting.Min.Value)
            {
                result.Errors.Add($"{setting.DisplayName} must be at least {setting.Min.Value}.");
            }

            if (setting.Max.HasValue && number > setting.Max.Value)
            {
                result.Errors.Add($"{setting.DisplayName} must be at most {setting.Max.Value}.");
            }
        }
    }

    public ArkPortConflictResult CheckPortConflicts(ArkSurvivalAscendedServerProfile profile)
    {
        var listeners = IPGlobalProperties.GetIPGlobalProperties();
        var tcpPorts = listeners.GetActiveTcpListeners().Select(endpoint => endpoint.Port).ToHashSet();
        var udpPorts = listeners.GetActiveUdpListeners().Select(endpoint => endpoint.Port).ToHashSet();
        var conflicts = new List<string>();
        if (udpPorts.Contains(profile.Network.GamePort))
        {
            conflicts.Add($"UDP game port {profile.Network.GamePort} is already listening.");
        }

        if (udpPorts.Contains(profile.Network.QueryPort))
        {
            conflicts.Add($"UDP query port {profile.Network.QueryPort} is already listening.");
        }

        if (tcpPorts.Contains(profile.Network.RCONPort))
        {
            conflicts.Add($"TCP RCON port {profile.Network.RCONPort} is already listening.");
        }

        return new ArkPortConflictResult(conflicts);
    }

    private static string GetConfiguredValue(ArkSurvivalAscendedServerProfile profile, ArkSettingDefinition setting)
    {
        return setting.FileLocation switch
        {
            ArkSettingFileLocation.GameUserSettingsIni => profile.GameUserSettings.ServerSettings.TryGetValue(setting.Key, out var value) ? value : string.Empty,
            ArkSettingFileLocation.GameIni => profile.GameIni.ShooterGameModeSettings.TryGetValue(setting.Key, out var value) ? value : string.Empty,
            _ => string.Empty
        };
    }

    private static void Required(string value, string message, ArkValidationResult result)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            result.Errors.Add(message);
        }
    }

    private static void Range(int value, int min, int max, string message, ArkValidationResult result)
    {
        if (value < min || value > max)
        {
            result.Errors.Add(message);
        }
    }

    private static void Port(int value, string name, ArkValidationResult result)
    {
        if (value is < IPEndPoint.MinPort or > IPEndPoint.MaxPort)
        {
            result.Errors.Add($"{name} must be between 1 and 65535.");
        }
    }
}

public sealed class ArkValidationResult
{
    public List<string> Errors { get; } = new();
    public List<string> Warnings { get; } = new();
    public bool IsValid => Errors.Count == 0;
}

public sealed record ArkPortConflictResult(IReadOnlyList<string> Conflicts);

public sealed class ArkAsaModManager
{
    public ArkValidationResult Validate(ArkModSettings settings)
    {
        var result = new ArkValidationResult();
        foreach (var mod in settings.EnabledMods.Where(mod => string.IsNullOrWhiteSpace(mod.Id)))
        {
            result.Errors.Add($"Mod '{mod.Name}' is missing an ID.");
        }

        foreach (var duplicate in settings.EnabledMods.Where(mod => mod.Enabled).GroupBy(mod => mod.Id).Where(group => group.Count() > 1))
        {
            result.Warnings.Add($"Duplicate mod ID: {duplicate.Key}.");
        }

        return result;
    }

    public string BuildModArgument(ArkModSettings settings)
    {
        var ids = settings.EnabledMods.Where(mod => mod.Enabled).Select(mod => mod.Id).Where(id => !string.IsNullOrWhiteSpace(id));
        return string.Join(',', ids);
    }
}

public sealed class ArkAsaClusterManager
{
    public static IReadOnlyList<ArkAsaKnownMap> KnownMaps { get; } = new[]
    {
        new ArkAsaKnownMap("The Island", "TheIsland_WP", "Island"),
        new ArkAsaKnownMap("Scorched Earth", "ScorchedEarth_WP", "Scorched"),
        new ArkAsaKnownMap("The Center", "TheCenter_WP", "Center"),
        new ArkAsaKnownMap("Aberration", "Aberration_WP", "Aberration"),
        new ArkAsaKnownMap("Extinction", "Extinction_WP", "Extinction"),
        new ArkAsaKnownMap("Astraeos", "Astraeos_WP", "Astraeos"),
        new ArkAsaKnownMap("Ragnarok", "Ragnarok_WP", "Ragnarok"),
        new ArkAsaKnownMap("Valguero", "Valguero_WP", "Valguero"),
        new ArkAsaKnownMap("Lost Colony", "LostColony_WP", "LostColony"),
        new ArkAsaKnownMap("Custom", "Custom", "Custom")
    };

    public ServerProfile CreateClusterMapProfile(ArkAsaClusterMapRequest request)
    {
        var map = request.MapName.Equals("Custom", StringComparison.OrdinalIgnoreCase)
            ? request.CustomMapName
            : request.MapName;
        var displayName = string.IsNullOrWhiteSpace(request.DisplayName)
            ? map
            : request.DisplayName.Trim();
        var altSave = string.IsNullOrWhiteSpace(request.AltSaveDirectoryName)
            ? Sanitize(displayName)
            : request.AltSaveDirectoryName.Trim();

        return new ServerProfile
        {
            Id = Guid.NewGuid().ToString(),
            GameId = ArkSurvivalAscendedServerProfile.GameId,
            ProfileName = $"{request.ClusterName} - {displayName}",
            ServerName = $"{request.ClusterName} {displayName}",
            InstallPath = request.InstallPath,
            MapName = map,
            MaxPlayers = request.MaxPlayers,
            AutoBackupEnabled = request.SharedBackupEnabled,
            Ports = new List<ServerPort>
            {
                new() { Name = "Game", Port = request.GamePort, DefaultPort = 7777, Protocol = PortProtocol.UDP, Description = "ARK game traffic", IsRequired = true },
                new() { Name = "Query", Port = request.QueryPort, DefaultPort = 27015, Protocol = PortProtocol.UDP, Description = "ARK query traffic", IsRequired = true },
                new() { Name = "RCON", Port = request.RconPort, DefaultPort = 27020, Protocol = PortProtocol.TCP, Description = "ARK remote console", IsRequired = false }
            },
            Settings =
            {
                ["ClusterEnabled"] = "True",
                ["ClusterID"] = request.ClusterId,
                ["ClusterDirOverride"] = request.ClusterDirectoryOverride,
                ["SharedClusterFolder"] = request.ClusterDirectoryOverride,
                ["ClusterMapGroup"] = request.ClusterName,
                ["AltSaveDirectoryName"] = altSave,
                ["NoTransferFromFiltering"] = request.NoTransferFromFiltering.ToString(),
                ["PreventDownloadSurvivors"] = request.PreventDownloadSurvivors.ToString(),
                ["PreventDownloadItems"] = request.PreventDownloadItems.ToString(),
                ["PreventDownloadDinos"] = request.PreventDownloadDinos.ToString(),
                ["PreventUploadSurvivors"] = request.PreventUploadSurvivors.ToString(),
                ["PreventUploadItems"] = request.PreventUploadItems.ToString(),
                ["PreventUploadDinos"] = request.PreventUploadDinos.ToString(),
                ["AllowTributeDownloads"] = request.AllowTributeDownloads.ToString(),
                ["noTributeDownloads"] = (!request.AllowTributeDownloads).ToString(),
                ["RCONEnabled"] = "True",
                ["SharedBackup"] = request.SharedBackupEnabled.ToString()
            }
        };
    }

    public ArkClusterDashboard BuildDashboard(IEnumerable<ServerProfile> profiles)
    {
        var mapper = new ArkAsaProfileMapper();
        var maps = profiles
            .Where(IsArkProfile)
            .Select(profile => new ArkClusterMapSummary(profile, mapper.FromServerProfile(profile)))
            .Where(map => map.Profile.Settings.TryGetValue("ClusterEnabled", out var enabled) &&
                          bool.TryParse(enabled, out var parsed) &&
                          parsed)
            .OrderBy(map => map.Ark.Basic.ServerName, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var clusterId = MostCommon(maps.Select(map => map.Ark.Cluster.ClusterID));
        var clusterDir = MostCommon(maps.Select(map => map.Ark.Cluster.ClusterDirectoryOverride));
        var validation = ValidateClusterProfiles(maps.Select(map => map.Profile), KnownMaps.Take(1).Select(map => map.InternalName));
        return new ArkClusterDashboard(clusterId, clusterDir, maps, validation);
    }

    public ArkClusterValidationReport ValidateClusterProfiles(IEnumerable<ServerProfile> profiles, IEnumerable<string>? expectedMaps = null)
    {
        var summaries = profiles.Where(IsArkProfile)
            .Select(profile => new ArkClusterMapSummary(profile, new ArkAsaProfileMapper().FromServerProfile(profile)))
            .ToArray();
        return ValidateClusterSummaries(summaries, expectedMaps);
    }

    public ArkValidationResult ValidateCluster(IEnumerable<ArkSurvivalAscendedServerProfile> profiles)
    {
        var result = new ArkValidationResult();
        var clusterProfiles = profiles.Where(profile => profile.Cluster.ClusterEnabled).ToArray();
        foreach (var group in clusterProfiles.GroupBy(profile => profile.Cluster.ClusterID))
        {
            if (string.IsNullOrWhiteSpace(group.Key))
            {
                result.Warnings.Add("One or more clustered maps have an empty ClusterID.");
            }

            var directories = group.Select(profile => profile.Cluster.ClusterDirectoryOverride).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            if (directories.Length > 1)
            {
                result.Warnings.Add($"Cluster '{group.Key}' has mismatched ClusterDirOverride values.");
            }

            var duplicatedPorts = group.SelectMany(profile => new[] { profile.Network.GamePort, profile.Network.QueryPort, profile.Network.RCONPort })
                .GroupBy(port => port)
                .Where(port => port.Count() > 1)
                .Select(port => port.Key);
            foreach (var port in duplicatedPorts)
            {
                result.Errors.Add($"Cluster '{group.Key}' has duplicate port {port}.");
            }
        }

        return result;
    }

    private static ArkClusterValidationReport ValidateClusterSummaries(IReadOnlyList<ArkClusterMapSummary> maps, IEnumerable<string>? expectedMaps)
    {
        var issues = new List<ArkClusterValidationIssue>();
        if (maps.Count == 0)
        {
            issues.Add(new ArkClusterValidationIssue(ArkClusterIssueSeverity.Warning, "No ARK ASA maps are configured for this cluster yet."));
            return new ArkClusterValidationReport(issues);
        }

        if (maps.Count == 1)
        {
            issues.Add(new ArkClusterValidationIssue(ArkClusterIssueSeverity.Warning, "Only one map is enabled. ARK clusters require at least two server profiles."));
        }

        var clusterIds = maps.Select(map => map.Ark.Cluster.ClusterID).Where(value => !string.IsNullOrWhiteSpace(value)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        if (clusterIds.Length == 0)
        {
            issues.Add(new ArkClusterValidationIssue(ArkClusterIssueSeverity.Error, "ClusterID is required before starting a cluster."));
        }
        else if (clusterIds.Length > 1)
        {
            issues.Add(new ArkClusterValidationIssue(ArkClusterIssueSeverity.Error, $"ClusterID does not match across maps: {string.Join(", ", clusterIds)}."));
        }

        var clusterDirs = maps.Select(map => map.Ark.Cluster.ClusterDirectoryOverride).Where(value => !string.IsNullOrWhiteSpace(value)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        if (clusterDirs.Length == 0)
        {
            issues.Add(new ArkClusterValidationIssue(ArkClusterIssueSeverity.Error, "ClusterDirOverride is required before starting a cluster."));
        }
        else if (clusterDirs.Length > 1)
        {
            issues.Add(new ArkClusterValidationIssue(ArkClusterIssueSeverity.Error, $"ClusterDirOverride does not match across maps: {string.Join(", ", clusterDirs)}."));
        }
        else
        {
            ValidateSharedClusterDirectory(clusterDirs[0], maps, issues);
        }

        foreach (var portGroup in maps.SelectMany(map => new[]
                 {
                     (Map: map.Ark.Basic.MapName, Name: "Game", Port: map.Ark.Network.GamePort),
                     (Map: map.Ark.Basic.MapName, Name: "Query", Port: map.Ark.Network.QueryPort),
                     (Map: map.Ark.Basic.MapName, Name: "RCON", Port: map.Ark.Network.RCONPort)
                 })
                 .GroupBy(item => item.Port)
                 .Where(group => group.Count() > 1))
        {
            issues.Add(new ArkClusterValidationIssue(
                ArkClusterIssueSeverity.Error,
                $"Port {portGroup.Key} conflicts across {string.Join(", ", portGroup.Select(item => $"{item.Map} {item.Name}"))}."));
        }

        foreach (var saveGroup in maps.Select(map => map.Ark.Basic.AltSaveDirectoryName)
                     .Where(value => !string.IsNullOrWhiteSpace(value))
                     .GroupBy(value => value, StringComparer.OrdinalIgnoreCase)
                     .Where(group => group.Count() > 1))
        {
            issues.Add(new ArkClusterValidationIssue(ArkClusterIssueSeverity.Error, $"AltSaveDirectoryName '{saveGroup.Key}' is used by more than one map."));
        }

        var transferSignatures = maps.Select(map => new
        {
            Map = map.Ark.Basic.MapName,
            Signature = $"{map.Ark.Cluster.NoTransferFromFiltering}|{map.Ark.Cluster.PreventDownloadSurvivors}|{map.Ark.Cluster.PreventDownloadItems}|{map.Ark.Cluster.PreventDownloadDinos}|{map.Ark.Cluster.PreventUploadSurvivors}|{map.Ark.Cluster.PreventUploadItems}|{map.Ark.Cluster.PreventUploadDinos}|{map.Ark.Cluster.AllowTributeDownloads}"
        }).ToArray();
        if (transferSignatures.Select(item => item.Signature).Distinct(StringComparer.Ordinal).Count() > 1)
        {
            issues.Add(new ArkClusterValidationIssue(ArkClusterIssueSeverity.Warning, "Transfer rules do not match across every map."));
        }

        foreach (var expectedMap in expectedMaps ?? Array.Empty<string>())
        {
            if (!maps.Any(map => map.Ark.Basic.MapName.Equals(expectedMap, StringComparison.OrdinalIgnoreCase)))
            {
                issues.Add(new ArkClusterValidationIssue(ArkClusterIssueSeverity.Warning, $"Expected map '{expectedMap}' is missing from this cluster."));
            }
        }

        foreach (var map in maps.Where(map => !File.Exists(map.Ark.Paths.ExecutablePath)))
        {
            issues.Add(new ArkClusterValidationIssue(ArkClusterIssueSeverity.Warning, $"{map.Ark.Basic.MapName} is missing its server executable."));
        }

        foreach (var map in maps.Where(map => string.IsNullOrWhiteSpace(map.Ark.Basic.MapName)))
        {
            issues.Add(new ArkClusterValidationIssue(ArkClusterIssueSeverity.Error, $"{map.Ark.Basic.ServerName} must select a map."));
        }

        return new ArkClusterValidationReport(issues);
    }

    private static void ValidateSharedClusterDirectory(string clusterDir, IReadOnlyList<ArkClusterMapSummary> maps, List<ArkClusterValidationIssue> issues)
    {
        try
        {
            var fullClusterDir = Path.GetFullPath(clusterDir);
            var parent = Directory.Exists(fullClusterDir)
                ? fullClusterDir
                : Path.GetDirectoryName(fullClusterDir);

            if (string.IsNullOrWhiteSpace(parent) || !Directory.Exists(parent))
            {
                issues.Add(new ArkClusterValidationIssue(ArkClusterIssueSeverity.Error, $"Shared cluster directory parent does not exist: {clusterDir}."));
                return;
            }

            if (Directory.Exists(fullClusterDir))
            {
                var probe = Path.Combine(fullClusterDir, $".nexus-write-test-{Guid.NewGuid():N}.tmp");
                File.WriteAllText(probe, "test");
                File.Delete(probe);
            }

            var containingInstall = maps
                .Select(map => map.Ark.Basic.InstallPath)
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .Select(Path.GetFullPath)
                .FirstOrDefault(path => fullClusterDir.StartsWith(path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase));

            if (!string.IsNullOrWhiteSpace(containingInstall))
            {
                issues.Add(new ArkClusterValidationIssue(ArkClusterIssueSeverity.Warning, "Shared cluster directory is inside one server install folder. A neutral shared folder is safer for multi-map clusters."));
            }
        }
        catch (Exception ex)
        {
            issues.Add(new ArkClusterValidationIssue(ArkClusterIssueSeverity.Error, $"Shared cluster directory is not writable or creatable: {ex.Message}"));
        }
    }

    private static bool IsArkProfile(ServerProfile profile)
    {
        return profile.GameId.Equals(ArkSurvivalAscendedServerProfile.GameId, StringComparison.OrdinalIgnoreCase) ||
               profile.GameId.Equals(ArkSurvivalAscendedServerProfile.LegacyGameId, StringComparison.OrdinalIgnoreCase);
    }

    private static string MostCommon(IEnumerable<string> values)
    {
        return values
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .GroupBy(value => value, StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(group => group.Count())
            .Select(group => group.Key)
            .FirstOrDefault() ?? string.Empty;
    }

    private static string Sanitize(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        return new string(value.Select(ch => invalid.Contains(ch) || char.IsWhiteSpace(ch) ? '_' : ch).ToArray());
    }
}

public sealed record ArkAsaKnownMap(string DisplayName, string InternalName, string DefaultAltSaveDirectoryName)
{
    public override string ToString() => DisplayName;
}

public sealed record ArkAsaClusterMapRequest(
    string ClusterName,
    string ClusterId,
    string ClusterDirectoryOverride,
    string DisplayName,
    string MapName,
    string CustomMapName,
    string AltSaveDirectoryName,
    string InstallPath,
    int GamePort,
    int QueryPort,
    int RconPort,
    int MaxPlayers,
    bool SharedBackupEnabled,
    bool NoTransferFromFiltering,
    bool PreventDownloadSurvivors,
    bool PreventDownloadItems,
    bool PreventDownloadDinos,
    bool PreventUploadSurvivors,
    bool PreventUploadItems,
    bool PreventUploadDinos,
    bool AllowTributeDownloads);

public sealed record ArkClusterMapSummary(ServerProfile Profile, ArkSurvivalAscendedServerProfile Ark);

public sealed record ArkClusterDashboard(
    string ClusterId,
    string ClusterDirectoryOverride,
    IReadOnlyList<ArkClusterMapSummary> Maps,
    ArkClusterValidationReport Validation);

public sealed record ArkClusterValidationReport(IReadOnlyList<ArkClusterValidationIssue> Issues)
{
    public bool CanStart => Issues.All(issue => issue.Severity != ArkClusterIssueSeverity.Error);
    public IReadOnlyList<ArkClusterValidationIssue> Errors => Issues.Where(issue => issue.Severity == ArkClusterIssueSeverity.Error).ToArray();
    public IReadOnlyList<ArkClusterValidationIssue> Warnings => Issues.Where(issue => issue.Severity == ArkClusterIssueSeverity.Warning).ToArray();
}

public sealed record ArkClusterValidationIssue(ArkClusterIssueSeverity Severity, string Message);

public enum ArkClusterIssueSeverity
{
    Warning,
    Error
}

public sealed class ArkAsaBackupService
{
    public async Task<ArkBackupMetadata> CreateBackupAsync(ArkSurvivalAscendedServerProfile profile, string notes = "")
    {
        Directory.CreateDirectory(profile.Paths.BackupPath);
        var name = $"{SafeName(profile.Basic.ServerName)}-{profile.Basic.MapName}-{DateTime.UtcNow:yyyyMMddHHmmss}.zip";
        var path = Path.Combine(profile.Paths.BackupPath, name);
        using var archive = ZipFile.Open(path, ZipArchiveMode.Create);
        AddDirectory(archive, profile.Paths.SavesPath, "SavedArks");
        AddFile(archive, profile.Paths.GameUserSettingsPath, "Config/GameUserSettings.ini");
        AddFile(archive, profile.Paths.GameIniPath, "Config/Game.ini");
        if (profile.Cluster.ClusterEnabled)
        {
            AddDirectory(archive, profile.Paths.ClusterPath, "Cluster");
        }

        var metadata = new ArkBackupMetadata(DateTime.UtcNow, profile.Basic.ServerName, profile.Basic.MapName, profile.Cluster.ClusterID, new FileInfo(path).Length, "0.1.0", notes, path);
        var metadataText = $"Date={metadata.CreatedAtUtc:o}{Environment.NewLine}Server={metadata.ServerName}{Environment.NewLine}Map={metadata.MapName}{Environment.NewLine}ClusterID={metadata.ClusterId}{Environment.NewLine}Notes={metadata.Notes}";
        var entry = archive.CreateEntry("backup-metadata.txt");
        await using var stream = entry.Open();
        await using var writer = new StreamWriter(stream);
        await writer.WriteAsync(metadataText);
        return metadata;
    }

    private static void AddDirectory(ZipArchive archive, string directory, string entryRoot)
    {
        if (!Directory.Exists(directory))
        {
            return;
        }

        foreach (var file in Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(directory, file).Replace('\\', '/');
            archive.CreateEntryFromFile(file, $"{entryRoot}/{relative}", CompressionLevel.Optimal);
        }
    }

    private static void AddFile(ZipArchive archive, string path, string entryName)
    {
        if (File.Exists(path))
        {
            archive.CreateEntryFromFile(path, entryName, CompressionLevel.Optimal);
        }
    }

    private static string SafeName(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        return new string(value.Select(ch => invalid.Contains(ch) ? '_' : ch).ToArray());
    }
}

public sealed record ArkBackupMetadata(DateTime CreatedAtUtc, string ServerName, string MapName, string ClusterId, long FileSize, string AppVersion, string Notes, string Path);

public sealed class ArkAsaPresetService
{
    public IReadOnlyList<ArkServerPreset> GetPresets()
    {
        return new[]
        {
            Preset("Official-like", 1, 1, 1, 1, false),
            Preset("Casual PvE", 2, 3, 2, 2, true),
            Preset("Boosted PvE", 5, 10, 5, 5, true),
            Preset("Small Friends Server", 3, 6, 3, 3, true),
            Preset("Breeding Boosted", 3, 10, 4, 20, true, babyMature: 25),
            Preset("PvP", 2, 3, 2, 2, false),
            Preset("Cluster Map", 3, 5, 3, 5, true, cluster: true),
            Preset("Testing / Localhost", 20, 50, 20, 50, true, babyMature: 50)
        };
    }

    public void Apply(ArkSurvivalAscendedServerProfile profile, ArkServerPreset preset, IEnumerable<string>? categories = null)
    {
        var allowed = categories?.ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var setting in preset.GameUserSettings)
        {
            if (allowed == null || allowed.Contains("GameUserSettings.ini"))
            {
                profile.GameUserSettings.ServerSettings[setting.Key] = setting.Value;
            }
        }

        foreach (var setting in preset.GameIni)
        {
            if (allowed == null || allowed.Contains("Game.ini"))
            {
                profile.GameIni.ShooterGameModeSettings[setting.Key] = setting.Value;
            }
        }

        if (preset.ClusterEnabled)
        {
            profile.Cluster.ClusterEnabled = true;
            profile.Cluster.ClusterID = string.IsNullOrWhiteSpace(profile.Cluster.ClusterID) ? $"cluster-{Guid.NewGuid():N}"[..20] : profile.Cluster.ClusterID;
        }

        profile.Advanced.MarkServerNeedsRestart = true;
    }

    private static ArkServerPreset Preset(string name, decimal xp, decimal taming, decimal harvest, decimal hatch, bool pve, decimal babyMature = 1, bool cluster = false)
    {
        return new ArkServerPreset(
            name,
            new Dictionary<string, string>
            {
                ["XPMultiplier"] = xp.ToString("0.###"),
                ["TamingSpeedMultiplier"] = taming.ToString("0.###"),
                ["HarvestAmountMultiplier"] = harvest.ToString("0.###"),
                ["ServerPVE"] = pve ? "True" : "False",
                ["AutoSavePeriodMinutes"] = "15"
            },
            new Dictionary<string, string>
            {
                ["EggHatchSpeedMultiplier"] = hatch.ToString("0.###"),
                ["BabyMatureSpeedMultiplier"] = babyMature.ToString("0.###")
            },
            cluster);
    }
}

public sealed record ArkServerPreset(string Name, IReadOnlyDictionary<string, string> GameUserSettings, IReadOnlyDictionary<string, string> GameIni, bool ClusterEnabled);

public sealed class ArkAsaHealthService
{
    public ArkHealthReport Check(ArkSurvivalAscendedServerProfile profile)
    {
        var warnings = new List<string>();
        if (!File.Exists(profile.Paths.ExecutablePath))
        {
            warnings.Add($"Missing executable: {profile.Paths.ExecutablePath}");
        }

        if (!File.Exists(profile.Paths.GameUserSettingsPath))
        {
            warnings.Add("GameUserSettings.ini is missing.");
        }

        if (!File.Exists(profile.Paths.GameIniPath))
        {
            warnings.Add("Game.ini is missing.");
        }

        if (string.IsNullOrWhiteSpace(profile.Basic.AdminPassword))
        {
            warnings.Add("Admin password is empty.");
        }

        if (profile.Mods.WarnOnDuplicateMods && profile.Mods.EnabledMods.GroupBy(mod => mod.Id).Any(group => group.Count() > 1))
        {
            warnings.Add("Duplicate mod IDs are configured.");
        }

        if (profile.Cluster.ClusterEnabled && string.IsNullOrWhiteSpace(profile.Cluster.ClusterDirectoryOverride))
        {
            warnings.Add("Cluster folder override is missing.");
        }

        return new ArkHealthReport(warnings);
    }
}

public sealed record ArkHealthReport(IReadOnlyList<string> Warnings);
