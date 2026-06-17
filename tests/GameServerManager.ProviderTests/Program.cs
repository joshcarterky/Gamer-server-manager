using GameServerManager.Core.Models;
using GameServerManager.GameProviders;
using GameServerManager.Services;
using GameServerManager.Services.ArkSurvivalAscended;
using GameServerManager.Services.Configuration;
using GameServerManager.Services.Diagnostics;
using GameServerManager.Services.Repositories;
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
TestUpdaterVersionComparison();
TestSettingsUpdateSeparation();
TestArkSettingsRedesignContracts();
TestDiagnosticsMaskSecrets();
TestPortableModeDetection();
TestReleaseVersionStamp();

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
    Assert(xaml.Contains("Server Overview", StringComparison.Ordinal), "ARK settings UI should include a designed overview page.");
    Assert(xaml.Contains("BooleanValue", StringComparison.Ordinal), "ARK settings UI should bind boolean settings to a checkbox editor.");
    Assert(!xaml.Contains("ItemsSource=\"{Binding Tabs}\" SelectedItem=\"{Binding SelectedTab}\"", StringComparison.Ordinal), "ARK settings navigation should not use the old flat tab list.");
    Assert(!xaml.Contains("Launch Command Preview", StringComparison.Ordinal), "Launch command preview must not be rendered as a global panel.");
    Assert(!xaml.Contains("SteamCMD Install / Update", StringComparison.Ordinal), "SteamCMD command must not be rendered as a global panel.");
    Assert(!xaml.Contains("Text=\"Current vs Pending Config\"", StringComparison.Ordinal), "Old global configuration diff panel must not be rendered.");
    Assert(xaml.Contains("Visibility=\"{Binding IsHealthValidationTab", StringComparison.Ordinal), "Health and Validation should own the diff page.");
    Assert(xaml.Contains("Visibility=\"{Binding IsInstallUpdateTab", StringComparison.Ordinal), "Install / Update should own SteamCMD install content.");
    Assert(xaml.Contains("Visibility=\"{Binding IsStartupTab", StringComparison.Ordinal), "Startup should own launch command content.");
    Assert(xaml.Contains("Visibility=\"{Binding IsRawEditorTab", StringComparison.Ordinal), "Raw INI Editor should own raw file editors.");
    Assert(xaml.Contains("Header=\"Technical Command Preview\"", StringComparison.Ordinal), "SteamCMD command preview should be collapsed by default.");
    Assert(xaml.Contains("Header=\"View Generated Command\"", StringComparison.Ordinal), "Launch command preview should be collapsed by default.");
    Assert(xaml.Contains("RawGameUserSettingsEditorText", StringComparison.Ordinal), "Raw editor should bind through masked editor text.");
    Assert(xaml.Contains("GroupName=\"ArkSettingsMode\"", StringComparison.Ordinal), "Basic and Advanced mode should use one segmented radio group.");
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
