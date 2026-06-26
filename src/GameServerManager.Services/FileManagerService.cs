using System.IO;
using System.Text;

namespace GameServerManager.Services;

public enum FileEntryType { File, Directory }

public record FileEntry(
    string Name,
    string FullPath,
    FileEntryType Type,
    long SizeBytes,
    DateTime Modified,
    bool IsReadOnly
);

public record FileManagerResult(bool Success, string? Error = null);

public class FileManagerService
{
    private static readonly HashSet<string> EditableExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".ini", ".json", ".yaml", ".yml", ".xml", ".txt", ".cfg",
        ".properties", ".log", ".config", ".toml", ".env", ".bat",
        ".sh", ".cmd", ".conf", ".htaccess", ".md", ".csv"
    };

    private static readonly HashSet<char> InvalidNameChars =
        new(Path.GetInvalidFileNameChars());

    // ── Path safety ────────────────────────────────────────────────────────────

    public bool IsPathSafe(string fullPath, string rootPath)
    {
        if (string.IsNullOrWhiteSpace(fullPath) || string.IsNullOrWhiteSpace(rootPath))
            return false;

        try
        {
            var normalRoot = Path.GetFullPath(rootPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))
                             + Path.DirectorySeparatorChar;
            var normalFull = Path.GetFullPath(fullPath);

            // Must be inside root
            if (!normalFull.StartsWith(normalRoot, StringComparison.OrdinalIgnoreCase))
                return false;

            // Must not be a reparse point (symlink/junction)
            if (File.Exists(normalFull) || Directory.Exists(normalFull))
            {
                var attrs = File.GetAttributes(normalFull);
                if ((attrs & FileAttributes.ReparsePoint) != 0)
                    return false;
            }

            return true;
        }
        catch
        {
            return false;
        }
    }

    public string? ResolveSafe(string relativePath, string rootPath)
    {
        try
        {
            var combined = Path.IsPathRooted(relativePath)
                ? relativePath
                : Path.Combine(rootPath, relativePath);
            var full = Path.GetFullPath(combined);
            return IsPathSafe(full, rootPath) ? full : null;
        }
        catch
        {
            return null;
        }
    }

    public bool IsValidFileName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return false;
        if (name.Any(c => InvalidNameChars.Contains(c)))
            return false;
        if (name == "." || name == "..")
            return false;
        if (name.Length > 255)
            return false;
        return true;
    }

    public bool IsEditable(string fileName) =>
        EditableExtensions.Contains(Path.GetExtension(fileName));

    // ── Directory listing ──────────────────────────────────────────────────────

    public async Task<(IReadOnlyList<FileEntry> Entries, string? Error)> ListDirectoryAsync(
        string path, string rootPath, CancellationToken ct = default)
    {
        if (!IsPathSafe(path, rootPath))
            return ([], "Access denied: path is outside the permitted directory.");

        try
        {
            var entries = await Task.Run(() =>
            {
                var result = new List<FileEntry>();
                var di = new DirectoryInfo(path);

                if (!di.Exists)
                    return result;

                foreach (var d in di.GetDirectories())
                {
                    ct.ThrowIfCancellationRequested();
                    if ((d.Attributes & FileAttributes.ReparsePoint) != 0) continue;
                    result.Add(new FileEntry(d.Name, d.FullName, FileEntryType.Directory,
                        0, d.LastWriteTime, (d.Attributes & FileAttributes.ReadOnly) != 0));
                }

                foreach (var f in di.GetFiles())
                {
                    ct.ThrowIfCancellationRequested();
                    if ((f.Attributes & FileAttributes.ReparsePoint) != 0) continue;
                    result.Add(new FileEntry(f.Name, f.FullName, FileEntryType.File,
                        f.Length, f.LastWriteTime, (f.Attributes & FileAttributes.ReadOnly) != 0));
                }

                return result;
            }, ct);

            return (entries, null);
        }
        catch (OperationCanceledException)
        {
            return ([], "Cancelled.");
        }
        catch (Exception ex)
        {
            return ([], ex.Message);
        }
    }

    public async Task<(IReadOnlyList<FileEntry> Entries, string? Error)> GetSubDirectoriesAsync(
        string path, string rootPath, CancellationToken ct = default)
    {
        if (!IsPathSafe(path, rootPath))
            return ([], "Access denied.");

        try
        {
            var entries = await Task.Run(() =>
            {
                var di = new DirectoryInfo(path);
                if (!di.Exists) return (IReadOnlyList<FileEntry>)[];

                return (IReadOnlyList<FileEntry>)di.GetDirectories()
                    .Where(d => (d.Attributes & FileAttributes.ReparsePoint) == 0)
                    .Select(d => new FileEntry(d.Name, d.FullName, FileEntryType.Directory,
                        0, d.LastWriteTime, (d.Attributes & FileAttributes.ReadOnly) != 0))
                    .ToList();
            }, ct);

            return (entries, null);
        }
        catch (Exception ex)
        {
            return ([], ex.Message);
        }
    }

    // ── Create ─────────────────────────────────────────────────────────────────

    public async Task<FileManagerResult> CreateFolderAsync(string parentPath, string folderName, string rootPath)
    {
        if (!IsPathSafe(parentPath, rootPath))
            return new FileManagerResult(false, "Access denied.");

        if (!IsValidFileName(folderName))
            return new FileManagerResult(false, $"Invalid folder name: '{folderName}'.");

        var newPath = Path.Combine(parentPath, folderName);
        if (!IsPathSafe(newPath, rootPath))
            return new FileManagerResult(false, "Access denied.");

        if (Directory.Exists(newPath))
            return new FileManagerResult(false, $"A folder named '{folderName}' already exists.");

        try
        {
            await Task.Run(() => Directory.CreateDirectory(newPath));
            return new FileManagerResult(true);
        }
        catch (Exception ex)
        {
            return new FileManagerResult(false, ex.Message);
        }
    }

    public async Task<FileManagerResult> CreateTextFileAsync(string parentPath, string fileName, string rootPath)
    {
        if (!IsPathSafe(parentPath, rootPath))
            return new FileManagerResult(false, "Access denied.");

        if (!IsValidFileName(fileName))
            return new FileManagerResult(false, $"Invalid file name: '{fileName}'.");

        var newPath = Path.Combine(parentPath, fileName);
        if (!IsPathSafe(newPath, rootPath))
            return new FileManagerResult(false, "Access denied.");

        if (File.Exists(newPath))
            return new FileManagerResult(false, $"A file named '{fileName}' already exists.");

        try
        {
            await Task.Run(() => File.WriteAllText(newPath, string.Empty, Encoding.UTF8));
            return new FileManagerResult(true);
        }
        catch (Exception ex)
        {
            return new FileManagerResult(false, ex.Message);
        }
    }

    // ── Rename ─────────────────────────────────────────────────────────────────

    public async Task<FileManagerResult> RenameAsync(string fullPath, string newName, string rootPath)
    {
        if (!IsPathSafe(fullPath, rootPath))
            return new FileManagerResult(false, "Access denied.");

        if (!IsValidFileName(newName))
            return new FileManagerResult(false, $"Invalid name: '{newName}'.");

        var parentDir = Path.GetDirectoryName(fullPath);
        if (parentDir is null)
            return new FileManagerResult(false, "Cannot determine parent directory.");

        var newPath = Path.Combine(parentDir, newName);
        if (!IsPathSafe(newPath, rootPath))
            return new FileManagerResult(false, "Access denied.");

        if (File.Exists(newPath) || Directory.Exists(newPath))
            return new FileManagerResult(false, $"An item named '{newName}' already exists.");

        try
        {
            await Task.Run(() =>
            {
                if (Directory.Exists(fullPath))
                    Directory.Move(fullPath, newPath);
                else
                    File.Move(fullPath, newPath);
            });
            return new FileManagerResult(true);
        }
        catch (Exception ex)
        {
            return new FileManagerResult(false, ex.Message);
        }
    }

    // ── Delete ─────────────────────────────────────────────────────────────────

    public async Task<FileManagerResult> DeleteAsync(IEnumerable<string> paths, string rootPath)
    {
        var pathList = paths.ToList();
        var denied = pathList.Where(p => !IsPathSafe(p, rootPath)).ToList();
        if (denied.Any())
            return new FileManagerResult(false, $"Access denied for: {string.Join(", ", denied.Select(Path.GetFileName))}");

        try
        {
            await Task.Run(() =>
            {
                foreach (var path in pathList)
                {
                    if (Directory.Exists(path))
                        Directory.Delete(path, recursive: true);
                    else if (File.Exists(path))
                        File.Delete(path);
                }
            });
            return new FileManagerResult(true);
        }
        catch (Exception ex)
        {
            return new FileManagerResult(false, ex.Message);
        }
    }

    // ── Copy / Move ────────────────────────────────────────────────────────────

    public async Task<FileManagerResult> CopyAsync(
        IEnumerable<string> sourcePaths, string destinationDirectory, string rootPath,
        IProgress<(int done, int total, string current)>? progress = null)
    {
        if (!IsPathSafe(destinationDirectory, rootPath))
            return new FileManagerResult(false, "Destination is outside the permitted directory.");

        var sources = sourcePaths.ToList();
        var denied = sources.Where(p => !IsPathSafe(p, rootPath)).ToList();
        if (denied.Any())
            return new FileManagerResult(false, $"Access denied for: {string.Join(", ", denied.Select(Path.GetFileName))}");

        try
        {
            await Task.Run(() =>
            {
                for (int i = 0; i < sources.Count; i++)
                {
                    var src = sources[i];
                    var name = Path.GetFileName(src);
                    progress?.Report((i, sources.Count, name));

                    if (Directory.Exists(src))
                        CopyDirectoryRecursive(src, Path.Combine(destinationDirectory, name));
                    else if (File.Exists(src))
                        File.Copy(src, GetUniqueDestPath(destinationDirectory, name), overwrite: false);
                }
                progress?.Report((sources.Count, sources.Count, string.Empty));
            });
            return new FileManagerResult(true);
        }
        catch (Exception ex)
        {
            return new FileManagerResult(false, ex.Message);
        }
    }

    public async Task<FileManagerResult> MoveAsync(
        IEnumerable<string> sourcePaths, string destinationDirectory, string rootPath,
        IProgress<(int done, int total, string current)>? progress = null)
    {
        if (!IsPathSafe(destinationDirectory, rootPath))
            return new FileManagerResult(false, "Destination is outside the permitted directory.");

        var sources = sourcePaths.ToList();
        var denied = sources.Where(p => !IsPathSafe(p, rootPath)).ToList();
        if (denied.Any())
            return new FileManagerResult(false, $"Access denied for: {string.Join(", ", denied.Select(Path.GetFileName))}");

        try
        {
            await Task.Run(() =>
            {
                for (int i = 0; i < sources.Count; i++)
                {
                    var src = sources[i];
                    var name = Path.GetFileName(src);
                    progress?.Report((i, sources.Count, name));

                    var dest = GetUniqueDestPath(destinationDirectory, name);
                    if (Directory.Exists(src))
                        Directory.Move(src, dest);
                    else if (File.Exists(src))
                        File.Move(src, dest);
                }
                progress?.Report((sources.Count, sources.Count, string.Empty));
            });
            return new FileManagerResult(true);
        }
        catch (Exception ex)
        {
            return new FileManagerResult(false, ex.Message);
        }
    }

    // ── File editor ────────────────────────────────────────────────────────────

    public async Task<(string? Content, string? Error)> ReadTextFileAsync(string fullPath, string rootPath)
    {
        if (!IsPathSafe(fullPath, rootPath))
            return (null, "Access denied.");

        if (!File.Exists(fullPath))
            return (null, "File not found.");

        var fi = new FileInfo(fullPath);
        if (fi.Length > 10 * 1024 * 1024) // 10 MB cap for inline editor
            return (null, "File is too large to open in the editor (> 10 MB).");

        try
        {
            var content = await File.ReadAllTextAsync(fullPath);
            return (content, null);
        }
        catch (Exception ex)
        {
            return (null, ex.Message);
        }
    }

    public async Task<FileManagerResult> WriteTextFileAsync(string fullPath, string content, string rootPath)
    {
        if (!IsPathSafe(fullPath, rootPath))
            return new FileManagerResult(false, "Access denied.");

        try
        {
            var dir = Path.GetDirectoryName(fullPath);
            if (dir is not null)
                Directory.CreateDirectory(dir);

            await File.WriteAllTextAsync(fullPath, content, Encoding.UTF8);
            return new FileManagerResult(true);
        }
        catch (Exception ex)
        {
            return new FileManagerResult(false, ex.Message);
        }
    }

    // ── Upload / Download helpers ──────────────────────────────────────────────

    public async Task<FileManagerResult> UploadFileAsync(
        string localSourcePath, string destinationDirectory, string rootPath,
        IProgress<double>? progress = null)
    {
        if (!IsPathSafe(destinationDirectory, rootPath))
            return new FileManagerResult(false, "Destination is outside the permitted directory.");

        if (!File.Exists(localSourcePath))
            return new FileManagerResult(false, "Source file not found.");

        var fileName = Path.GetFileName(localSourcePath);
        var destPath = GetUniqueDestPath(destinationDirectory, fileName);
        if (!IsPathSafe(destPath, rootPath))
            return new FileManagerResult(false, "Access denied.");

        try
        {
            await CopyWithProgressAsync(localSourcePath, destPath, progress);
            return new FileManagerResult(true);
        }
        catch (Exception ex)
        {
            return new FileManagerResult(false, ex.Message);
        }
    }

    public async Task<FileManagerResult> DownloadFileAsync(
        string fullSourcePath, string localDestinationPath, string rootPath,
        IProgress<double>? progress = null)
    {
        if (!IsPathSafe(fullSourcePath, rootPath))
            return new FileManagerResult(false, "Access denied.");

        if (!File.Exists(fullSourcePath))
            return new FileManagerResult(false, "Source file not found.");

        try
        {
            await CopyWithProgressAsync(fullSourcePath, localDestinationPath, progress);
            return new FileManagerResult(true);
        }
        catch (Exception ex)
        {
            return new FileManagerResult(false, ex.Message);
        }
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    private static string GetUniqueDestPath(string directory, string fileName)
    {
        var dest = Path.Combine(directory, fileName);
        if (!File.Exists(dest) && !Directory.Exists(dest))
            return dest;

        var baseName = Path.GetFileNameWithoutExtension(fileName);
        var ext = Path.GetExtension(fileName);
        for (int i = 2; i < 1000; i++)
        {
            dest = Path.Combine(directory, $"{baseName} ({i}){ext}");
            if (!File.Exists(dest) && !Directory.Exists(dest))
                return dest;
        }
        return dest;
    }

    private static void CopyDirectoryRecursive(string source, string dest)
    {
        Directory.CreateDirectory(dest);
        foreach (var f in Directory.GetFiles(source))
            File.Copy(f, Path.Combine(dest, Path.GetFileName(f)), overwrite: false);
        foreach (var d in Directory.GetDirectories(source))
            CopyDirectoryRecursive(d, Path.Combine(dest, Path.GetFileName(d)));
    }

    private static async Task CopyWithProgressAsync(string source, string dest, IProgress<double>? progress)
    {
        const int bufferSize = 81920;
        var fi = new FileInfo(source);
        long total = fi.Length;
        long copied = 0;

        using var src = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, bufferSize, true);
        using var dst = new FileStream(dest, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize, true);
        var buffer = new byte[bufferSize];
        int read;

        while ((read = await src.ReadAsync(buffer)) > 0)
        {
            await dst.WriteAsync(buffer.AsMemory(0, read));
            copied += read;
            progress?.Report(total > 0 ? (double)copied / total : 0);
        }
    }

    // ── Formatting helpers ─────────────────────────────────────────────────────

    public static string FormatSize(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
        if (bytes < 1024L * 1024 * 1024) return $"{bytes / (1024.0 * 1024):F1} MB";
        return $"{bytes / (1024.0 * 1024 * 1024):F2} GB";
    }
}
