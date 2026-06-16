namespace GameServerManager.Services;

public class AppDataPaths
{
    public AppDataPaths(string? rootDirectory = null)
    {
        InstallDirectory = AppContext.BaseDirectory;
        IsPortable = rootDirectory is null && DetectPortableMode(InstallDirectory);
        LegacyDataDirectory = Path.Combine(Environment.CurrentDirectory, "Data");
        LegacyServersDirectory = Path.Combine(Environment.CurrentDirectory, "Servers");
        RootDirectory = rootDirectory ?? (IsPortable
            ? Path.Combine(InstallDirectory, "Data")
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Nexus Server Manager"));
        ServersJsonPath = Path.Combine(RootDirectory, "servers.json");
        ProfilesDirectory = Path.Combine(RootDirectory, "Profiles");
        LogsDirectory = Path.Combine(RootDirectory, "Logs");
        ServerLogsDirectory = Path.Combine(LogsDirectory, "Servers");
        SettingsDirectory = Path.Combine(RootDirectory, "Settings");
        BackupsDirectory = Path.Combine(RootDirectory, "Backups");
        DiagnosticsDirectory = Path.Combine(RootDirectory, "Diagnostics");
        UpdateBackupsDirectory = Path.Combine(RootDirectory, "UpdateBackups");
        UpdateDownloadsDirectory = Path.Combine(RootDirectory, "UpdateDownloads");
        ServersDirectory = IsPortable
            ? Path.Combine(InstallDirectory, "Servers")
            : Directory.Exists(LegacyServersDirectory)
                ? LegacyServersDirectory
                : Path.Combine(RootDirectory, "Servers");
    }

    public string InstallDirectory { get; }
    public bool IsPortable { get; }
    public string LegacyDataDirectory { get; }
    public string LegacyServersDirectory { get; }
    public string RootDirectory { get; }
    public string ServersJsonPath { get; }
    public string ProfilesDirectory { get; }
    public string LogsDirectory { get; }
    public string ServerLogsDirectory { get; }
    public string SettingsDirectory { get; }
    public string BackupsDirectory { get; }
    public string DiagnosticsDirectory { get; }
    public string UpdateBackupsDirectory { get; }
    public string UpdateDownloadsDirectory { get; }
    public string ServersDirectory { get; }

    public void EnsureCreated()
    {
        MigrateLegacyDataIfNeeded();

        Directory.CreateDirectory(RootDirectory);
        Directory.CreateDirectory(ProfilesDirectory);
        Directory.CreateDirectory(LogsDirectory);
        Directory.CreateDirectory(ServerLogsDirectory);
        Directory.CreateDirectory(SettingsDirectory);
        Directory.CreateDirectory(BackupsDirectory);
        Directory.CreateDirectory(DiagnosticsDirectory);
        Directory.CreateDirectory(UpdateBackupsDirectory);
        Directory.CreateDirectory(UpdateDownloadsDirectory);
        Directory.CreateDirectory(ServersDirectory);

        foreach (var serverFolder in new[]
                 {
                     "ARK_Survival_Ascended",
                     "7_Days_To_Die",
                     "Palworld",
                     "Minecraft",
                     "Minecraft_Bedrock",
                     "Valheim",
                     "Rust"
                 })
        {
            Directory.CreateDirectory(Path.Combine(ServersDirectory, serverFolder));
        }
    }

    private static bool DetectPortableMode(string installDirectory)
    {
        if (File.Exists(Path.Combine(installDirectory, "portable.flag")))
        {
            return true;
        }

        var directoryName = new DirectoryInfo(installDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)).Name;
        return directoryName.Contains("portable", StringComparison.OrdinalIgnoreCase);
    }

    private void MigrateLegacyDataIfNeeded()
    {
        if (IsPortable
            || !Directory.Exists(LegacyDataDirectory)
            || Directory.Exists(RootDirectory)
            || string.Equals(
                Path.GetFullPath(LegacyDataDirectory).TrimEnd(Path.DirectorySeparatorChar),
                Path.GetFullPath(RootDirectory).TrimEnd(Path.DirectorySeparatorChar),
                StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        CopyDirectory(LegacyDataDirectory, RootDirectory);
    }

    private static void CopyDirectory(string sourceDirectory, string destinationDirectory)
    {
        Directory.CreateDirectory(destinationDirectory);

        foreach (var file in Directory.EnumerateFiles(sourceDirectory))
        {
            var destination = Path.Combine(destinationDirectory, Path.GetFileName(file));
            if (!File.Exists(destination))
            {
                File.Copy(file, destination);
            }
        }

        foreach (var directory in Directory.EnumerateDirectories(sourceDirectory))
        {
            CopyDirectory(directory, Path.Combine(destinationDirectory, Path.GetFileName(directory)));
        }
    }
}
