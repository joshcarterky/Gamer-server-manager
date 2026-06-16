using GameServerManager.Core.Models;
using GameServerManager.GameProviders;

namespace GameServerManager.Services;

public sealed class ServerImportDetector
{
    private readonly GameProviderRegistry _providers;

    public ServerImportDetector(GameProviderRegistry providers)
    {
        _providers = providers;
    }

    public ServerProfile Detect(string folderPath)
    {
        if (string.IsNullOrWhiteSpace(folderPath) || !Directory.Exists(folderPath))
        {
            throw new DirectoryNotFoundException("Choose an existing server folder.");
        }

        var detection = DetectGame(folderPath);
        var provider = _providers.GetProvider(detection.GameId);
        var serverName = new DirectoryInfo(folderPath).Name;
        var executablePath = detection.ExecutablePath ?? FindExecutable(folderPath, provider.ExecutableRelativePath) ?? string.Empty;
        var configFolder = FindExistingFolder(folderPath, provider.ConfigFolder) ?? folderPath;
        var saveFolder = detection.SaveFolder ?? FindExistingFolder(folderPath, provider.SavesFolder) ?? folderPath;
        var backupFolder = FindBackupFolder(folderPath) ?? Path.Combine("Data", "Backups", provider.GameId, SafeName(serverName));

        return new ServerProfile
        {
            GameId = provider.GameId,
            ProfileName = serverName,
            ServerName = serverName,
            InstallPath = folderPath,
            ExecutablePath = executablePath,
            MapName = detection.MapName,
            Status = ServerStatus.Stopped,
            MaxPlayers = detection.MaxPlayers,
            Ports = ClonePorts(provider.DefaultPorts),
            Notes = $"Imported from {folderPath}",
            Settings =
            {
                ["ipAddress"] = "0.0.0.0",
                ["description"] = $"Imported {provider.GameName} server.",
                ["tags"] = "imported",
                ["serverPath"] = folderPath,
                ["configDirectory"] = configFolder,
                ["saveDirectory"] = saveFolder,
                ["backupDirectory"] = backupFolder,
                ["detection"] = detection.Reason
            },
            CreatedAt = DateTime.UtcNow,
            ModifiedAt = DateTime.UtcNow
        };
    }

    private DetectionResult DetectGame(string folderPath)
    {
        if (TryFindFile(folderPath, "ArkAscendedServer.exe", out var arkAscendedExe) ||
            Directory.Exists(Path.Combine(folderPath, "ShooterGame", "Binaries", "Win64")) &&
            File.Exists(Path.Combine(folderPath, "ShooterGame", "Binaries", "Win64", "ArkAscendedServer.exe")))
        {
            return new DetectionResult(
                "ark_survival_ascended",
                arkAscendedExe ?? Path.Combine(folderPath, "ShooterGame", "Binaries", "Win64", "ArkAscendedServer.exe"),
                FindFolder(folderPath, "SavedArks"),
                "TheIsland_WP",
                70,
                "Found ArkAscendedServer.exe");
        }

        var hasServerJar = File.Exists(Path.Combine(folderPath, "server.jar"));
        var hasMinecraftJar = TryFindFile(folderPath, "*.jar", out var minecraftJar);
        if (hasServerJar || hasMinecraftJar && File.Exists(Path.Combine(folderPath, "server.properties")))
        {
            return new DetectionResult(
                "minecraft_java",
                minecraftJar ?? Path.Combine(folderPath, "server.jar"),
                Path.Combine(folderPath, "world"),
                "world",
                20,
                "Found Minecraft Java server jar/properties");
        }

        if (TryFindFile(folderPath, "7DaysToDieServer.exe", out var sevenDaysExe) ||
            File.Exists(Path.Combine(folderPath, "serverconfig.xml")))
        {
            return new DetectionResult(
                "seven_days_to_die",
                sevenDaysExe ?? Path.Combine(folderPath, "7DaysToDieServer.exe"),
                FindFolder(folderPath, "Saves"),
                string.Empty,
                8,
                "Found 7 Days to Die server files");
        }

        if (TryFindFile(folderPath, "PalServer.exe", out var palworldExe) ||
            Directory.Exists(Path.Combine(folderPath, "Pal", "Saved")))
        {
            return new DetectionResult(
                "palworld",
                palworldExe ?? Path.Combine(folderPath, "PalServer.exe"),
                Path.Combine(folderPath, "Pal", "Saved", "SaveGames"),
                string.Empty,
                32,
                "Found Palworld server files");
        }

        if (TryFindFile(folderPath, "RustDedicated.exe", out var rustExe) ||
            Directory.Exists(Path.Combine(folderPath, "server")) && Directory.Exists(Path.Combine(folderPath, "cfg")))
        {
            return new DetectionResult(
                "rust",
                rustExe ?? Path.Combine(folderPath, "RustDedicated.exe"),
                Path.Combine(folderPath, "server"),
                string.Empty,
                100,
                "Found Rust server files");
        }

        return new DetectionResult(
            "generic_server",
            TryFindFile(folderPath, "*.exe", out var executable) ? executable : string.Empty,
            FindFolder(folderPath, "saves") ?? folderPath,
            string.Empty,
            10,
            "No known game layout detected; imported as Generic Server");
    }

    private static List<ServerPort> ClonePorts(IReadOnlyList<ServerPort> ports)
    {
        return ports.Select(port => new ServerPort
        {
            Name = port.Name,
            Port = port.Port,
            Protocol = port.Protocol,
            Description = port.Description,
            IsRequired = port.IsRequired,
            DefaultPort = port.DefaultPort
        }).ToList();
    }

    private static string? FindExecutable(string folderPath, string relativePath)
    {
        var candidate = Path.Combine(folderPath, relativePath.Replace('/', Path.DirectorySeparatorChar));
        return File.Exists(candidate) ? candidate : null;
    }

    private static string? FindExistingFolder(string folderPath, string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath) || relativePath == ".")
        {
            return folderPath;
        }

        var candidate = Path.Combine(folderPath, relativePath.Replace('/', Path.DirectorySeparatorChar));
        return Directory.Exists(candidate) ? candidate : null;
    }

    private static string? FindFolder(string folderPath, string folderName)
    {
        return Directory.EnumerateDirectories(folderPath, folderName, SearchOption.AllDirectories).FirstOrDefault();
    }

    private static string? FindBackupFolder(string folderPath)
    {
        return Directory.EnumerateDirectories(folderPath, "*backup*", SearchOption.AllDirectories).FirstOrDefault();
    }

    private static bool TryFindFile(string folderPath, string pattern, out string? filePath)
    {
        filePath = Directory.EnumerateFiles(folderPath, pattern, SearchOption.AllDirectories).FirstOrDefault();
        return filePath != null;
    }

    private static string SafeName(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        return new string(value.Select(ch => invalid.Contains(ch) ? '_' : ch).ToArray());
    }

    private sealed record DetectionResult(
        string GameId,
        string? ExecutablePath,
        string? SaveFolder,
        string MapName,
        int MaxPlayers,
        string Reason);
}
