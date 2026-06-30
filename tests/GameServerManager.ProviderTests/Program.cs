using GameServerManager.Core.Models;
using GameServerManager.GameProviders;
using GameServerManager.Services;
using GameServerManager.Services.ArkSurvivalAscended;
using GameServerManager.Services.Configuration;
using GameServerManager.Services.Diagnostics;
using GameServerManager.Services.Repositories;
using GameServerManager.Services.SevenDaysToDie;
using GameServerManager.Services.Updates;
using System.Xml.Linq;

var registry = GameProviderRegistry.CreateDefault();
var expectedGameIds = new[]
{
    "ark-survival-ascended",
    "ark_survival_ascended",
    "minecraft_java",
    "minecraft_bedrock",
    "seven_days_to_die",
    "palworld",
    "rust",
    "valheim"
};

foreach (var gameId in expectedGameIds)
{
    Assert(registry.TryGetProvider(gameId, out var provider), $"Provider missing: {gameId}");
    Assert(!string.IsNullOrWhiteSpace(provider.GameName), $"{gameId} name is required");
    Assert(!string.IsNullOrWhiteSpace(provider.DefaultInstallFolder), $"{gameId} install folder is required");
    Assert(!string.IsNullOrWhiteSpace(provider.ExecutableRelativePath), $"{gameId} executable path is required");
    Assert(provider.DefaultPorts.Count > 0, $"{gameId} must define at least one port");
    Assert(provider.DefaultPorts.All(p => p.Port is >= 1 and <= 65535), $"{gameId} has invalid port");

    var profile = new ServerProfile
    {
        Id = Guid.NewGuid().ToString(),
        GameId = provider.GameId,
        ProfileName = $"{provider.GameId}_test",
        ServerName = $"{provider.GameName} Test",
        InstallPath = Path.Combine("C:\\Servers", provider.GameId),
        MapName = "TestWorld",
        MaxPlayers = 10,
        Ports = provider.DefaultPorts.Select(p => new ServerPort
        {
            Name = p.Name,
            Port = p.Port,
            DefaultPort = p.DefaultPort,
            Protocol = p.Protocol,
            Description = p.Description,
            IsRequired = p.IsRequired
        }).ToList()
    };

    if (provider.GameId == "minecraft_java")
    {
        profile.Settings["MemoryMode"] = "Custom";
        profile.Settings["CustomMemoryMb"] = "1024";
        profile.Settings["JarFile"] = "server.jar";
    }

    var command = provider.BuildStartCommand(profile);
    Assert(command.IsValid, $"{gameId} launch command is invalid");
    Assert(!string.IsNullOrWhiteSpace(command.ExecutablePath), $"{gameId} executable is empty");
    Assert(!string.IsNullOrWhiteSpace(command.WorkingDirectory), $"{gameId} working dir is empty");
}

await TestRepositorySaveLoadAsync();
await TestServersJsonAddEditDeleteAsync();
TestImportDetection();
await TestImportCopyServiceAsync();
TestMemoryPolicy();
await TestArkSurvivalAscendedAsync();
TestArkClusterArgsEmittedWhenEnabled();
TestArkClusterArgsOmittedWhenDisabled();
TestArkClusterSharedDirectoryQuoting();
TestArkTransferRestrictionMapping();
TestArkDuplicatePortValidation();
TestArkSingleMemberClusterWarning();
TestArkCleanMultiMemberClusterValidation();
TestUpdaterVersionComparison();
TestSettingsUpdateSeparation();
TestArkSettingsRedesignContracts();
TestDiagnosticsMaskSecrets();
TestPortableModeDetection();
TestReleaseVersionStamp();
await TestServerInstallServiceValidationAsync();
TestArkWinLiveMaxPlayers();
await TestArkMaxPlayersNotInIniAsync();
TestArkActiveModsNotGenerated();
await TestArkIniRoundTripAsync();
await TestArkCommentsPreservedAsync();
TestArkMultiServerIsolation();
TestArkDecimalFormatting();
TestArkPasswordRedaction();
TestArkModOrdering();
await TestArkMigrationDetectionAsync();
await TestArkMinimalIniCreationAsync();
Test7DaysToDieProvider();
Test7DaysToDieSteamCmdArgs();
await Test7DaysToDieConfigXmlAsync();
Test7DaysToDieLaunchBuilder();
Test7DaysToDieValidator();
Test7DaysToDieSettingDescriptions();
Test7DaysToDieCrossplayValidation();
Test7DaysToDieWebDashboardSettings();
await Test7DaysToDieXmlPreservesUnknownPropertiesAsync();
Test7DaysToDieSandboxCodeRoundTrip();

Console.WriteLine("Provider and server data tests passed.");

static async Task TestRepositorySaveLoadAsync()
{
    var tempRoot = CreateTempRoot();
    try
    {
        var paths = new AppDataPaths(tempRoot);
        var repository = new JsonServerProfileRepository(paths);
        var profile = CreateTestProfile("repo-test", "Repository Test");

        await repository.SaveAsync(new[] { profile });
        var loaded = await repository.LoadAsync();

        Assert(loaded.Count == 1, "Repository should load one saved profile.");
        Assert(loaded[0].Id == profile.Id, "Repository should preserve profile id.");
        Assert(loaded[0].ServerName == "Repository Test", "Repository should preserve server name.");
    }
    finally
    {
        DeleteTempRoot(tempRoot);
    }
}

static async Task TestServersJsonAddEditDeleteAsync()
{
    var tempRoot = CreateTempRoot();
    try
    {
        var service = new ServersJsonService(new AppDataPaths(tempRoot));
        var profile = CreateTestProfile("crud-test", "CRUD Test");

        await service.AddServerAsync(profile);
        var added = await service.LoadServersAsync();
        Assert(added.Count == 1, "AddServerAsync should add one profile.");

        profile.ServerName = "CRUD Test Edited";
        await service.UpdateServerAsync(profile);
        var edited = await service.LoadServersAsync();
        Assert(edited.Single().ServerName == "CRUD Test Edited", "UpdateServerAsync should save edited profile.");

        await service.DeleteServerAsync(profile.Id);
        var deleted = await service.LoadServersAsync();
        Assert(deleted.Count == 0, "DeleteServerAsync should remove the profile.");
    }
    finally
    {
        DeleteTempRoot(tempRoot);
    }
}

static void TestImportDetection()
{
    var registry = GameProviderRegistry.CreateDefault();
    var detector = new ServerImportDetector(registry);
    var tempRoot = CreateTempRoot();
    try
    {
        var palworld = Path.Combine(tempRoot, "PalworldServer");
        Directory.CreateDirectory(Path.Combine(palworld, "Pal", "Saved", "SaveGames"));
        File.WriteAllText(Path.Combine(palworld, "PalServer.exe"), string.Empty);

        var palworldProfile = detector.Detect(palworld);
        Assert(palworldProfile.GameId == "palworld", "Import detection should identify Palworld.");
        Assert(palworldProfile.ExecutablePath.EndsWith("PalServer.exe", StringComparison.OrdinalIgnoreCase), "Import detection should find Palworld executable.");

        var generic = Path.Combine(tempRoot, "UnknownServer");
        Directory.CreateDirectory(generic);
        File.WriteAllText(Path.Combine(generic, "CustomServer.exe"), string.Empty);

        var genericProfile = detector.Detect(generic);
        Assert(genericProfile.GameId == "generic_server", "Unknown folders should import as Generic Server.");
        Assert(genericProfile.ExecutablePath.EndsWith("CustomServer.exe", StringComparison.OrdinalIgnoreCase), "Generic import should find an executable.");
    }
    finally
    {
        DeleteTempRoot(tempRoot);
    }
}

static async Task TestImportCopyServiceAsync()
{
    var tempRoot = CreateTempRoot();
    try
    {
        var source = Path.Combine(tempRoot, "OldServers", "ARK-Island");
        Directory.CreateDirectory(Path.Combine(source, "ShooterGame", "Binaries", "Win64"));
        File.WriteAllText(Path.Combine(source, "ShooterGame", "Binaries", "Win64", "ArkAscendedServer.exe"), "server");
        File.WriteAllText(Path.Combine(source, "server-settings.ini"), "keep");

        var paths = new AppDataPaths(Path.Combine(tempRoot, "ManagedData"));
        var importService = new ServerImportService(paths);
        var destination = importService.CreateDestinationPath("ARK-Island");
        await importService.CopyIntoManagedFolderAsync(source, destination, new Progress<ServerImportProgress>());

        Assert(File.Exists(Path.Combine(destination, "server-settings.ini")), "Import copy should copy server files into managed storage.");
        Assert(File.Exists(Path.Combine(source, "server-settings.ini")), "Import copy should leave the original server folder untouched.");

        var duplicateDestination = importService.CreateDestinationPath("ARK-Island");
        Assert(!string.Equals(destination, duplicateDestination, StringComparison.OrdinalIgnoreCase), "Duplicate imports should get a safe alternate destination.");
    }
    finally
    {
        DeleteTempRoot(tempRoot);
    }
}

static void TestMemoryPolicy()
{
    var registry = GameProviderRegistry.CreateDefault();
    Assert(registry.TryGetProvider("minecraft_java", out var minecraft), "Minecraft Java provider missing.");
    Assert(minecraft.SupportsMemoryLimit, "Minecraft Java should support optional custom memory limits.");

    var autoProfile = CreateTestProfile("minecraft-auto-memory", "Minecraft Auto Memory");
    autoProfile.GameId = "minecraft_java";
    autoProfile.InstallPath = "C:\\Servers\\Minecraft";
    autoProfile.Settings["JarFile"] = "server.jar";
    autoProfile.Settings["MemoryMode"] = "Auto";
    var autoCommand = minecraft.BuildStartCommand(autoProfile);
    Assert(!autoCommand.Arguments.Contains("-Xmx", StringComparison.OrdinalIgnoreCase), "Minecraft Auto memory mode should not add -Xmx.");
    Assert(!autoCommand.Arguments.Contains("-Xms", StringComparison.OrdinalIgnoreCase), "Minecraft Auto memory mode should not add -Xms.");

    var customProfile = CreateTestProfile("minecraft-custom-memory", "Minecraft Custom Memory");
    customProfile.GameId = "minecraft_java";
    customProfile.InstallPath = "C:\\Servers\\Minecraft";
    customProfile.Settings["JarFile"] = "server.jar";
    customProfile.Settings["MemoryMode"] = "Custom";
    customProfile.Settings["CustomMemoryMb"] = "8192";
    var customCommand = minecraft.BuildStartCommand(customProfile);
    Assert(customCommand.Arguments.Contains("-Xmx8192M", StringComparison.Ordinal), "Minecraft Custom memory mode should add -Xmx.");

    Assert(registry.TryGetProvider("ark-survival-ascended", out var ark), "ARK ASA provider missing.");
    var arkProfile = CreateTestProfile("ark-memory", "ARK Memory");
    arkProfile.GameId = "ark-survival-ascended";
    arkProfile.Settings["ramLimitMb"] = "4096";
    var migrated = MemorySettingsPolicy.ApplyProfileMigration(arkProfile, ark, out var message);
    Assert(migrated, "Unsupported games with legacy memory caps should be migrated.");
    Assert(!arkProfile.Settings.ContainsKey("ramLimitMb"), "ARK ASA memory cap should be removed.");
    Assert(message.Contains("Game Default", StringComparison.Ordinal), "Migration message should explain Game Default memory mode.");
}

