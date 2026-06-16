using GameServerManager.Core.Models;
using GameServerManager.GameProviders;
using GameServerManager.Services;
using GameServerManager.Services.ArkSurvivalAscended;
using GameServerManager.Services.Configuration;
using GameServerManager.Services.Diagnostics;
using GameServerManager.Services.Repositories;
using GameServerManager.Services.Updates;

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
        profile.Settings["MemoryMb"] = "1024";
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
await TestArkSurvivalAscendedAsync();
TestUpdaterVersionComparison();
TestDiagnosticsMaskSecrets();
TestPortableModeDetection();

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
        File.WriteAllText(arkProfile.Paths.GameUserSettingsPath, "; keep me\r\n[ServerSettings]\r\nUnknownKey=Keep\r\nServerPVE=False\r\n");
        File.WriteAllText(arkProfile.Paths.GameIniPath, "[/script/shootergame.shootergamemode]\r\nConfigAddNPCSpawnEntriesContainer=(Foo=1)\r\nConfigAddNPCSpawnEntriesContainer=(Bar=2)\r\n");
        File.WriteAllText(Path.Combine(arkProfile.Paths.SavesPath, "TheIsland.ark"), "save");

        var document = await IniDocument.LoadAsync(arkProfile.Paths.GameIniPath);
        Assert(document.GetValues(ArkAsaSettingRegistry.GameModeSection, "ConfigAddNPCSpawnEntriesContainer").Count == 2, "INI parser should preserve repeated array settings.");
        document.SetValue(ArkAsaSettingRegistry.GameModeSection, "DinoCountMultiplier", "1.5");
        Assert(document.Render().Contains("ConfigAddNPCSpawnEntriesContainer=(Foo=1)", StringComparison.Ordinal), "INI writer should preserve unknown repeated lines.");

        arkProfile.GameUserSettings.ServerSettings["ServerPVE"] = "True";
        arkProfile.GameIni.RepeatedSettings["ConfigAddNPCSpawnEntriesContainer"] = new List<string> { "(Foo=1)", "(Bar=2)" };
        await new ArkAsaConfigService().SaveAsync(arkProfile);
        var savedGus = File.ReadAllText(arkProfile.Paths.GameUserSettingsPath);
        Assert(savedGus.Contains("; keep me", StringComparison.Ordinal), "Config save should preserve comments.");
        Assert(savedGus.Contains("UnknownKey=Keep", StringComparison.Ordinal), "Config save should preserve unknown settings.");
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
        var island = clusterManager.CreateClusterMapProfile(new ArkAsaClusterMapRequest(
            "Test Cluster",
            "cluster-shared",
            "C:\\Clusters\\shared",
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
            "C:\\Clusters\\shared",
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
