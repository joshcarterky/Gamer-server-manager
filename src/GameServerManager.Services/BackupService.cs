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

        // Millisecond precision keeps filenames unique even when a backup and a
        // restore-time safety backup happen within the same second.
        var backupPath = Path.Combine(
            backupDirectory,
            $"{Sanitize(profile.ProfileName)}_{DateTime.UtcNow:yyyyMMdd_HHmmss_fff}.zip");

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

    /// <summary>The folder where this server's backup zips are written and listed.</summary>
    public string GetServerBackupsFolder(ServerProfile profile) => GetBackupTargetDirectory(profile);

    /// <summary>Lists this server's backup archives, newest first.</summary>
    public IReadOnlyList<BackupFileInfo> ListBackups(ServerProfile profile)
    {
        var directory = GetBackupTargetDirectory(profile);
        if (!Directory.Exists(directory))
        {
            return Array.Empty<BackupFileInfo>();
        }

        return Directory.EnumerateFiles(directory, "*.zip")
            .Select(path => new FileInfo(path))
            .OrderByDescending(file => file.LastWriteTimeUtc)
            .Select(file => new BackupFileInfo(file.FullName, file.Name, file.Length, file.LastWriteTime))
            .ToList();
    }

    /// <summary>
    /// Restores a backup archive over the server's source folder. Before touching
    /// anything it takes a fresh safety backup of the current state, and it rejects
    /// archive entries that would escape the restore root (ZIP-slip protection).
    /// Never reports success until the extraction completes.
    /// </summary>
    public async Task<BackupRestoreResult> RestoreServerAsync(
        ServerProfile profile,
        string backupZipPath,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(backupZipPath) || !File.Exists(backupZipPath))
        {
            return new BackupRestoreResult(false, "The selected backup file no longer exists.", null);
        }

        var sourceDirectory = GetBackupSourceDirectory(profile);
        var restoreRoot = Path.GetDirectoryName(Path.GetFullPath(sourceDirectory));
        if (string.IsNullOrWhiteSpace(restoreRoot))
        {
            return new BackupRestoreResult(false, "Could not determine where to restore the backup.", null);
        }

        // Safety backup of the current state first, so a bad restore is recoverable.
        string? safetyBackup = null;
        if (Directory.Exists(sourceDirectory))
        {
            try
            {
                safetyBackup = await BackupServerAsync(profile);
            }
            catch (Exception ex)
            {
                return new BackupRestoreResult(false, $"Aborted — could not create a safety backup first: {ex.Message}", null);
            }
        }

        try
        {
            await Task.Run(() =>
            {
                using var archive = ZipFile.OpenRead(backupZipPath);
                var rootFull = Path.GetFullPath(restoreRoot);
                if (!rootFull.EndsWith(Path.DirectorySeparatorChar))
                {
                    rootFull += Path.DirectorySeparatorChar;
                }

                foreach (var entry in archive.Entries)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var destinationPath = Path.GetFullPath(Path.Combine(restoreRoot, entry.FullName));

                    // ZIP-slip guard: entry must resolve inside the restore root.
                    if (!destinationPath.StartsWith(rootFull, StringComparison.OrdinalIgnoreCase))
                    {
                        throw new IOException($"Refused an archive entry that escapes the restore folder: {entry.FullName}");
                    }

                    // Directory entry
                    if (string.IsNullOrEmpty(entry.Name))
                    {
                        Directory.CreateDirectory(destinationPath);
                        continue;
                    }

                    Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
                    entry.ExtractToFile(destinationPath, overwrite: true);
                }
            }, cancellationToken);
        }
        catch (Exception ex)
        {
            var preserved = safetyBackup != null
                ? $" Your previous state is preserved in {Path.GetFileName(safetyBackup)}."
                : string.Empty;
            return new BackupRestoreResult(false, $"Restore failed: {ex.Message}.{preserved}", safetyBackup);
        }

        return new BackupRestoreResult(true, $"Restored from {Path.GetFileName(backupZipPath)}.", safetyBackup);
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

        return Path.Combine(_paths.BackupsRoot, AppDataPaths.ToSlug(profile.GameId), Sanitize(profile.ProfileName));
    }

    private static string Sanitize(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var cleaned = new string(value.Select(ch => invalid.Contains(ch) ? '_' : ch).ToArray());
        return string.IsNullOrWhiteSpace(cleaned) ? "edit" : cleaned;
    }
}

/// <summary>One backup archive on disk.</summary>
public sealed record BackupFileInfo(string FullPath, string FileName, long SizeBytes, DateTime CreatedAt);

/// <summary>Outcome of a restore operation, including the pre-restore safety backup.</summary>
public sealed record BackupRestoreResult(bool Success, string Message, string? SafetyBackupPath);