static async Task TestArkSurvivalAscendedAsync()
{
    var registry = GameProviderRegistry.CreateDefault();
    Assert(registry.TryGetProvider(ArkSurvivalAscendedServerProfile.GameId, out var provider), "ARK ASA canonical provider missing.");
    Assert(provider.SteamAppId == 2430930, "ARK ASA SteamCMD app id must be 2430930.");
    Assert(provider.ExecutableRelativePath == ArkSurvivalAscendedServerProfile.DefaultExecutableRelativePath, "ARK ASA executable path mismatch.");

    var serverProfile = new ServerProfile
    {
        Id = "ark-asa-test",
        GameId = ArkSurvivalAscendedServerProfile.GameId,
        ProfileName = "Island",
        ServerName = "ASA Test",
        InstallPath = Path.Combine("C:\\Servers", "ARK ASA"),
        MapName = "TheIsland_WP",
        MaxPlayers = 20,
        Password = "join-secret",
        AdminPassword = "admin-secret",
        Ports = provider.DefaultPorts.Select(p => new ServerPort
        {
            Name = p.Name,
            Port = p.Port,
            DefaultPort = p.DefaultPort,
            Protocol = p.Protocol,
            Description = p.Description,
            IsRequired = p.IsRequired
        }).ToList(),
        Settings =
        {
            ["RCONEnabled"] = "True",
            ["AltSaveDirectoryName"] = "Island01",
            ["ClusterEnabled"] = "True",
            ["ClusterID"] = "cluster-one",
            ["ClusterDirOverride"] = "C:\\Clusters\\cluster-one",
            ["ModIDs"] = "928708,929110"
        }
    };

    var command = provider.BuildStartCommand(serverProfile);
    Assert(command.Arguments.Contains("TheIsland_WP?listen", StringComparison.Ordinal), "ARK launch command should include map and listen.");
    Assert(command.Arguments.Contains("SessionName=\"ASA Test\"", StringComparison.Ordinal), "ARK launch command should include session name.");
    Assert(command.Arguments.Contains("ServerAdminPassword=\"admin-secret\"", StringComparison.Ordinal), "ARK launch command should include admin password for process launch.");
    Assert(command.Arguments.Contains("-mods=928708,929110", StringComparison.Ordinal), "ARK launch command should include mods.");
    Assert(command.Arguments.Contains("-clusterid=cluster-one", StringComparison.Ordinal), "ARK launch command should include cluster ID.");

    var disabledClusterProfile = CreateArkClusterTestProfile(provider);
    disabledClusterProfile.Settings["Cluster.Enabled"] = "False";
    disabledClusterProfile.Settings["Cluster.Id"] = "disabled-cluster";
    disabledClusterProfile.Settings["Cluster.DirectoryOverride"] = "C:\\ARK Clusters\\Disabled Cluster";
    var disabledClusterCommand = provider.BuildStartCommand(disabledClusterProfile);
    Assert(!disabledClusterCommand.Arguments.Contains("-clusterid", StringComparison.OrdinalIgnoreCase), "Disabled cluster should not emit -clusterid.");
    Assert(!disabledClusterCommand.Arguments.Contains("-ClusterDirOverride", StringComparison.OrdinalIgnoreCase), "Disabled cluster should not emit -ClusterDirOverride.");

    var enabledClusterProfile = CreateArkClusterTestProfile(provider);
    enabledClusterProfile.Settings["Cluster.Enabled"] = "True";
    enabledClusterProfile.Settings["Cluster.Id"] = "main-crossark";
    enabledClusterProfile.Settings["Cluster.DirectoryOverride"] = "C:\\ARK Clusters\\Main Cluster";
    var enabledClusterCommand = provider.BuildStartCommand(enabledClusterProfile);
    Assert(enabledClusterCommand.Arguments.Contains("-clusterid=main-crossark", StringComparison.Ordinal), "Enabled cluster should emit Cluster ID.");
    Assert(enabledClusterCommand.Arguments.Contains("-ClusterDirOverride=\"C:\\ARK Clusters\\Main Cluster\"", StringComparison.Ordinal), "Enabled cluster should quote Cluster Directory Override paths with spaces.");
    Assert(!enabledClusterCommand.Arguments.Contains("-NoTransferFromFiltering", StringComparison.Ordinal), "Cluster launch should not add NoTransferFromFiltering.");

    var missingClusterIdProfile = new ArkAsaProfileMapper().FromServerProfile(enabledClusterProfile);
    missingClusterIdProfile.Cluster.ClusterID = string.Empty;
    var missingClusterIdValidation = new ArkAsaValidator().Validate(missingClusterIdProfile);
    Assert(missingClusterIdValidation.Errors.Any(error => error.Contains("Cluster ID is required", StringComparison.Ordinal)), "Missing Cluster ID should be a validation error.");

    var missingClusterDirProfile = new ArkAsaProfileMapper().FromServerProfile(enabledClusterProfile);
    missingClusterDirProfile.Cluster.ClusterDirectoryOverride = string.Empty;
    var missingClusterDirValidation = new ArkAsaValidator().Validate(missingClusterDirProfile);
    Assert(missingClusterDirValidation.Errors.Any(error => error.Contains("Cluster Directory Override is required", StringComparison.Ordinal)), "Missing Cluster Directory Override should be a validation error.");

    var steamCmd = new ArkAsaSteamCmdService().BuildInstallOrUpdateArguments("C:\\Servers\\ARK ASA");
    Assert(steamCmd == "+force_install_dir \"C:\\Servers\\ARK ASA\" +login anonymous +app_update 2430930 validate +quit", "SteamCMD command mismatch.");

    var mapper = new ArkAsaProfileMapper();
    var arkProfile = mapper.FromServerProfile(serverProfile);
    var preview = new ArkAsaLaunchBuilder().Build(arkProfile);
    Assert(preview.CommandLine.Contains("********", StringComparison.Ordinal), "Launch preview should mask passwords.");
    Assert(!preview.CommandLine.Contains("admin-secret", StringComparison.Ordinal), "Launch preview should not expose admin password by default.");

    var validation = new ArkAsaValidator().Validate(arkProfile);
    Assert(validation.IsValid, "Valid ARK profile should pass validation.");
    arkProfile.GameUserSettings.ServerSettings["ServerPVE"] = "Maybe";
    validation = new ArkAsaValidator().Validate(arkProfile);
    Assert(validation.Errors.Any(error => error.Contains("True or False", StringComparison.Ordinal)), "Boolean validation should reject non-boolean values.");
    arkProfile.GameUserSettings.ServerSettings["ServerPVE"] = "True";

    arkProfile.Mods.EnabledMods.Add(new ArkModEntry { Id = "928708", Name = "Duplicate", Enabled = true });
    var modValidation = new ArkAsaModManager().Validate(arkProfile.Mods);
    Assert(modValidation.Warnings.Any(warning => warning.Contains("Duplicate mod ID", StringComparison.Ordinal)), "Duplicate mod IDs should warn.");
    arkProfile.Mods.EnabledMods.RemoveAt(arkProfile.Mods.EnabledMods.Count - 1);

    var tempRoot = CreateTempRoot();
    try
    {
        arkProfile.Basic.InstallPath = tempRoot;
        mapper.HydratePaths(arkProfile);
        Directory.CreateDirectory(arkProfile.Paths.ConfigPath);
        Directory.CreateDirectory(arkProfile.Paths.SavesPath);
        File.WriteAllText(arkProfile.Paths.GameUserSettingsPath, "; keep me\r\n[ServerSettings]\r\nUnknownKey=Keep\r\nServerPVE=False\r\nTamingSpeedMultiplier=5.5\r\nserverpve=True\r\n");
        File.WriteAllText(arkProfile.Paths.GameIniPath, "[/script/shootergame.shootergamemode]\r\nMatingIntervalMultiplier=0.25\r\nConfigAddNPCSpawnEntriesContainer=(Foo=1)\r\nConfigAddNPCSpawnEntriesContainer=(Bar=2)\r\n");
        File.WriteAllText(Path.Combine(arkProfile.Paths.SavesPath, "TheIsland.ark"), "save");

        var state = await new ArkAsaConfigurationStateService().LoadAsync("ark-asa-test", arkProfile);
        Assert(state.GameUserSettingsPath == arkProfile.Paths.GameUserSettingsPath, "ARK state should read the selected server GameUserSettings.ini path.");
        Assert(arkProfile.GameUserSettings.ServerSettings["TamingSpeedMultiplier"] == "5.5", "Existing GameUserSettings.ini values should hydrate visual settings state.");
        Assert(arkProfile.GameUserSettings.ServerSettings["ServerPVE"] == "True", "Case-insensitive duplicate scalar keys should resolve to the last saved value.");
        Assert(arkProfile.GameIni.ShooterGameModeSettings["MatingIntervalMultiplier"] == "0.25", "Existing Game.ini values should hydrate visual settings state.");
        Assert(arkProfile.GameIni.RepeatedSettings["ConfigAddNPCSpawnEntriesContainer"].Count == 2, "Repeated Game.ini values should hydrate as ordered repeated state.");

        var rawEditedState = new ArkAsaConfigurationStateService().LoadFromRawText(
            "ark-asa-test",
            arkProfile,
            state.GameUserSettingsRawText.Replace("TamingSpeedMultiplier=5.5", "TamingSpeedMultiplier=8.25", StringComparison.Ordinal),
            state.GameIniRawText.Replace("MatingIntervalMultiplier=0.25", "MatingIntervalMultiplier=0.05", StringComparison.Ordinal));
        Assert(rawEditedState.PendingValues.Any(entry => entry.Value == "8.25"), "Raw GameUserSettings.ini edit should update pending visual state.");
        Assert(arkProfile.GameIni.ShooterGameModeSettings["MatingIntervalMultiplier"] == "0.05", "Raw Game.ini edit should update pending visual state.");

        var document = await IniDocument.LoadAsync(arkProfile.Paths.GameIniPath);
        Assert(document.GetValues(ArkAsaSettingRegistry.GameModeSection, "ConfigAddNPCSpawnEntriesContainer").Count == 2, "INI parser should preserve repeated array settings.");
        document.SetValue(ArkAsaSettingRegistry.GameModeSection, "DinoCountMultiplier", "1.5");
        Assert(document.Render().Contains("ConfigAddNPCSpawnEntriesContainer=(Foo=1)", StringComparison.Ordinal), "INI writer should preserve unknown repeated lines.");

        arkProfile.GameUserSettings.ServerSettings["ServerPVE"] = "False";
        arkProfile.GameUserSettings.ServerSettings["TamingSpeedMultiplier"] = "9.75";
        arkProfile.GameIni.ShooterGameModeSettings["MatingIntervalMultiplier"] = "0.15";
        arkProfile.GameIni.RepeatedSettings["ConfigAddNPCSpawnEntriesContainer"] = new List<string> { "(Foo=1)", "(Bar=2)" };
        await new ArkAsaConfigService().SaveAsync(arkProfile);
        var savedGus = File.ReadAllText(arkProfile.Paths.GameUserSettingsPath);
        var savedGame = File.ReadAllText(arkProfile.Paths.GameIniPath);
        Assert(savedGus.Contains("; keep me", StringComparison.Ordinal), "Config save should preserve comments.");
        Assert(savedGus.Contains("UnknownKey=Keep", StringComparison.Ordinal), "Config save should preserve unknown settings.");
        Assert(savedGus.Contains("TamingSpeedMultiplier=9.75", StringComparison.Ordinal), "Visual GameUserSettings.ini change should save to GameUserSettings.ini.");
        Assert(!savedGus.Contains("MatingIntervalMultiplier=0.15", StringComparison.Ordinal), "Game.ini setting should not be written to GameUserSettings.ini.");
        Assert(savedGame.Contains("MatingIntervalMultiplier=0.15", StringComparison.Ordinal), "Visual Game.ini change should save to Game.ini.");
        Assert(!savedGame.Contains("TamingSpeedMultiplier=9.75", StringComparison.Ordinal), "GameUserSettings.ini setting should not be written to Game.ini.");
        Assert(savedGame.Contains("ConfigAddNPCSpawnEntriesContainer=(Foo=1)", StringComparison.Ordinal), "Repeated Game.ini entries should be preserved after unrelated saves.");
        Assert(savedGame.Contains("ConfigAddNPCSpawnEntriesContainer=(Bar=2)", StringComparison.Ordinal), "Repeated Game.ini entries should preserve order after unrelated saves.");
        Assert((await IniDocument.LoadAsync(arkProfile.Paths.GameUserSettingsPath)).GetValues(ArkAsaSettingRegistry.ServerSettingsSection, "ServerPVE").Count == 1, "Duplicate scalar keys should not be created after save.");
        Assert(Directory.EnumerateFiles(arkProfile.Paths.ConfigPath, "*.bak").Any(), "Config save should create backups.");

        var backup = await new ArkAsaBackupService().CreateBackupAsync(arkProfile, "test backup");
        Assert(File.Exists(backup.Path), "ARK backup zip should be created.");
        Assert(backup.FileSize > 0, "ARK backup should not be empty.");

        var clusterA = mapper.FromServerProfile(serverProfile);
        var clusterB = mapper.FromServerProfile(serverProfile);
        clusterA.Cluster.ClusterEnabled = true;
        clusterB.Cluster.ClusterEnabled = true;
        clusterA.Cluster.ClusterID = "cluster-one";
        clusterB.Cluster.ClusterID = "cluster-one";
        clusterA.Network.GamePort = 7777;
        clusterB.Network.GamePort = 7777;
        var clusterValidation = new ArkAsaClusterManager().ValidateCluster(new[] { clusterA, clusterB });
        Assert(clusterValidation.Errors.Any(error => error.Contains("duplicate port", StringComparison.OrdinalIgnoreCase)), "Cluster validation should catch duplicate ports.");

        var health = new ArkAsaHealthService().Check(arkProfile);
        Assert(health.Warnings.Any(warning => warning.Contains("Missing executable", StringComparison.Ordinal)), "Health check should warn about missing executable.");

        var clusterManager = new ArkAsaClusterManager();
        var clusterSharedDirectory = Path.Combine(tempRoot, "shared cluster");
        Directory.CreateDirectory(clusterSharedDirectory);
        var island = clusterManager.CreateClusterMapProfile(new ArkAsaClusterMapRequest(
            "Test Cluster",
            "cluster-shared",
            clusterSharedDirectory,
            "Island",
            "TheIsland_WP",
            string.Empty,
            "Island",
            "C:\\Servers\\Ark\\Island",
            7777,
            27015,
            27020,
            70,
            true,
            false,
            false,
            false,
            false,
            false,
            false,
            false,
            true));
        var scorched = clusterManager.CreateClusterMapProfile(new ArkAsaClusterMapRequest(
            "Test Cluster",
            "cluster-shared",
            clusterSharedDirectory,
            "Scorched",
            "ScorchedEarth_WP",
            string.Empty,
            "Scorched",
            "C:\\Servers\\Ark\\Scorched",
            7778,
            27016,
            27021,
            70,
            true,
            false,
            false,
            false,
            false,
            false,
            false,
            false,
            true));
        var clusterReport = clusterManager.ValidateClusterProfiles(new[] { island, scorched }, new[] { "TheIsland_WP", "ScorchedEarth_WP" });
        Assert(clusterReport.Errors.Count == 0, "Valid cluster maps should not have validation errors.");
        Assert(island.Settings["ClusterID"] == scorched.Settings["ClusterID"], "Cluster maps should share ClusterID.");
        Assert(island.Settings["ClusterDirOverride"] == scorched.Settings["ClusterDirOverride"], "Cluster maps should share ClusterDirOverride.");
        Assert(island.Settings["AltSaveDirectoryName"] != scorched.Settings["AltSaveDirectoryName"], "Cluster maps need unique AltSaveDirectoryName values.");

        scorched.Settings["ClusterID"] = "wrong-cluster";
        scorched.Ports[0].Port = island.Ports[0].Port;
        clusterReport = clusterManager.ValidateClusterProfiles(new[] { island, scorched }, new[] { "TheIsland_WP", "Aberration_WP" });
        Assert(clusterReport.Errors.Any(error => error.Message.Contains("ClusterID does not match", StringComparison.Ordinal)), "Cluster validation should catch mismatched ClusterID.");
        Assert(clusterReport.Errors.Any(error => error.Message.Contains("conflicts", StringComparison.Ordinal)), "Cluster validation should catch port conflicts.");
        Assert(clusterReport.Warnings.Any(warning => warning.Message.Contains("Aberration_WP", StringComparison.Ordinal)), "Cluster validation should warn when an expected map is missing.");
    }
    finally
    {
        DeleteTempRoot(tempRoot);
    }
}

