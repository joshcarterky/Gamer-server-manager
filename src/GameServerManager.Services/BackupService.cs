using System.IO.Compression;
using GameServerManager.Core.Models;

namespace GameServerManager.Services;

public class BackupService
{
    private readonly AppDataPaths _paths;

    public BackupService(AppDataPaths paths)
    {
        _paths = paths;
        _paths.EnsureCreated();
    }

    public async Task<string?> BackupFileBeforeEditAsync(string filePath, string reason)
    {
        if (!File.Exists(filePath))
        {
            return null;
        }

        var backupDirectory = Path.Combine(_paths.BackupsDirectory, "BeforeEdit", DateTime.UtcNow.ToString("yyyyMMdd"));
        Directory.CreateDirectory(backupDirectory);
        var backupName = $"{Path.GetFileName(filePath)}.{DateTime.UtcNow:HHmmss}.{Sanitize(reason)}.bak";
        var backupPath = Path.Combine(backupDirectory, backupName);

        await using var source = File.OpenRead(filePath);
        await using var target = File.Create(backupPath);
        await source.CopyToAsync(target);
        return backupPath;
    }

    public async Task<string> BackupServerAsync(ServerProfile profile)
    {
        var sourceDirectory = GetBackupSourceDirectory(profile);
        if (string.IsNullOrWhiteSpace(sourceDirectory) || !Directory.Exists(sourceDirectory))
        {
            throw new DirectoryNotFoundException($"Backup source folder was not found: {sourceDirectory}");
        }

        var backupDirectory = GetBackupTargetDirectory(profile);
        Directory.CreateDirectory(backupDirectory);

        var backupPath = Path.Combine(
            backupDirectory,
            $"{Sanitize(profile.ProfileName)}_{DateTime.UtcNow:yyyyMMdd_HHmmss}.zip");

        await Task.Run(() =>
        {
            if (File.Exists(backupPath))
            {
                File.Delete(backupPath);
            }

            ZipFile.CreateFromDirectory(sourceDirectory, backupPath, CompressionLevel.Optimal, includeBaseDirectory: true);
        });

        return backupPath;
    }

    private static string GetBackupSourceDirectory(ServerProfile profile)
    {
        if (profile.Settings.TryGetValue("saveDirectory", out var saveDirectory) && Directory.Exists(saveDirectory))
        {
            return saveDirectory;
        }

        return profile.InstallPath;
    }

    private string GetBackupTargetDirectory(ServerProfile profile)
    {
        if (profile.Settings.TryGetValue("backupDirectory", out var backupDirectory) &&
            !string.IsNullOrWhiteSpace(backupDirectory))
        {
            return backupDirectory;
        }

        return Path.Combine(_paths.BackupsDirectory, "Servers", Sanitize(profile.ProfileName));
    }

    private static string Sanitize(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var cleaned = new string(value.Select(ch => invalid.Contains(ch) ? '_' : ch).ToArray());
        return string.IsNullOrWhiteSpace(cleaned) ? "edit" : cleaned;
    }
}
