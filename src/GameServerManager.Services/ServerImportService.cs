namespace GameServerManager.Services;

public sealed class ServerImportService
{
    private readonly AppDataPaths _paths;
    private readonly string _logPath;

    public ServerImportService(AppDataPaths? paths = null)
    {
        _paths = paths ?? new AppDataPaths();
        _paths.EnsureCreated();
        _logPath = Path.Combine(_paths.LogsDirectory, "import.log");
    }

    public string ManagedServersDirectory => _paths.ServersDirectory;

    public string CreateDestinationPath(string serverName)
    {
        var safeName = SafeFolderName(serverName);
        var basePath = Path.Combine(ManagedServersDirectory, safeName);
        if (!Directory.Exists(basePath))
        {
            return basePath;
        }

        var importedPath = Path.Combine(ManagedServersDirectory, $"{safeName}-Imported");
        if (!Directory.Exists(importedPath))
        {
            return importedPath;
        }

        for (var i = 2; i < 1000; i++)
        {
            var candidate = Path.Combine(ManagedServersDirectory, $"{safeName}-{i}");
            if (!Directory.Exists(candidate))
            {
                return candidate;
            }
        }

        throw new IOException($"Could not find an available destination folder for {serverName}.");
    }

    public Task<long> CalculateFolderSizeAsync(string sourceDirectory, CancellationToken cancellationToken = default)
    {
        return Task.Run(() =>
        {
            EnsureReadableSource(sourceDirectory);
            long total = 0;
            foreach (var file in Directory.EnumerateFiles(sourceDirectory, "*", SearchOption.AllDirectories))
            {
                cancellationToken.ThrowIfCancellationRequested();
                total += new FileInfo(file).Length;
            }

            return total;
        }, cancellationToken);
    }

    public async Task CopyIntoManagedFolderAsync(
        string sourceDirectory,
        string destinationDirectory,
        IProgress<ServerImportProgress> progress,
        CancellationToken cancellationToken = default)
    {
        EnsureReadableSource(sourceDirectory);
        if (Directory.Exists(destinationDirectory))
        {
            throw new IOException($"Destination folder already exists: {destinationDirectory}");
        }

        await LogAsync($"Import started. Source={sourceDirectory}; Destination={destinationDirectory}", cancellationToken);
        var totalBytes = await CalculateFolderSizeAsync(sourceDirectory, cancellationToken);
        EnsureEnoughDiskSpace(destinationDirectory, totalBytes);

        long copiedBytes = 0;
        var startedAt = DateTime.UtcNow;

        try
        {
            Directory.CreateDirectory(destinationDirectory);
            var files = Directory.EnumerateFiles(sourceDirectory, "*", SearchOption.AllDirectories).ToList();
            for (var index = 0; index < files.Count; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var sourceFile = files[index];
                var relativePath = Path.GetRelativePath(sourceDirectory, sourceFile);
                var destinationFile = Path.Combine(destinationDirectory, relativePath);
                Directory.CreateDirectory(Path.GetDirectoryName(destinationFile)!);

                await CopyFileAsync(sourceFile, destinationFile, totalBytes, copiedBytes, startedAt, progress, cancellationToken);
                copiedBytes += new FileInfo(sourceFile).Length;
                progress.Report(CreateProgress("Copying server files...", relativePath, copiedBytes, totalBytes, startedAt));
            }

            progress.Report(new ServerImportProgress("Import complete.", string.Empty, totalBytes, totalBytes, 100, 0));
            await LogAsync($"Import completed. Files={files.Count}; TotalBytes={totalBytes}", cancellationToken);
        }
        catch (OperationCanceledException)
        {
            await LogAsync("Import canceled by user.", CancellationToken.None);
            TryDeletePartialDestination(destinationDirectory);
            throw;
        }
        catch (Exception ex)
        {
            await LogAsync($"Import failed. {ex}", CancellationToken.None);
            TryDeletePartialDestination(destinationDirectory);
            throw;
        }
    }