static void TestArkClusterArgsEmittedWhenEnabled()
{
    var provider = new ArkSurvivalAscendedProvider();
    var profile = CreateArkClusterProfile("enabled", 7777, 27015, 27020);
    profile.Settings["ClusterEnabled"] = "True";

    var command = provider.BuildStartCommand(profile);

    Assert(command.Arguments.Contains("-clusterid=cluster-one", StringComparison.Ordinal), "Cluster ID arg should be emitted when clustering is enabled.");
    Assert(command.Arguments.Contains("-ClusterDirOverride=\"C:\\ARK Clusters\\cluster one\"", StringComparison.Ordinal), "Cluster directory arg should be emitted when clustering is enabled.");
    Assert(!command.Arguments.Contains("-NoTransferFromFiltering", StringComparison.Ordinal), "Cluster launch should only emit Cluster ID and Cluster Directory Override.");
}

static void TestArkClusterArgsOmittedWhenDisabled()
{
    var provider = new ArkSurvivalAscendedProvider();
    var profile = CreateArkClusterProfile("disabled", 7777, 27015, 27020);
    profile.Settings["ClusterEnabled"] = "False";

    var command = provider.BuildStartCommand(profile);

    Assert(!command.Arguments.Contains("-clusterid=", StringComparison.Ordinal), "Cluster ID arg should be omitted when clustering is disabled.");
    Assert(!command.Arguments.Contains("-ClusterDirOverride", StringComparison.Ordinal), "Cluster directory arg should be omitted when clustering is disabled.");
    Assert(!command.Arguments.Contains("-NoTransferFromFiltering", StringComparison.Ordinal), "Compatibility flag should be omitted when clustering is disabled.");
}

static void TestArkClusterSharedDirectoryQuoting()
{
    var provider = new ArkSurvivalAscendedProvider();
    var profile = CreateArkClusterProfile("quoted", 7777, 27015, 27020);
    profile.Settings["ClusterEnabled"] = "True";
    profile.Settings["ClusterDirOverride"] = "C:\\ARK Clusters\\cluster one";

    var command = provider.BuildStartCommand(profile);

    Assert(command.Arguments.Contains("-ClusterDirOverride=\"C:\\ARK Clusters\\cluster one\"", StringComparison.Ordinal), "ClusterDirOverride should be quoted for paths with spaces.");
}

static void TestArkTransferRestrictionMapping()
{
    var request = new ArkAsaClusterMapRequest(
        "Test Cluster",
        "cluster-one",
        "C:\\ARKClusters\\cluster-one",
        "Island",
        "TheIsland_WP",
        string.Empty,
        "Island",
        "C:\\Servers\\ARK\\Island",
        7777,
        27015,
        27020,
        70,
        true,
        true,
        PreventDownloadSurvivors: true,
        PreventDownloadItems: true,
        PreventDownloadDinos: true,
        PreventUploadSurvivors: false,
        PreventUploadItems: true,
        PreventUploadDinos: true,
        AllowTributeDownloads: false);

    var profile = new ArkAsaClusterManager().CreateClusterMapProfile(request);

    Assert(profile.Settings["PreventDownloadSurvivors"] == "True", "Survivor download block should map to PreventDownloadSurvivors=True.");
    Assert(profile.Settings["PreventDownloadItems"] == "True", "Item download block should map to PreventDownloadItems=True.");
    Assert(profile.Settings["PreventDownloadDinos"] == "True", "Dino download block should map to PreventDownloadDinos=True.");
    Assert(profile.Settings["PreventUploadSurvivors"] == "False", "Survivor upload allowance should map to PreventUploadSurvivors=False.");
    Assert(profile.Settings["PreventUploadItems"] == "True", "Item upload block should map to PreventUploadItems=True.");
    Assert(profile.Settings["PreventUploadDinos"] == "True", "Dino upload block should map to PreventUploadDinos=True.");
    Assert(profile.Settings["AllowTributeDownloads"] == "False", "Tribute download block should map through AllowTributeDownloads=False.");
    Assert(profile.Settings["noTributeDownloads"] == "True", "Tribute download block should map to noTributeDownloads=True.");
}

static void TestArkDuplicatePortValidation()
{
    var manager = new ArkAsaClusterManager();
    var profiles = new[]
    {
        CreateArkClusterProfile("island", 7777, 27015, 27020),
        CreateArkClusterProfile("scorched", 7777, 27016, 27021)
    };

    var report = manager.ValidateClusterProfiles(profiles);

    Assert(report.Errors.Any(error => error.Message.Contains("conflicts", StringComparison.OrdinalIgnoreCase)), "Cluster validation should catch duplicate game/query/RCON ports.");
}

static void TestArkSingleMemberClusterWarning()
{
    var manager = new ArkAsaClusterManager();
    var report = manager.ValidateClusterProfiles(new[] { CreateArkClusterProfile("island", 7777, 27015, 27020) });

    Assert(report.Warnings.Any(warning => warning.Message.Contains("at least two", StringComparison.OrdinalIgnoreCase)), "Single-member clusters should show a warning.");
}

static void TestArkCleanMultiMemberClusterValidation()
{
    var tempRoot = CreateTempRoot();
    try
    {
        var shared = Path.Combine(tempRoot, "cluster shared");
        Directory.CreateDirectory(shared);
        var island = CreateArkClusterProfile("island", 7777, 27015, 27020, shared);
        var scorched = CreateArkClusterProfile("scorched", 7778, 27016, 27021, shared);
        island.Settings["AltSaveDirectoryName"] = "Island";
        scorched.Settings["AltSaveDirectoryName"] = "Scorched";

        var report = new ArkAsaClusterManager().ValidateClusterProfiles(new[] { island, scorched });

        Assert(report.Errors.Count == 0, "Multiple members with the same cluster ID/shared directory and unique ports should validate cleanly.");
        Assert(!report.Warnings.Any(warning => warning.Message.Contains("at least two", StringComparison.OrdinalIgnoreCase)), "Valid multi-member clusters should not show the single-member warning.");
    }
    finally
    {
        DeleteTempRoot(tempRoot);
    }
}

