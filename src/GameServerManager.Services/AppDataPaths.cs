using System.Text.RegularExpressions;

namespace GameServerManager.Services;

public class AppDataPaths
{
    public AppDataPaths(string? rootDirectory = null)
    {
        InstallDirectory = Path.GetFullPath(AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        IsPortable = rootDirectory is null && DetectPortableMode(InstallDirectory);

        // Legacy paths resolve against InstallDirectory (not CurrentDirectory).
        LegacyDataDirectory = Path.Combine(InstallDirectory, "Data");
        LegacyServersDirectory = Path.Combine(InstallDirectory, "Servers");

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

        // Server files and tools always live beside the executable, regardless of portable mode.
        ServersDirectory = Path.Combine(InstallDirectory, "Servers");
        BackupsRoot = Path.Combine(InstallDirectory, "Backups");
        ToolsRoot = Path.Combine(InstallDirectory, "Tools");
        SteamCmdRoot = Path.Combine(InstallDirectory, "Tools", "SteamCMD");
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

    /// <summary>Root for game server installations: <InstallDirectory>\Servers</summary>
    public string ServersDirectory { get; }
    /// <summary>Root for server save backups: <InstallDirectory>\Backups</summary>
    public string BackupsRoot { get; }
    /// <summary>Root for shared tools: <InstallDirectory>\Tools</summary>
    public string ToolsRoot { get; }
    /// <summary>SteamCMD installation directory: <InstallDirectory>\Tools\SteamCMD</summary>
    public string SteamCmdRoot { get; }

    // ── Per-server path helpers ──────────────────────────────────────────────

    /// <summary>Dedicated root folder for one server: Servers\&lt;gameSlug&gt;\&lt;serverSlug&gt;</summary>
    public string GetServerRoot(string gameId, string serverSlug)
        => Path.GetFullPath(Path.Combine(ServersDirectory, ToSlug(gameId), serverSlug));

    /// <summary>Where game files are installed: GetServerRoot\ServerFiles</summary>
    public string GetServerInstallDirectory(string gameId, string serverSlug)
        => Path.GetFullPath(Path.Combine(GetServerRoot(gameId, serverSlug), "ServerFiles"));

    /// <summary>Where backups are stored: Backups\&lt;gameSlug&gt;\&lt;serverSlug&gt;</summary>
    public string GetServerBackupDirectory(string gameId, string serverSlug)
        => Path.GetFullPath(Path.Combine(BackupsRoot, ToSlug(gameId), serverSlug));

    // ── Path resolution ──────────────────────────────────────────────────────

    /// <summary>
    /// Resolves a stored path against the application root.
    /// Absolute paths are returned as-is; relative paths are rooted at InstallDirectory.
    /// </summary>
    public string ResolveStoredPath(string storedPath)
    {
        if (string.IsNullOrWhiteSpace(storedPath))
            return storedPath;
        return Path.IsPathRooted(storedPath)
            ? Path.GetFullPath(storedPath)
            : Path.GetFullPath(Path.Combine(InstallDirectory, storedPath));
    }

    // ── Slug generation ──────────────────────────────────────────────────────

    private static readonly char[] _invalidNameChars = Path.GetInvalidFileNameChars();

    private static readonly HashSet<string> _reservedNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "CON","PRN","AUX","NUL",
        "COM1","COM2","COM3","COM4","COM5","COM6","COM7","COM8","COM9",
        "LPT1","LPT2","LPT3","LPT4","LPT5","LPT6","LPT7","LPT8","LPT9"
    };

    /// <summary>
    /// Converts a display name into a safe, lowercase, hyphen-separated folder name.
    /// Example: "ARK TheIsland" → "ark-theisland", "New Server" → "new-server"
    /// </summary>
    public static string ToSlug(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return "server";

        var s = name.Trim().ToLowerInvariant();

        // Replace invalid filename chars, backslash, forward-slash → hyphen
        s = new string(s.Select(c => _invalidNameChars.Contains(c) || c == '\\' || c == '/' ? '-' : c).ToArray());

        // Replace spaces and underscores with hyphens
        s = s.Replace(' ', '-').Replace('_', '-');

        // Collapse multiple hyphens
        s = Regex.Replace(s, "-{2,}", "-");

        // Trim hyphens and trailing periods (Windows restriction)
        s = s.Trim('-').TrimEnd('.');

        if (string.IsNullOrWhiteSpace(s))
            return "server";

        if (_reservedNames.Contains(s))
            s += "-srv";

        return s;
    }

    // ── Directory initialisation ─────────────────────────────────────────────

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

        // Beside-the-executable directories
        Directory.CreateDirectory(ServersDirectory);
        Directory.CreateDirectory(BackupsRoot);
        Directory.CreateDirectory(ToolsRoot);
        Directory.CreateDirectory(SteamCmdRoot);
    }

    // ── Portable detection ───────────────────────────────────────────────────

    private static bool DetectPortableMode(string installDirectory)
    {
        if (File.Exists(Path.Combine(installDirectory, "portable.flag")))
            return true;

        var directoryName = new DirectoryInfo(installDirectory).Name;
        return directoryName.Contains("portable", StringComparison.OrdinalIgnoreCase);
    }

    // ── Legacy migration ─────────────────────────────────────────────────────

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
                File.Copy(file, destination);
        }

        foreach (var directory in Directory.EnumerateDirectories(sourceDirectory))
            CopyDirectory(directory, Path.Combine(destinationDirectory, Path.GetFileName(directory)));
    }
}