    public Task LogAsync(string message, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_logPath)!);
        return File.AppendAllTextAsync(_logPath, $"[{DateTimeOffset.Now:o}] {message}{Environment.NewLine}", cancellationToken);
    }

    private static async Task CopyFileAsync(
        string sourceFile,
        string destinationFile,
        long totalBytes,
        long copiedBeforeFile,
        DateTime startedAt,
        IProgress<ServerImportProgress> progress,
        CancellationToken cancellationToken)
    {
        const int bufferSize = 1024 * 128;
        await using var source = new FileStream(sourceFile, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize, useAsync: true);
        await using var destination = new FileStream(destinationFile, FileMode.CreateNew, FileAccess.Write, FileShare.None, bufferSize, useAsync: true);
        var relativeName = Path.GetFileName(sourceFile);
        var buffer = new byte[bufferSize];
        long copiedThisFile = 0;

        while (true)
        {
            var read = await source.ReadAsync(buffer, cancellationToken);
            if (read == 0)
            {
                break;
            }

            await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
            copiedThisFile += read;
            progress.Report(CreateProgress("Copying server files...", relativeName, copiedBeforeFile + copiedThisFile, totalBytes, startedAt));
        }
    }

    private static ServerImportProgress CreateProgress(
        string status,
        string currentFile,
        long copiedBytes,
        long totalBytes,
        DateTime startedAt)
    {
        var elapsedSeconds = Math.Max((DateTime.UtcNow - startedAt).TotalSeconds, 0.1);
        var percent = totalBytes <= 0 ? 100 : (int)Math.Clamp(copiedBytes * 100d / totalBytes, 0, 100);
        return new ServerImportProgress(status, currentFile, copiedBytes, totalBytes, percent, copiedBytes / elapsedSeconds);
    }

    private static void EnsureReadableSource(string sourceDirectory)
    {
        if (string.IsNullOrWhiteSpace(sourceDirectory) || !Directory.Exists(sourceDirectory))
        {
            throw new DirectoryNotFoundException("Choose an existing server folder.");
        }

        if (!Directory.EnumerateFileSystemEntries(sourceDirectory).Any())
        {
            throw new IOException("The selected server folder is empty.");
        }
    }

    private static void EnsureEnoughDiskSpace(string destinationDirectory, long requiredBytes)
    {
        var root = Path.GetPathRoot(Path.GetFullPath(destinationDirectory));
        if (string.IsNullOrWhiteSpace(root))
        {
            return;
        }

        var drive = new DriveInfo(root);
        if (drive.AvailableFreeSpace < requiredBytes)
        {
            throw new IOException($"Not enough disk space. Required: {FormatBytes(requiredBytes)}. Available: {FormatBytes(drive.AvailableFreeSpace)}.");
        }
    }

    private static void TryDeletePartialDestination(string destinationDirectory)
    {
        try
        {
            if (Directory.Exists(destinationDirectory))
            {
                Directory.Delete(destinationDirectory, recursive: true);
            }
        }
        catch
        {
            // Best-effort cleanup; the import log contains the failed destination.
        }
    }

    private static string SafeFolderName(string value)
    {
        var fallback = string.IsNullOrWhiteSpace(value) ? "ImportedServer" : value.Trim();
        var invalid = Path.GetInvalidFileNameChars();
        var safe = new string(fallback.Select(ch => invalid.Contains(ch) ? '_' : ch).ToArray());
        return string.IsNullOrWhiteSpace(safe) ? "ImportedServer" : safe;
    }

    private static string FormatBytes(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB", "TB"];
        double value = bytes;
        var unit = 0;
        while (value >= 1024 && unit < units.Length - 1)
        {
            value /= 1024;
            unit++;
        }

        return $"{value:0.##} {units[unit]}";
    }
}

public sealed record ServerImportProgress(
    string Status,
    string CurrentFile,
    long CopiedBytes,
    long TotalBytes,
    int Percent,
    double BytesPerSecond);