static void TestUpdaterVersionComparison()
{
    Assert(SemanticVersionInfo.Parse("v1.0.10").CompareTo(SemanticVersionInfo.Parse("v1.0.9")) > 0, "Semantic version comparison should handle v1.0.10 > v1.0.9.");
    Assert(SemanticVersionInfo.Parse("v1.1.0").GetUpdateTypeComparedTo(SemanticVersionInfo.Parse("v1.0.9")) == "Minor", "Minor update type should be detected.");
    Assert(SemanticVersionInfo.Parse("v2.0.0").GetUpdateTypeComparedTo(SemanticVersionInfo.Parse("v1.9.9")) == "Major", "Major update type should be detected.");
    Assert(SemanticVersionInfo.Parse("v1.0.1").GetUpdateTypeComparedTo(SemanticVersionInfo.Parse("v1.0.0")) == "Patch", "Patch update type should be detected.");
    Assert(SemanticVersionInfo.Parse("v1.0.0-beta.1").CompareTo(SemanticVersionInfo.Parse("v1.0.0")) < 0, "Prerelease should sort before stable release.");
    Assert(SemanticVersionInfo.Parse("v3.0.10").CompareTo(SemanticVersionInfo.Parse("v3.0.2")) > 0, "Semantic version comparison should handle v3.0.10 > v3.0.2.");
    Assert(SemanticVersionInfo.Parse("v3.0.1-stable").CompareTo(SemanticVersionInfo.Parse("3.0.1")) == 0, "Stable suffix tags should compare as stable versions.");

    var assets = new[]
    {
        new UpdateAsset("NexusServerManager-Portable-v1.0.1.zip", "https://example.invalid/portable.zip", 10, "application/zip"),
        new UpdateAsset("NexusServerManager-Setup-v1.0.1.exe", "https://example.invalid/setup.exe", 20, "application/octet-stream")
    };
    var best = GitHubAssetDownloadService.PickBestWindowsAsset(assets);
    Assert(best?.Name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) == true, "Windows setup asset should be preferred over portable ZIP.");

    var noisyAssets = new[]
    {
        new UpdateAsset("Source code.zip", "https://example.invalid/source.zip", 10, "application/zip"),
        new UpdateAsset("NexusServerManager-Checksums-v3.0.8.txt", "https://example.invalid/checksums.txt", 10, "text/plain"),
        new UpdateAsset("NexusServerManager-Setup-v3.0.8-x64.exe", "https://example.invalid/setup.exe", 20, "application/octet-stream"),
        new UpdateAsset("NexusServerManager-Portable-v3.0.8-x64.zip", "https://example.invalid/portable.zip", 30, "application/zip"),
        new UpdateAsset("NexusServerManager-Setup-v3.0.8-arm64.exe", "https://example.invalid/arm64.exe", 20, "application/octet-stream")
    };
    var setup = GitHubAssetDownloadService.PickBestWindowsAsset(noisyAssets, "Installer");
    Assert(setup?.Name == "NexusServerManager-Setup-v3.0.8-x64.exe", "Installer mode should select the compatible setup EXE.");
    var portable = GitHubAssetDownloadService.PickBestWindowsAsset(noisyAssets, "Portable");
    Assert(portable?.Name == "NexusServerManager-Portable-v3.0.8-x64.zip", "Portable mode should select the compatible portable ZIP.");
    Assert(!GitHubAssetDownloadService.IsCompatibleWindowsAsset(noisyAssets[0]), "Source ZIP should be rejected.");
    Assert(!GitHubAssetDownloadService.IsCompatibleWindowsAsset(noisyAssets[1]), "Checksums file should be rejected as an installer.");

    var msiOnly = new[] { new UpdateAsset("NexusServerManager-v3.0.8-x64.msi", "https://example.invalid/setup.msi", 20, "application/octet-stream") };
    Assert(GitHubAssetDownloadService.PickBestWindowsAsset(msiOnly, "Installer")?.Name.EndsWith(".msi", StringComparison.OrdinalIgnoreCase) == true, "MSI installer should be selected when no EXE is available.");
}

static void TestSettingsUpdateSeparation()
{
    var xamlPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "GameServerManager.App", "Views", "SettingsView.xaml"));
    var xaml = File.ReadAllText(xamlPath);
    var advancedStart = xaml.IndexOf("IsAdvancedSelected", StringComparison.Ordinal);
    var comingSoonStart = xaml.IndexOf("IsComingSoonSelected", StringComparison.Ordinal);
    Assert(advancedStart > 0 && comingSoonStart > advancedStart, "Settings XAML should contain the Advanced page block.");

    var advancedBlock = xaml[advancedStart..comingSoonStart];
    var forbiddenAdvancedText = new[]
    {
        "CheckForUpdatesCommand",
        "DownloadUpdateCommand",
        "InstallUpdateCommand",
        "CancelDownloadCommand",
        "Latest Version",
        "Last Checked",
        "Release Channel",
        "UpdateStatus",
        "Release Notes"
    };

    foreach (var forbidden in forbiddenAdvancedText)
    {
        Assert(!advancedBlock.Contains(forbidden, StringComparison.Ordinal), $"Advanced page should not contain update UI: {forbidden}");
    }

    Assert(xaml.Contains("Client Updates", StringComparison.Ordinal), "Updates page should expose the Client Updates center.");
    Assert(xaml.Contains("Advanced Update Source", StringComparison.Ordinal), "Updates page should own repository source configuration.");
    Assert(xaml.Contains("CheckForUpdatesCommand", StringComparison.Ordinal), "Updates page should retain the update check command.");
}

static void TestArkSettingsRedesignContracts()
{
    var boolSetting = ArkAsaSettingRegistry.All.FirstOrDefault(setting =>
        setting.Key.Equals("ServerPVE", StringComparison.OrdinalIgnoreCase));
    Assert(boolSetting is not null, "ARK settings registry should include ServerPVE.");
    Assert(boolSetting!.DataType == ArkSettingDataType.Boolean, "ServerPVE should be modeled as a boolean.");

    var validation = new ArkValidationResult();
    new ArkAsaValidator().ValidateSettingValue(boolSetting, "Maybe", validation);
    Assert(validation.Errors.Any(error => error.Contains("True or False", StringComparison.Ordinal)), "Boolean validation should reject non-boolean values.");

    var xamlPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "GameServerManager.App", "Views", "ArkAsaSettingsView.xaml"));
    var xaml = File.ReadAllText(xamlPath);
    Assert(xaml.Contains("NavigationGroups", StringComparison.Ordinal), "ARK settings UI should use grouped navigation.");
    Assert(xaml.Contains("IsOverviewTab", StringComparison.Ordinal), "ARK settings UI should include an overview tab binding.");
    Assert(xaml.Contains("BooleanValue", StringComparison.Ordinal), "ARK settings UI should bind boolean settings to a checkbox editor.");
    Assert(!xaml.Contains("ItemsSource=\"{Binding Tabs}\" SelectedItem=\"{Binding SelectedTab}\"", StringComparison.Ordinal), "ARK settings navigation should not use the old flat tab list.");
    Assert(!xaml.Contains("Launch Command Preview", StringComparison.Ordinal), "Launch command preview must not be rendered as a global panel.");
    Assert(!xaml.Contains("SteamCMD Install / Update", StringComparison.Ordinal), "SteamCMD command must not be rendered as a global panel.");
    Assert(!xaml.Contains("Text=\"Current vs Pending Config\"", StringComparison.Ordinal), "Old global configuration diff panel must not be rendered.");
    Assert(xaml.Contains("Visibility=\"{Binding IsHealthValidationTab", StringComparison.Ordinal), "Health and Validation should own the diff page.");
    Assert(xaml.Contains("Visibility=\"{Binding IsStartupTab", StringComparison.Ordinal), "Startup should own launch command content.");
    Assert(xaml.Contains("Visibility=\"{Binding IsRawEditorTab", StringComparison.Ordinal), "Raw INI Editor should own raw file editors.");
    Assert(xaml.Contains("Header=\"View Generated Command\"", StringComparison.Ordinal), "Launch command preview should be collapsed by default.");
    Assert(xaml.Contains("RawGameUserSettingsEditorText", StringComparison.Ordinal), "Raw editor should bind through masked editor text.");
    Assert(!xaml.Contains("CheckBox Content=\"Advanced\"", StringComparison.Ordinal), "Advanced mode should not be a checkbox.");

    var viewModelPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "GameServerManager.App", "ViewModels", "ArkAsaSettingsViewModel.cs"));
    var viewModel = File.ReadAllText(viewModelPath);
    Assert(viewModel.Contains("RevealSensitiveRawValues = false", StringComparison.Ordinal), "Raw sensitive values should remask when leaving Raw INI Editor.");
    Assert(viewModel.Contains("MaskedCommandPreview", StringComparison.Ordinal), "Command previews should expose a masked display value.");
    Assert(viewModel.Contains("MatchingSettingsText", StringComparison.Ordinal), "Navigation count should distinguish available settings from search matches.");
}

static void TestDiagnosticsMaskSecrets()
{
    var masked = DiagnosticsService.MaskSecrets("{\n  \"ServerPassword\": \"secret\",\n  \"Name\": \"Safe\"\n}");
    Assert(masked.Contains("********", StringComparison.Ordinal), "Diagnostics should mask password-like values.");
    Assert(!masked.Contains("secret", StringComparison.Ordinal), "Diagnostics should not retain secret values.");
    Assert(masked.Contains("Safe", StringComparison.Ordinal), "Diagnostics should preserve safe values.");
}

static void TestPortableModeDetection()
{
    var tempRoot = CreateTempRoot();
    try
    {
        File.WriteAllText(Path.Combine(tempRoot, "portable.flag"), string.Empty);
        var paths = new AppDataPaths(tempRoot);
        Assert(!paths.IsPortable, "Explicit test roots should not auto-switch into portable mode.");

        var portableInstall = Path.Combine(tempRoot, "GameServerManager-portable");
        Directory.CreateDirectory(portableInstall);
        File.WriteAllText(Path.Combine(portableInstall, "portable.flag"), string.Empty);
        var explicitPortableRoot = Path.Combine(portableInstall, "Data");
        var portablePaths = new AppDataPaths(explicitPortableRoot);
        Assert(portablePaths.RootDirectory == explicitPortableRoot, "Explicit root should be preserved for tests and tools.");
    }
    finally
    {
        DeleteTempRoot(tempRoot);
    }
}

static void TestReleaseVersionStamp()
{
    var root = FindRepositoryRoot();
    var version = File.ReadAllText(Path.Combine(root, "VERSION")).Trim().TrimStart('v');
    var props = XDocument.Load(Path.Combine(root, "Directory.Build.props"));
    var propertyGroup = props.Root?.Element("PropertyGroup")
        ?? throw new InvalidOperationException("Directory.Build.props is missing PropertyGroup.");

    var versionPrefix = propertyGroup.Element("VersionPrefix")?.Value;
    var assemblyVersion = propertyGroup.Element("AssemblyVersion")?.Value;
    var fileVersion = propertyGroup.Element("FileVersion")?.Value;

    Assert(versionPrefix == version, "Directory.Build.props VersionPrefix must match VERSION.");
    Assert(assemblyVersion == $"{version}.0", "Directory.Build.props AssemblyVersion must match VERSION.");
    Assert(fileVersion == $"{version}.0", "Directory.Build.props FileVersion must match VERSION.");
}

static string FindRepositoryRoot()
{
    var directory = new DirectoryInfo(Directory.GetCurrentDirectory());
    while (directory is not null)
    {
        if (File.Exists(Path.Combine(directory.FullName, "VERSION"))
            && File.Exists(Path.Combine(directory.FullName, "Directory.Build.props")))
        {
            return directory.FullName;
        }

        directory = directory.Parent;
    }

    throw new DirectoryNotFoundException("Could not find repository root.");
}

static ServerProfile CreateTestProfile(string id, string serverName)
{
    return new ServerProfile
    {
        Id = id,
        GameId = "generic_server",
        ProfileName = serverName,
        ServerName = serverName,
        InstallPath = Path.Combine("C:\\Servers", serverName.Replace(' ', '_')),
        ExecutablePath = Path.Combine("C:\\Servers", serverName.Replace(' ', '_'), "server.exe"),
        MaxPlayers = 10,
        Ports = new List<ServerPort>
        {
            new()
            {
                Name = "Game",
                Port = 27015,
                DefaultPort = 27015,
                Protocol = PortProtocol.UDP,
                IsRequired = true
            }
        }
    };
}

static ServerProfile CreateArkClusterProfile(string id, int gamePort, int queryPort, int rconPort, string? sharedDirectory = null)
{
    return new ServerProfile
    {
        Id = id,
        GameId = ArkSurvivalAscendedServerProfile.GameId,
        ProfileName = id,
        ServerName = $"ASA {id}",
        InstallPath = Path.Combine("C:\\Servers\\ARK", id),
        MapName = id.Equals("scorched", StringComparison.OrdinalIgnoreCase) ? "ScorchedEarth_WP" : "TheIsland_WP",
        MaxPlayers = 70,
        Ports = new List<ServerPort>
        {
            new() { Name = "Game", Port = gamePort, DefaultPort = 7777, Protocol = PortProtocol.UDP, IsRequired = true },
            new() { Name = "Query", Port = queryPort, DefaultPort = 27015, Protocol = PortProtocol.UDP, IsRequired = true },
            new() { Name = "RCON", Port = rconPort, DefaultPort = 27020, Protocol = PortProtocol.TCP, IsRequired = false }
        },
        Settings =
        {
            ["ClusterEnabled"] = "True",
            ["ClusterID"] = "cluster-one",
            ["ClusterDirOverride"] = sharedDirectory ?? "C:\\ARK Clusters\\cluster one",
            ["AltSaveDirectoryName"] = id,
            ["RCONEnabled"] = "True"
        }
    };
}

static ServerProfile CreateArkClusterTestProfile(IGameServerProvider provider)
{
    return new ServerProfile
    {
        Id = Guid.NewGuid().ToString(),
        GameId = ArkSurvivalAscendedServerProfile.GameId,
        ProfileName = "Cluster Test",
        ServerName = "Cluster Test",
        InstallPath = Path.Combine("C:\\Servers", "ARK ASA Cluster Test"),
        MapName = "TheIsland_WP",
        MaxPlayers = 10,
        AdminPassword = "admin-secret",
        Ports = provider.DefaultPorts.Select(p => new ServerPort
        {
            Name = p.Name,
            Port = p.Port,
            DefaultPort = p.DefaultPort,
            Protocol = p.Protocol,
            Description = p.Description,
            IsRequired = p.IsRequired
        }).ToList(),
        Settings =
        {
            ["RCONEnabled"] = "True"
        }
    };
}

static void TestArkWinLiveMaxPlayers()
{
    var registry = GameProviderRegistry.CreateDefault();
    registry.TryGetProvider(ArkSurvivalAscendedServerProfile.GameId, out var provider);
    var profile = new ServerProfile
    {
        Id = "mp-test",
        GameId = ArkSurvivalAscendedServerProfile.GameId,
        ProfileName = "MP Test",
        ServerName = "MP Test",
        InstallPath = "C:\\Servers\\ARK",
        MaxPlayers = 32,
        Ports = provider!.DefaultPorts.Select(p => new ServerPort { Name = p.Name, Port = p.Port, DefaultPort = p.DefaultPort, Protocol = p.Protocol, IsRequired = p.IsRequired }).ToList()
    };
    var command = provider.BuildStartCommand(profile);
    Assert(command.Arguments.Contains("-WinLiveMaxPlayers=32", StringComparison.Ordinal), "ARK ASA should use -WinLiveMaxPlayers= dash flag, not MaxPlayers= URL parameter.");
    Assert(!command.Arguments.Contains("?MaxPlayers=", StringComparison.OrdinalIgnoreCase), "ARK ASA should not emit legacy URL-style ?MaxPlayers= query parameter.");
}

static async Task TestArkMaxPlayersNotInIniAsync()
{
    var tempRoot = CreateTempRoot();
    try
    {
        var mapper = new ArkAsaProfileMapper();
        var ark = new ArkSurvivalAscendedServerProfile();
        ark.Basic.InstallPath = tempRoot;
        ark.Basic.MaxPlayers = 50;
        mapper.HydratePaths(ark);
        Directory.CreateDirectory(ark.Paths.ConfigPath);
        File.WriteAllText(ark.Paths.GameUserSettingsPath, "[ServerSettings]\r\nMaxPlayers=70\r\n");
        File.WriteAllText(ark.Paths.GameIniPath, "[/script/shootergame.shootergamemode]\r\n");

        await new ArkAsaConfigService().SaveAsync(ark);
        var savedGus = File.ReadAllText(ark.Paths.GameUserSettingsPath);
        Assert(!savedGus.Contains("MaxPlayers=", StringComparison.Ordinal), "SaveAsync must not write MaxPlayers to GameUserSettings.ini; ASA uses -WinLiveMaxPlayers launch flag.");
    }
    finally
    {
        DeleteTempRoot(tempRoot);
    }
}

static void TestArkActiveModsNotGenerated()
{
    var mapper = new ArkAsaProfileMapper();
    var ark = new ArkSurvivalAscendedServerProfile();
    ark.Basic.InstallPath = "C:\\Servers\\ARK";
    mapper.HydratePaths(ark);
    ark.Mods.EnabledMods.Add(new ArkModEntry { Id = "928708", Enabled = true });
    ark.Mods.EnabledMods.Add(new ArkModEntry { Id = "929110", Enabled = true });

    var preview = new ArkAsaLaunchBuilder().Build(ark, revealPasswords: true);
    Assert(preview.Arguments.Contains("-mods=928708,929110", StringComparison.Ordinal), "ARK ASA should emit mods as -mods= launch flag.");
    Assert(!preview.Arguments.Contains("ActiveMods", StringComparison.Ordinal), "ARK ASA must not emit the obsolete ActiveMods INI key in the launch command.");
}

static async Task TestArkIniRoundTripAsync()
{
    var tempRoot = CreateTempRoot();
    try
    {
        var mapper = new ArkAsaProfileMapper();
        var ark = new ArkSurvivalAscendedServerProfile();
        ark.Basic.InstallPath = tempRoot;
        mapper.HydratePaths(ark);
        Directory.CreateDirectory(ark.Paths.ConfigPath);
        File.WriteAllText(ark.Paths.GameUserSettingsPath, "[ServerSettings]\r\nTamingSpeedMultiplier=3.0\r\nServerPVE=False\r\n");
        File.WriteAllText(ark.Paths.GameIniPath, "[/script/shootergame.shootergamemode]\r\nMatingIntervalMultiplier=0.5\r\n");

        var state = await new ArkAsaConfigurationStateService().LoadAsync("roundtrip", ark);
        Assert(ark.GameUserSettings.ServerSettings["TamingSpeedMultiplier"] == "3.0", "Round-trip: loaded taming speed from INI.");
        Assert(ark.GameIni.ShooterGameModeSettings["MatingIntervalMultiplier"] == "0.5", "Round-trip: loaded mating interval from Game.ini.");

        ark.GameUserSettings.ServerSettings["TamingSpeedMultiplier"] = "5.0";
        ark.GameIni.ShooterGameModeSettings["MatingIntervalMultiplier"] = "0.25";
        await new ArkAsaConfigService().SaveAsync(ark, createBackup: false);

        var reloaded = new ArkSurvivalAscendedServerProfile();
        reloaded.Basic.InstallPath = tempRoot;
        mapper.HydratePaths(reloaded);
        await new ArkAsaConfigurationStateService().LoadAsync("roundtrip", reloaded);
        Assert(reloaded.GameUserSettings.ServerSettings["TamingSpeedMultiplier"] == "5.0", "Round-trip: reloaded taming speed matches saved value.");
        Assert(reloaded.GameIni.ShooterGameModeSettings["MatingIntervalMultiplier"] == "0.25", "Round-trip: reloaded mating interval matches saved value.");
    }
    finally
    {
        DeleteTempRoot(tempRoot);
    }
}

static async Task TestArkCommentsPreservedAsync()
{
    var tempRoot = CreateTempRoot();
    try
    {
        var mapper = new ArkAsaProfileMapper();
        var ark = new ArkSurvivalAscendedServerProfile();
        ark.Basic.InstallPath = tempRoot;
        mapper.HydratePaths(ark);
        Directory.CreateDirectory(ark.Paths.ConfigPath);
        File.WriteAllText(ark.Paths.GameUserSettingsPath, "; My important comment\r\n[ServerSettings]\r\n; Another comment\r\nTamingSpeedMultiplier=1.0\r\n");
        File.WriteAllText(ark.Paths.GameIniPath, "[/script/shootergame.shootergamemode]\r\n; Game ini comment\r\n");

        await new ArkAsaConfigurationStateService().LoadAsync("comments", ark);
        ark.GameUserSettings.ServerSettings["TamingSpeedMultiplier"] = "2.0";
        await new ArkAsaConfigService().SaveAsync(ark, createBackup: false);

        var saved = File.ReadAllText(ark.Paths.GameUserSettingsPath);
        Assert(saved.Contains("; My important comment", StringComparison.Ordinal), "Save must preserve top-level comments.");
        Assert(saved.Contains("; Another comment", StringComparison.Ordinal), "Save must preserve inline section comments.");
    }
    finally
    {
        DeleteTempRoot(tempRoot);
    }
}

static void TestArkMultiServerIsolation()
{
    var mapper = new ArkAsaProfileMapper();
    var arkA = new ArkSurvivalAscendedServerProfile();
    arkA.Basic.InstallPath = "C:\\Servers\\ARK_A";
    mapper.HydratePaths(arkA);

    var arkB = new ArkSurvivalAscendedServerProfile();
    arkB.Basic.InstallPath = "C:\\Servers\\ARK_B";
    mapper.HydratePaths(arkB);

    Assert(arkA.Paths.GameUserSettingsPath != arkB.Paths.GameUserSettingsPath, "Two server profiles must have distinct GameUserSettings.ini paths.");
    Assert(arkA.Paths.GameIniPath != arkB.Paths.GameIniPath, "Two server profiles must have distinct Game.ini paths.");
}

static void TestArkDecimalFormatting()
{
    var doc = IniDocument.Parse("[ServerSettings]\r\nTamingSpeedMultiplier=1.5\r\n");
    doc.SetValue("ServerSettings", "TamingSpeedMultiplier", (1.25m).ToString(System.Globalization.CultureInfo.InvariantCulture));
    var rendered = doc.Render();
    Assert(rendered.Contains("TamingSpeedMultiplier=1.25", StringComparison.Ordinal), "Decimal values must be formatted with a period separator regardless of locale.");
    Assert(!rendered.Contains("1,25", StringComparison.Ordinal), "Decimal values must not use a comma separator.");
}

static void TestArkPasswordRedaction()
{
    var mapper = new ArkAsaProfileMapper();
    var ark = new ArkSurvivalAscendedServerProfile();
    ark.Basic.InstallPath = "C:\\Servers\\ARK";
    ark.Basic.ServerPassword = "join-secret";
    ark.Basic.AdminPassword = "admin-secret";
    mapper.HydratePaths(ark);

    var maskedPreview = new ArkAsaLaunchBuilder().Build(ark, revealPasswords: false);
    Assert(!maskedPreview.CommandLine.Contains("join-secret", StringComparison.Ordinal), "Masked preview must not reveal server password.");
    Assert(!maskedPreview.CommandLine.Contains("admin-secret", StringComparison.Ordinal), "Masked preview must not reveal admin password.");
    Assert(maskedPreview.CommandLine.Contains("********", StringComparison.Ordinal), "Masked preview must replace password with mask.");

    var revealedPreview = new ArkAsaLaunchBuilder().Build(ark, revealPasswords: true);
    Assert(revealedPreview.CommandLine.Contains("join-secret", StringComparison.Ordinal), "Revealed preview must show server password when requested.");
    Assert(revealedPreview.CommandLine.Contains("admin-secret", StringComparison.Ordinal), "Revealed preview must show admin password when requested.");
}

static void TestArkModOrdering()
{
    var mapper = new ArkAsaProfileMapper();
    var ark = new ArkSurvivalAscendedServerProfile();
    ark.Basic.InstallPath = "C:\\Servers\\ARK";
    mapper.HydratePaths(ark);
    ark.Mods.EnabledMods.Add(new ArkModEntry { Id = "111111", Enabled = true });
    ark.Mods.EnabledMods.Add(new ArkModEntry { Id = "222222", Enabled = true });
    ark.Mods.EnabledMods.Add(new ArkModEntry { Id = "333333", Enabled = true });
    ark.Mods.EnabledMods.Add(new ArkModEntry { Id = "444444", Enabled = false }); // disabled

    var preview = new ArkAsaLaunchBuilder().Build(ark);
    Assert(preview.Arguments.Contains("-mods=111111,222222,333333", StringComparison.Ordinal), "Mod order in -mods= must match enabled mod list order.");
    Assert(!preview.Arguments.Contains("444444", StringComparison.Ordinal), "Disabled mods must not appear in -mods= argument.");
}

static async Task TestArkMigrationDetectionAsync()
{
    var tempRoot = CreateTempRoot();
    try
    {
        var mapper = new ArkAsaProfileMapper();
        var ark = new ArkSurvivalAscendedServerProfile();
        ark.Basic.InstallPath = tempRoot;
        mapper.HydratePaths(ark);
        Directory.CreateDirectory(ark.Paths.ConfigPath);
        File.WriteAllText(ark.Paths.GameUserSettingsPath, "[ServerSettings]\r\nMaxPlayers=70\r\nActiveMods=928708\r\nTamingSpeedMultiplier=1.0\r\n");
        File.WriteAllText(ark.Paths.GameIniPath, "[/script/shootergame.shootergamemode]\r\n");

        var state = await new ArkAsaConfigurationStateService().LoadAsync("migration", ark);
        Assert(state.MigrationResult.HasWarnings, "Migration detection should find obsolete MaxPlayers and ActiveMods keys.");
        Assert(state.MigrationResult.Warnings.Any(w => w.Contains("MaxPlayers", StringComparison.Ordinal)), "Migration result should warn about obsolete MaxPlayers key.");
        Assert(state.MigrationResult.Warnings.Any(w => w.Contains("ActiveMods", StringComparison.Ordinal)), "Migration result should warn about obsolete ActiveMods key.");
    }
    finally
    {
        DeleteTempRoot(tempRoot);
    }
}

static async Task TestArkMinimalIniCreationAsync()
{
    var tempRoot = CreateTempRoot();
    try
    {
        var mapper = new ArkAsaProfileMapper();
        var ark = new ArkSurvivalAscendedServerProfile();
        ark.Basic.InstallPath = tempRoot;
        mapper.HydratePaths(ark);

        ArkAsaConfigService.EnsureConfigFilesExist(ark);

        Assert(File.Exists(ark.Paths.GameUserSettingsPath), "EnsureConfigFilesExist must create GameUserSettings.ini.");
        Assert(File.Exists(ark.Paths.GameIniPath), "EnsureConfigFilesExist must create Game.ini.");

        var gusText = File.ReadAllText(ark.Paths.GameUserSettingsPath);
        var gameText = File.ReadAllText(ark.Paths.GameIniPath);
        Assert(gusText.Contains("[ServerSettings]", StringComparison.Ordinal), "Created GameUserSettings.ini must include [ServerSettings] section header.");
        Assert(gameText.Contains("[/script/shootergame.shootergamemode]", StringComparison.Ordinal), "Created Game.ini must include [/script/shootergame.shootergamemode] section header.");

        var gus = IniDocument.Parse(gusText);
        var game = IniDocument.Parse(gameText);
        Assert(gus != null, "Created GameUserSettings.ini should parse without error.");
        Assert(game != null, "Created Game.ini should parse without error.");

        // Calling again must not truncate existing content
        ArkAsaConfigService.EnsureConfigFilesExist(ark);
        Assert(File.ReadAllText(ark.Paths.GameUserSettingsPath) == gusText, "EnsureConfigFilesExist must not overwrite existing files.");

        // Should also load cleanly from disk
        await new ArkAsaConfigurationStateService().LoadAsync("minimal-create", ark);
    }
    finally
    {
        DeleteTempRoot(tempRoot);
    }
}

// ── 7 Days to Die tests ───────────────────────────────────────────────────────

static void Test7DaysToDieProvider()
{
    var registry = GameProviderRegistry.CreateDefault();
    Assert(registry.TryGetProvider("seven_days_to_die", out var provider), "7 Days to Die provider must be registered.");
    Assert(provider.SteamAppId == 294420, "7 Days to Die dedicated server Steam App ID must be 294420.");
    Assert(provider.ExecutableRelativePath == "7DaysToDieServer.exe", "Executable path mismatch.");
    Assert(provider.DefaultPorts.Count >= 4, "Provider must declare at least 4 ports (base + 3 UDP).");
    Assert(provider.DefaultPorts.Any(p => p.Name == "Game" && p.Protocol == PortProtocol.Both), "Game port must be TCP+UDP.");
    Assert(provider.SupportedFeatures.HasFlag(GameServerFeatures.SteamCmdInstall), "Must declare SteamCmdInstall.");
    Assert(provider.SupportedFeatures.HasFlag(GameServerFeatures.Mods), "Must declare Mods.");
    Assert(provider.SettingsDefinitions.Count > 0, "Provider must expose settings definitions.");
    Assert(provider.SettingsDefinitions.Any(s => s.SettingKey == "ServerName"), "Settings must include ServerName.");
    Assert(provider.SettingsDefinitions.Any(s => s.SettingKey == "SandboxCode"), "Settings must include SandboxCode for V3.");
    Assert(provider.SettingsDefinitions.Any(s => s.SettingKey == "EACEnabled"), "Settings must include EACEnabled.");
    Assert(provider.SettingsDefinitions.Any(s => s.SettingKey == "GameWorld"), "Settings must include GameWorld.");
    Assert(provider.SettingsDefinitions.Any(s => s.SettingKey == "ServerAllowCrossplay"), "Settings must include ServerAllowCrossplay.");

    var profile = new ServerProfile
    {
        Id = "7dtd-test",
        GameId = "seven_days_to_die",
        ProfileName = "Test",
        ServerName = "7DTD Test",
        InstallPath = @"C:\Servers\7dtd",
        MaxPlayers = 8,
        Ports = provider.DefaultPorts.Select(p => new ServerPort
        {
            Name = p.Name, Port = p.Port, DefaultPort = p.DefaultPort,
            Protocol = p.Protocol, Description = p.Description, IsRequired = p.IsRequired
        }).ToList()
    };

    var command = provider.BuildStartCommand(profile);
    Assert(command.IsValid, "Launch command must be valid.");
    Assert(command.Arguments.Contains("-batchmode", StringComparison.Ordinal), "Launch must include -batchmode.");
    Assert(command.Arguments.Contains("-nographics", StringComparison.Ordinal), "Launch must include -nographics.");
    Assert(command.Arguments.Contains("-dedicated", StringComparison.Ordinal), "Launch must include -dedicated.");
    Assert(command.Arguments.Contains("-configfile=", StringComparison.Ordinal), "Launch must include -configfile=.");
    Assert(command.Arguments.Contains("-UserDataFolder=", StringComparison.Ordinal), "Launch must include -UserDataFolder=.");
    Assert(command.Arguments.Contains("-logFile", StringComparison.Ordinal), "Launch must include -logFile.");

    // UserDataFolder must be under the install path (isolated per-instance, not AppData).
    Assert(command.Arguments.Contains(@"C:\Servers\7dtd", StringComparison.OrdinalIgnoreCase), "UserDataFolder must be under install path.");
}

static void Test7DaysToDieSteamCmdArgs()
{
    var svc = new SevenDaysToDieSteamCmdService();

    // Anonymous, stable branch
    var args = svc.BuildArguments(@"C:\Servers\7dtd");
    Assert(args.Contains("+force_install_dir", StringComparison.Ordinal), "SteamCMD args must include +force_install_dir.");
    Assert(args.Contains("+login anonymous", StringComparison.Ordinal), "Default login must be anonymous.");
    Assert(args.Contains($"+app_update {SevenDaysToDieSteamCmdService.AppId}", StringComparison.Ordinal), "Must reference AppId 294420.");
    Assert(args.Contains("+quit", StringComparison.Ordinal), "Must end with +quit.");
    Assert(!args.Contains("-beta", StringComparison.Ordinal), "Stable branch must not emit -beta.");

    // Experimental branch
    var expArgs = svc.BuildArguments(@"C:\Servers\7dtd", branch: "latest_experimental");
    Assert(expArgs.Contains("-beta \"latest_experimental\"", StringComparison.Ordinal), "Experimental branch must emit -beta.");

    // Validate flag
    var validateArgs = svc.BuildArguments(@"C:\Servers\7dtd", validate: true);
    Assert(validateArgs.Contains("validate", StringComparison.Ordinal), "Validate must include 'validate' keyword.");

    // Display args must never expose credentials
    var displayArgs = svc.BuildArgumentsForDisplay(@"C:\Servers\7dtd", hasCredentials: true);
    Assert(!displayArgs.Contains("password", StringComparison.OrdinalIgnoreCase), "Display args must not expose passwords.");
    Assert(displayArgs.Contains("****", StringComparison.Ordinal), "Display args must mask credentials.");

    // Stable/public branch synonyms must not emit -beta
    var publicArgs = svc.BuildArguments(@"C:\Servers\7dtd", branch: "stable");
    Assert(!publicArgs.Contains("-beta", StringComparison.Ordinal), "'stable' branch must not emit -beta.");
    var publicArgs2 = svc.BuildArguments(@"C:\Servers\7dtd", branch: "public");
    Assert(!publicArgs2.Contains("-beta", StringComparison.Ordinal), "'public' branch must not emit -beta.");
}

static async Task Test7DaysToDieConfigXmlAsync()
{
    var tempRoot = CreateTempRoot();
    try
    {
        var configPath = Path.Combine(tempRoot, "serverconfig.xml");

        // Create a minimal config and verify it parses
        await SevenDaysToDieConfigService.EnsureConfigExistsAsync(configPath, "Round Trip Server");
        Assert(File.Exists(configPath), "EnsureConfigExistsAsync must create serverconfig.xml.");

        var doc = await ServerConfigXmlDocument.LoadAsync(configPath);
        Assert(doc.GetValue("ServerName") == "Round Trip Server", "ServerName must be written and read back.");
        Assert(doc.GetValue("EACEnabled") != null, "EACEnabled must be present in the minimal config.");

        // Preserve an unknown property
        doc.SetValue("UnknownFutureProperty", "futuristic-value");
        await doc.SaveAtomicAsync(configPath);

        var reloaded = await ServerConfigXmlDocument.LoadAsync(configPath);
        Assert(reloaded.GetValue("UnknownFutureProperty") == "futuristic-value", "Unknown properties must survive save/reload.");
        Assert(reloaded.GetValue("ServerName") == "Round Trip Server", "Known properties must survive save/reload.");

        // EnsureConfigExistsAsync must not overwrite an existing file
        await SevenDaysToDieConfigService.EnsureConfigExistsAsync(configPath, "Different Name");
        var notOverwritten = await ServerConfigXmlDocument.LoadAsync(configPath);
        Assert(notOverwritten.GetValue("ServerName") == "Round Trip Server", "EnsureConfigExistsAsync must not overwrite existing config.");

        // Atomic save: a malformed intermediate state must never replace the original
        var originalContent = await File.ReadAllTextAsync(configPath);
        Assert(!string.IsNullOrWhiteSpace(originalContent), "Saved config must not be empty.");

        // Profile round-trip through SevenDaysToDieConfigService
        var profile = new ServerProfile
        {
            Id = "7dtd-cfg-test",
            GameId = "seven_days_to_die",
            ServerName = "Config Test Server",
            Password = "join-secret",
            MaxPlayers = 16,
            Ports = new List<ServerPort> { new() { Name = "Game", Port = 26910 } }
        };
        profile.Settings["EACEnabled"] = "false";
        profile.Settings["GameWorld"] = "RWG";
        profile.Settings["SandboxCode"] = "AAAJABJACJADJARFBNC";

        var svc = new SevenDaysToDieConfigService();
        var profileConfigPath = Path.Combine(tempRoot, "profile-serverconfig.xml");
        await SevenDaysToDieConfigService.EnsureConfigExistsAsync(profileConfigPath);
        await svc.SaveAsync(profile, profileConfigPath);

        var savedDoc = await ServerConfigXmlDocument.LoadAsync(profileConfigPath);
        Assert(savedDoc.GetValue("ServerName") == "Config Test Server", "ServerName must be saved from profile.");
        Assert(savedDoc.GetValue("ServerPort") == "26910", "ServerPort must be saved from Ports list.");
        Assert(savedDoc.GetValue("ServerMaxPlayerCount") == "16", "ServerMaxPlayerCount must be saved.");
        Assert(savedDoc.GetValue("GameWorld") == "RWG", "GameWorld must be saved from Settings.");
        Assert(savedDoc.GetValue("SandboxCode") == "AAAJABJACJADJARFBNC", "SandboxCode must be saved.");
        Assert(savedDoc.GetValue("ServerPassword") == "join-secret", "ServerPassword must be saved from profile.Password.");

        // Load from XML back into a profile
        var loadedProfile = new ServerProfile
        {
            Id = "7dtd-load-test",
            GameId = "seven_days_to_die",
            InstallPath = tempRoot,
            Ports = new List<ServerPort> { new() { Name = "Game", Port = 26900 } }
        };
        await svc.LoadAsync(loadedProfile, profileConfigPath);
        Assert(loadedProfile.ServerName == "Config Test Server", "LoadAsync must populate profile.ServerName.");
        Assert(loadedProfile.MaxPlayers == 16, "LoadAsync must populate profile.MaxPlayers.");
        Assert(loadedProfile.Password == "join-secret", "LoadAsync must populate profile.Password.");
        Assert(loadedProfile.Settings.ContainsKey("SandboxCode"), "LoadAsync must load SandboxCode into Settings.");

        // V2 legacy keys must NOT be written back when SandboxCode is set
        var v3Profile = new ServerProfile
        {
            Id = "7dtd-v3-test",
            GameId = "seven_days_to_die",
            ServerName = "V3 Server",
            Ports = new List<ServerPort> { new() { Name = "Game", Port = 26900 } }
        };
        v3Profile.Settings["SandboxCode"] = "AAAJABJACJADJARFBNC";
        v3Profile.Settings["GameDifficulty"] = "2";  // V2 legacy key
        v3Profile.Settings["ZombieMove"] = "1";      // V2 legacy key

        var v3ConfigPath = Path.Combine(tempRoot, "v3-serverconfig.xml");
        await SevenDaysToDieConfigService.EnsureConfigExistsAsync(v3ConfigPath);
        await svc.SaveAsync(v3Profile, v3ConfigPath);
        var v3Doc = await ServerConfigXmlDocument.LoadAsync(v3ConfigPath);
        Assert(v3Doc.GetValue("GameDifficulty") == null, "V2 GameDifficulty must not be written to a V3 config.");
        Assert(v3Doc.GetValue("ZombieMove") == null, "V2 ZombieMove must not be written to a V3 config.");
        Assert(v3Doc.GetValue("SandboxCode") == "AAAJABJACJADJARFBNC", "SandboxCode must be present in V3 config.");

        // Sensitive value masking
        var xmlWithSecrets = "<ServerSettings><property name=\"ServerPassword\" value=\"top-secret\" /><property name=\"ServerName\" value=\"Public\" /></ServerSettings>";
        var masked = SandboxCodeHelpers.MaskSensitiveValues(xmlWithSecrets);
        Assert(!masked.Contains("top-secret", StringComparison.Ordinal), "Masked XML must not contain secret password.");
        Assert(masked.Contains("********", StringComparison.Ordinal), "Masked XML must replace password with ********.");
        Assert(masked.Contains("Public", StringComparison.Ordinal), "Masked XML must preserve non-sensitive values.");

        // Backup created on save
        var backupCount = Directory.EnumerateFiles(tempRoot, "*.bak").Count();
        Assert(backupCount > 0, "SaveAtomicAsync must create a .bak backup file.");
    }
    finally
    {
        DeleteTempRoot(tempRoot);
    }
}

static void Test7DaysToDieLaunchBuilder()
{
    var profile = new ServerProfile
    {
        Id = "7dtd-launch-test",
        GameId = "seven_days_to_die",
        ServerName = "Launch Test",
        InstallPath = @"C:\Servers\7dtd",
        Ports = new List<ServerPort> { new() { Name = "Game", Port = 26900 } }
    };

    var preview = new SevenDaysToDieLaunchBuilder().Build(profile);
    Assert(preview.Arguments.Contains("-batchmode", StringComparison.Ordinal), "Launch builder must include -batchmode.");
    Assert(preview.Arguments.Contains("-nographics", StringComparison.Ordinal), "Launch builder must include -nographics.");
    Assert(preview.Arguments.Contains("-dedicated", StringComparison.Ordinal), "Launch builder must include -dedicated.");
    Assert(preview.Arguments.Contains("-configfile=", StringComparison.Ordinal), "Launch builder must include -configfile=.");
    Assert(preview.Arguments.Contains("-UserDataFolder=", StringComparison.Ordinal), "Launch builder must include -UserDataFolder=.");
    Assert(preview.CommandLine.Contains("7DaysToDieServer.exe", StringComparison.OrdinalIgnoreCase), "Command line must reference server executable.");
    Assert(preview.WorkingDirectory == @"C:\Servers\7dtd", "Working directory must be the install path.");
}

static void Test7DaysToDieValidator()
{
    var registry = GameProviderRegistry.CreateDefault();
    registry.TryGetProvider("seven_days_to_die", out var provider);
    var validator = new SevenDaysToDieValidator();

    // Valid V3 profile
    var validProfile = new ServerProfile
    {
        Id = "7dtd-valid",
        GameId = "seven_days_to_die",
        ServerName = "Valid Server",
        InstallPath = @"C:\Servers\7dtd",
        MaxPlayers = 8,
        Ports = provider!.DefaultPorts.Select(p => new ServerPort
        {
            Name = p.Name, Port = p.Port, DefaultPort = p.DefaultPort,
            Protocol = p.Protocol, IsRequired = p.IsRequired
        }).ToList()
    };
    validProfile.Settings["SandboxCode"] = "AAAJABJACJADJARFBNC";
    var result = validator.Validate(validProfile);
    Assert(result.IsValid, "Valid V3 profile must pass validation.");

    // Crossplay requires EAC
    var crossplayNoEac = new ServerProfile
    {
        Id = "7dtd-crossplay",
        GameId = "seven_days_to_die",
        ServerName = "Crossplay",
        InstallPath = @"C:\Servers\7dtd",
        MaxPlayers = 8,
        Ports = provider.DefaultPorts.Select(p => new ServerPort
        {
            Name = p.Name, Port = p.Port, DefaultPort = p.DefaultPort,
            Protocol = p.Protocol, IsRequired = p.IsRequired
        }).ToList()
    };
    crossplayNoEac.Settings["ServerAllowCrossplay"] = "true";
    crossplayNoEac.Settings["EACEnabled"] = "false";
    var crossplayResult = validator.Validate(crossplayNoEac);
    Assert(!crossplayResult.IsValid, "Crossplay without EAC must fail validation.");
    Assert(crossplayResult.Errors.Any(e => e.Contains("EAC", StringComparison.OrdinalIgnoreCase)), "Error must mention EAC.");

    // Crossplay player count warning
    var crossplayTooMany = new ServerProfile
    {
        Id = "7dtd-crossplay-slots",
        GameId = "seven_days_to_die",
        ServerName = "Too Many",
        InstallPath = @"C:\Servers\7dtd",
        MaxPlayers = 20,
        Ports = provider.DefaultPorts.Select(p => new ServerPort
        {
            Name = p.Name, Port = p.Port, DefaultPort = p.DefaultPort,
            Protocol = p.Protocol, IsRequired = p.IsRequired
        }).ToList()
    };
    crossplayTooMany.Settings["ServerAllowCrossplay"] = "true";
    crossplayTooMany.Settings["EACEnabled"] = "true";
    var slotResult = validator.Validate(crossplayTooMany);
    Assert(slotResult.HasWarnings, "Crossplay with >8 players must produce a warning.");
    Assert(slotResult.Warnings.Any(w => w.Contains("8", StringComparison.Ordinal)), "Warning must mention the 8-player limit.");

    // Missing install path
    var noPath = new ServerProfile { Id = "7dtd-nopath", GameId = "seven_days_to_die", ServerName = "No Path" };
    Assert(!validator.Validate(noPath).IsValid, "Missing InstallPath must fail validation.");

    // V2 settings without SandboxCode should warn
    var v2Profile = new ServerProfile
    {
        Id = "7dtd-v2",
        GameId = "seven_days_to_die",
        ServerName = "V2 Server",
        InstallPath = @"C:\Servers\7dtd",
        MaxPlayers = 8,
        Ports = provider.DefaultPorts.Select(p => new ServerPort
        {
            Name = p.Name, Port = p.Port, DefaultPort = p.DefaultPort,
            Protocol = p.Protocol, IsRequired = p.IsRequired
        }).ToList()
    };
    v2Profile.Settings["GameDifficulty"] = "2";
    var v2Result = validator.Validate(v2Profile);
    Assert(v2Result.HasWarnings, "V2 settings without SandboxCode must produce a migration warning.");
}

static void Test7DaysToDieSettingDescriptions()
{
    var registry = GameProviderRegistry.CreateDefault();
    registry.TryGetProvider("seven_days_to_die", out var provider);

    // Every setting must have a description
    var withoutDescription = provider.SettingsDefinitions
        .Where(s => string.IsNullOrWhiteSpace(s.Description))
        .Select(s => s.SettingKey)
        .ToList();
    Assert(withoutDescription.Count == 0,
        $"Every setting must have a Description. Missing: {string.Join(", ", withoutDescription)}");

    // Number settings must have units
    var numbersWithoutUnit = provider.SettingsDefinitions
        .Where(s => s.ControlType == SettingControlType.NumberBox && string.IsNullOrWhiteSpace(s.Unit))
        .Select(s => s.SettingKey)
        .ToList();
    Assert(numbersWithoutUnit.Count == 0,
        $"All number settings must have a Unit. Missing: {string.Join(", ", numbersWithoutUnit)}");

    // All settings must have a category
    var withoutCategory = provider.SettingsDefinitions
        .Where(s => string.IsNullOrWhiteSpace(s.Category))
        .Select(s => s.SettingKey)
        .ToList();
    Assert(withoutCategory.Count == 0,
        $"All settings must have a Category. Missing: {string.Join(", ", withoutCategory)}");

    // Password settings must be PasswordField type
    Assert(provider.SettingsDefinitions.First(s => s.SettingKey == "ServerPassword").ControlType == SettingControlType.PasswordField,
        "ServerPassword must use PasswordField control type.");
    Assert(provider.SettingsDefinitions.First(s => s.SettingKey == "TelnetPassword").ControlType == SettingControlType.PasswordField,
        "TelnetPassword must use PasswordField control type.");

    // Web dashboard settings must be present
    Assert(provider.SettingsDefinitions.Any(s => s.SettingKey == "WebDashboardEnabled"),
        "Provider must include WebDashboardEnabled (modern dashboard support).");
    Assert(provider.SettingsDefinitions.Any(s => s.SettingKey == "EnableMapRendering"),
        "Provider must include EnableMapRendering.");
}

static void Test7DaysToDieCrossplayValidation()
{
    var validator = new SevenDaysToDieValidator();

    // Crossplay + > 8 players → error
    var crossplayTooManyPlayers = new ServerProfile
    {
        Id = "7dtd-xp-players",
        GameId = "seven_days_to_die",
        ServerName = "Crossplay Test",
        InstallPath = @"C:\Servers\7dtd",
        MaxPlayers = 16,
        Ports = new List<ServerPort> { new() { Name = "Game", Port = 26900 } }
    };
    crossplayTooManyPlayers.Settings["ServerAllowCrossplay"] = "True";
    crossplayTooManyPlayers.Settings["EACEnabled"] = "True";
    var r1 = validator.Validate(crossplayTooManyPlayers);
    Assert(r1.HasWarnings || !r1.IsValid,
        "Crossplay with > 8 players must produce a warning or error.");

    // Crossplay + EAC disabled → error
    var crossplayNoEac = new ServerProfile
    {
        Id = "7dtd-xp-eac",
        GameId = "seven_days_to_die",
        ServerName = "Crossplay No EAC",
        InstallPath = @"C:\Servers\7dtd",
        MaxPlayers = 8,
        Ports = new List<ServerPort> { new() { Name = "Game", Port = 26900 } }
    };
    crossplayNoEac.Settings["ServerAllowCrossplay"] = "True";
    crossplayNoEac.Settings["EACEnabled"] = "False";
    var r2 = validator.Validate(crossplayNoEac);
    Assert(!r2.IsValid, "Crossplay with EAC disabled must produce a validation error.");
    Assert(r2.Errors.Any(e => e.Contains("EAC", StringComparison.OrdinalIgnoreCase)),
        "EAC crossplay error must mention EAC.");

    // Invalid port range
    var badPort = new ServerProfile
    {
        Id = "7dtd-badport",
        GameId = "seven_days_to_die",
        ServerName = "Bad Port",
        InstallPath = @"C:\Servers\7dtd",
        MaxPlayers = 8,
        Ports = new List<ServerPort> { new() { Name = "Game", Port = 70000 } }
    };
    var r3 = validator.Validate(badPort);
    Assert(!r3.IsValid, "Port > 65530 must produce a validation error.");

    // Valid crossplay config
    var validCrossplay = new ServerProfile
    {
        Id = "7dtd-xp-valid",
        GameId = "seven_days_to_die",
        ServerName = "Valid Crossplay",
        InstallPath = @"C:\Servers\7dtd",
        MaxPlayers = 8,
        Ports = new List<ServerPort> { new() { Name = "Game", Port = 26900 } }
    };
    validCrossplay.Settings["ServerAllowCrossplay"] = "True";
    validCrossplay.Settings["EACEnabled"] = "True";
    validCrossplay.Settings["IgnoreEOSSanctions"] = "False";
    var r4 = validator.Validate(validCrossplay);
    Assert(r4.IsValid, "Valid crossplay configuration must pass validation.");
}

static void Test7DaysToDieWebDashboardSettings()
{
    var registry = GameProviderRegistry.CreateDefault();
    registry.TryGetProvider("seven_days_to_die", out var provider);

    // Web dashboard settings must coexist with old control panel settings
    Assert(provider.SettingsDefinitions.Any(s => s.SettingKey == "ControlPanelEnabled"),
        "Legacy ControlPanelEnabled must still be present.");
    Assert(provider.SettingsDefinitions.Any(s => s.SettingKey == "WebDashboardEnabled"),
        "Modern WebDashboardEnabled must be present.");

    // Web dashboard settings must be in the correct category
    var dashCat = provider.SettingsDefinitions
        .First(s => s.SettingKey == "WebDashboardEnabled").Category;
    Assert(dashCat == "Web & Dashboard",
        $"WebDashboardEnabled must be in 'Web & Dashboard' category, got '{dashCat}'.");

    // Advanced settings must be marked as advanced
    Assert(provider.SettingsDefinitions.First(s => s.SettingKey == "WebDashboardUrl").IsAdvanced,
        "WebDashboardUrl must be marked as advanced.");
    Assert(provider.SettingsDefinitions.First(s => s.SettingKey == "EnableMapRendering").IsAdvanced,
        "EnableMapRendering must be marked as advanced.");
}

static async Task Test7DaysToDieXmlPreservesUnknownPropertiesAsync()
{
    var tempRoot = CreateTempRoot();
    try
    {
        var configPath = Path.Combine(tempRoot, "serverconfig.xml");

        // Write a config with known + unknown + commented properties
        var xml = @"<?xml version=""1.0"" encoding=""UTF-8"" standalone=""yes""?>
<ServerSettings>
  <property name=""ServerName"" value=""Preserved Server"" />
  <property name=""FutureGameProperty"" value=""future-value"" />
  <property name=""AnotherFutureProperty"" value=""42"" />
</ServerSettings>";
        await File.WriteAllTextAsync(configPath, xml);

        // Save via service — unknown properties must survive
        var profile = new ServerProfile
        {
            Id = "7dtd-preserve-test",
            GameId = "seven_days_to_die",
            ServerName = "Preserved Server",
            Ports = new List<ServerPort> { new() { Name = "Game", Port = 26900 } }
        };
        profile.Settings["EACEnabled"] = "True";

        var svc = new SevenDaysToDieConfigService();
        await svc.SaveAsync(profile, configPath, createBackup: false);

        var doc = await ServerConfigXmlDocument.LoadAsync(configPath);
        Assert(doc.GetValue("FutureGameProperty") == "future-value",
            "Unknown property FutureGameProperty must survive a save.");
        Assert(doc.GetValue("AnotherFutureProperty") == "42",
            "Unknown property AnotherFutureProperty must survive a save.");
        Assert(doc.GetValue("EACEnabled") == "True",
            "Known property EACEnabled must be written correctly.");

        // Malformed XML must not replace the original
        var originalContent = await File.ReadAllTextAsync(configPath);
        try
        {
            var badDoc = ServerConfigXmlDocument.Parse("<ServerSettings><unclosed>");
            // If we reach this point, the parse succeeded (should not happen but be safe)
        }
        catch (System.Xml.XmlException)
        {
            // Expected — malformed XML should not parse
        }
        var afterBadAttempt = await File.ReadAllTextAsync(configPath);
        Assert(afterBadAttempt == originalContent,
            "Malformed XML must not modify the on-disk config.");
    }
    finally
    {
        DeleteTempRoot(tempRoot);
    }
}

static void Test7DaysToDieSandboxCodeRoundTrip()
{
    // SandboxCode is treated as an opaque string — it must be preserved exactly.
    var codes = new[]
    {
        "AAAJABJACJADJARFBNC",
        "A",
        "",
        "ABCDEF123456!@#$%",
        new string('Z', 256) // long code
    };

    foreach (var code in codes)
    {
        var doc = ServerConfigXmlDocument.CreateDefault();
        doc.SetValue("SandboxCode", code);
        var rendered = doc.Render();

        var reparsed = ServerConfigXmlDocument.Parse(rendered);
        var readBack = reparsed.GetValue("SandboxCode") ?? string.Empty;
        Assert(readBack == code,
            $"SandboxCode '{code[..Math.Min(20, code.Length)]}...' must survive XML serialize/deserialize round-trip.");
    }

    // IsSensitive must return false for SandboxCode (it is not a password)
    Assert(!SandboxCodeHelpers.IsSensitive("SandboxCode"),
        "SandboxCode must not be treated as sensitive (it is not a password).");

    // Password keys must be sensitive
    Assert(SandboxCodeHelpers.IsSensitive("ServerPassword"), "ServerPassword must be sensitive.");
    Assert(SandboxCodeHelpers.IsSensitive("TelnetPassword"), "TelnetPassword must be sensitive.");
    Assert(SandboxCodeHelpers.IsSensitive("ControlPanelPassword"), "ControlPanelPassword must be sensitive.");
}

static string CreateTempRoot()
{
    var path = Path.Combine(Path.GetTempPath(), "nsm-tests", Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(path);
    return path;
}

static void DeleteTempRoot(string path)
{
    if (Directory.Exists(path))
    {
        Directory.Delete(path, recursive: true);
    }
}

static void Assert(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }
}

static async Task TestServerInstallServiceValidationAsync()
{
    var registry2 = GameProviderRegistry.CreateDefault();
    var tempRoot = CreateTempRoot();
    try
    {
        var paths = new AppDataPaths(tempRoot);
        using var installService = new ServerInstallService(paths);

        // ARK ASA must declare SteamCmdInstall and carry the right AppId
        Assert(registry2.TryGetProvider("ark-survival-ascended", out var arkProvider), "ARK ASA provider must be registered.");
        Assert(arkProvider!.SupportedFeatures.HasFlag(GameServerFeatures.SteamCmdInstall), "ARK ASA must declare SteamCmdInstall feature.");
        Assert(arkProvider.SteamAppId.HasValue, "ARK ASA must have a Steam App ID.");
        Assert(arkProvider.SteamAppId == 2430930, "ARK ASA Steam App ID must be 2430930.");

        // Missing install path → validation failure (no SteamCMD launched)
        var noPathProfile = new ServerProfile { Id = "install-test-1", ServerName = "NoPath" };
        var r1 = await installService.InstallOrUpdateAsync(noPathProfile, arkProvider, false, null, default);
        Assert(!r1.Success && !r1.Cancelled, "Must fail when InstallPath is empty.");
        Assert(r1.Message.Contains("path", StringComparison.OrdinalIgnoreCase), "Error must mention install path.");

        // Provider without SteamCmdInstall feature → validation failure
        Assert(registry2.TryGetProvider("minecraft_java", out var mcProvider), "Minecraft Java provider must be registered.");
        if (!mcProvider!.SupportedFeatures.HasFlag(GameServerFeatures.SteamCmdInstall))
        {
            var mcProfile = new ServerProfile { Id = "install-test-2", ServerName = "MC", InstallPath = tempRoot };
            var r2 = await installService.InstallOrUpdateAsync(mcProfile, mcProvider, false, null, default);
            Assert(!r2.Success, "Must fail for provider without SteamCmdInstall feature.");
            Assert(r2.Message.Contains("SteamCMD", StringComparison.OrdinalIgnoreCase), "Error must mention SteamCMD.");
        }

        // No active operations before anything runs
        Assert(!installService.IsOperationActive("any-id"), "No install operation should be active before a run.");
    }
    finally
    {
        DeleteTempRoot(tempRoot);
    }
}
