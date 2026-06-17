using System.Diagnostics;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text.Json;

namespace GameServerManager.Services.Updates;

public sealed class GitHubAssetDownloadService
{
    private readonly AppDataPaths _paths;
    private readonly UpdateLogger _logger;
    private readonly HttpClient _httpClient;

    public GitHubAssetDownloadService(AppDataPaths? paths = null, UpdateLogger? logger = null, HttpClient? httpClient = null)
    {
        _paths = paths ?? new AppDataPaths();
        _logger = logger ?? new UpdateLogger(_paths);
        _httpClient = httpClient ?? new HttpClient();
        if (!_httpClient.DefaultRequestHeaders.UserAgent.Any())
        {
            _httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("NexusServerManager", AppVersion.Current));
        }
    }

    public string DownloadsDirectory => _paths.UpdateDownloadsDirectory;

    public async Task<UpdateDownloadResult> DownloadBestWindowsAssetAsync(
        UpdateCheckResult update,
        Action<UpdateDownloadProgress> progress,
        CancellationToken cancellationToken = default)
    {
        var asset = PickBestWindowsAsset(update.Assets, _paths.IsPortable ? "Portable" : "Installer");
        if (asset is null)
        {
            var detail = $"Release={update.LatestVersion ?? "unknown"}; Assets=[{string.Join(", ", update.Assets.Select(a => a.Name))}]";
            await _logger.LogAsync("No compatible update asset found.", detail, cancellationToken);
            return new UpdateDownloadResult(false, null, UpdateErrorMessages.For("NoDownloadableAsset"), detail);
        }

        if (!Uri.TryCreate(asset.DownloadUrl, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
        {
            return new UpdateDownloadResult(false, null, "The selected update asset does not use a secure HTTPS download URL.", $"AssetUrl={asset.DownloadUrl}", asset);
        }

        try
        {
            var versionFolderName = SanitizeFileName((update.LatestVersion ?? "unknown").TrimStart('v'));
            var stagingDirectory = Path.Combine(_paths.UpdateDownloadsDirectory, versionFolderName);
            Directory.CreateDirectory(stagingDirectory);
            var fileName = SanitizeFileName(asset.Name);
            var destination = Path.Combine(stagingDirectory, fileName);
            var partialPath = $"{destination}.partial";
            var metadataPath = Path.Combine(stagingDirectory, "update-metadata.json");
            SafeDelete(partialPath);

            await _logger.LogAsync("Update download started.", $"Asset={asset.Name}; Url={asset.DownloadUrl}; Destination={destination}", cancellationToken);

            using var response = await _httpClient.GetAsync(asset.DownloadUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            await _logger.LogAsync("Update download HTTP response.", $"Status={(int)response.StatusCode} {response.ReasonPhrase}; ContentType={response.Content.Headers.ContentType}; Length={response.Content.Headers.ContentLength}", cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return new UpdateDownloadResult(false, null, $"GitHub returned {(int)response.StatusCode} {response.ReasonPhrase} while downloading the installer.", $"Asset={asset.Name}; Url={asset.DownloadUrl}", asset);
            }

            var totalBytes = response.Content.Headers.ContentLength ?? (asset.SizeBytes > 0 ? asset.SizeBytes : null);
            EnsureDiskSpace(stagingDirectory, totalBytes ?? asset.SizeBytes);
            await using var source = await response.Content.ReadAsStreamAsync(cancellationToken);
            await using var target = new FileStream(partialPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 1024 * 128, useAsync: true);

            var buffer = new byte[1024 * 128];
            long downloaded = 0;
            var stopwatch = Stopwatch.StartNew();
            while (true)
            {
                var read = await source.ReadAsync(buffer, cancellationToken);
                if (read == 0) break;

                await target.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                downloaded += read;
                var percent = totalBytes is > 0 ? (int)Math.Clamp(downloaded * 100d / totalBytes.Value, 0, 100) : 0;
                var speed = downloaded / Math.Max(stopwatch.Elapsed.TotalSeconds, 0.1);
                var remaining = totalBytes is > 0 && speed > 0
                    ? TimeSpan.FromSeconds(Math.Max(0, (totalBytes.Value - downloaded) / speed))
                    : (TimeSpan?)null;
                progress(new UpdateDownloadProgress(percent, downloaded, totalBytes, speed, remaining, fileName));
            }

            await target.FlushAsync(cancellationToken);
            target.Close();

            var verification = await VerifyDownloadedPackageAsync(partialPath, asset, update, cancellationToken);
            if (!verification.Success)
            {
                SafeDelete(partialPath);
                return verification;
            }

            SafeDelete(destination);
            File.Move(partialPath, destination);
            var actualSha = await ComputeSha256Async(destination, cancellationToken);
            var metadata = new PendingUpdateMetadata(
                update.CurrentVersion,
                update.LatestVersion ?? "unknown",
                update.LatestVersion ?? "unknown",
                update.ReleaseName,
                update.Channel,
                asset.Name,
                asset.DownloadUrl,
                GetPackageType(asset).ToString(),
                RuntimeInformation(),
                asset.SizeBytes > 0 ? asset.SizeBytes : totalBytes,
                new FileInfo(destination).Length,
                verification.Sha256,
                actualSha,
                destination,
                DateTimeOffset.Now,
                "Verified",
                "Pending",
                null,
                Environment.ProcessPath ?? string.Empty,
                update.CurrentVersion,
                null);
            await WriteMetadataAsync(metadataPath, metadata, cancellationToken);
            await _logger.LogAsync("Update download completed and verified.", $"Path={destination}; Sha256={actualSha}; Metadata={metadataPath}", cancellationToken);
            return new UpdateDownloadResult(true, destination, "Download complete and verified.", null, asset, actualSha, metadataPath);
        }
        catch (OperationCanceledException)
        {
            await _logger.LogAsync("Update download cancelled.", asset.Name, CancellationToken.None);
            throw;
        }
        catch (UnauthorizedAccessException ex)
        {
            await _logger.LogAsync("Download permission denied.", ex.ToString(), cancellationToken);
            return new UpdateDownloadResult(false, null, UpdateErrorMessages.For("PermissionDenied"), ex.ToString(), asset);
        }
        catch (IOException ex) when (ex.HResult == unchecked((int)0x80070070))
        {
            await _logger.LogAsync("Download failed because disk is full.", ex.ToString(), cancellationToken);
            return new UpdateDownloadResult(false, null, "There is not enough disk space to download the update.", ex.ToString(), asset);
        }
        catch (Exception ex)
        {
            await _logger.LogAsync("Download failed.", ex.ToString(), cancellationToken);
            return new UpdateDownloadResult(false, null, UpdateErrorMessages.For("DownloadFailed", ex.Message), ex.ToString(), asset);
        }
    }

    private async Task<UpdateDownloadResult> VerifyDownloadedPackageAsync(string filePath, UpdateAsset asset, UpdateCheckResult update, CancellationToken cancellationToken)
    {
        if (!File.Exists(filePath))
        {
            return new UpdateDownloadResult(false, filePath, UpdateErrorMessages.For("DownloadFailed", "Downloaded file is missing."), null, asset);
        }

        var info = new FileInfo(filePath);
        if (info.Length == 0)
        {
            return new UpdateDownloadResult(false, filePath, UpdateErrorMessages.For("DownloadFailed", "Downloaded file is empty."), null, asset);
        }

        if (asset.SizeBytes > 0 && info.Length != asset.SizeBytes)
        {
            return new UpdateDownloadResult(false, filePath, "The downloaded file size does not match the GitHub release metadata.", $"Expected={asset.SizeBytes}; Actual={info.Length}; Asset={asset.Name}", asset);
        }

        if (!await LooksLikePackageAsync(filePath, asset, cancellationToken))
        {
            return new UpdateDownloadResult(false, filePath, "The downloaded file does not look like a valid installer package.", $"Asset={asset.Name}; ContentType={asset.ContentType}; Path={filePath}", asset);
        }

        var checksum = await TryGetExpectedChecksumAsync(update, Path.GetFileName(filePath).Replace(".partial", string.Empty, StringComparison.OrdinalIgnoreCase), cancellationToken);
        if (!string.IsNullOrWhiteSpace(checksum))
        {
            var actual = await ComputeSha256Async(filePath, cancellationToken);
            if (!string.Equals(checksum, actual, StringComparison.OrdinalIgnoreCase))
            {
                await _logger.LogAsync("Checksum failed.", $"Expected={checksum}; Actual={actual}; Asset={asset.Name}", cancellationToken);
                return new UpdateDownloadResult(false, filePath, UpdateErrorMessages.For("ChecksumFailed"), $"Expected={checksum}; Actual={actual}", asset, checksum);
            }

            await _logger.LogAsync("Checksum verified.", $"Asset={asset.Name}; Sha256={actual}", cancellationToken);
        }

        return new UpdateDownloadResult(true, filePath, "Verification passed.", null, asset, checksum);
    }

    private async Task<string?> TryGetExpectedChecksumAsync(UpdateCheckResult update, string fileName, CancellationToken cancellationToken)
    {
        var checksumAsset = update.Assets.FirstOrDefault(a =>
            a.Name.EndsWith(".sha256", StringComparison.OrdinalIgnoreCase)
            || a.Name.Contains("Checksums", StringComparison.OrdinalIgnoreCase)
            || string.Equals(a.Name, "checksums.txt", StringComparison.OrdinalIgnoreCase));
        if (checksumAsset is null)
        {
            return null;
        }

        try
        {
            var checksums = await _httpClient.GetStringAsync(checksumAsset.DownloadUrl, cancellationToken);
            var line = checksums.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries)
                .FirstOrDefault(value => value.Contains(fileName, StringComparison.OrdinalIgnoreCase));
            return line?.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        }
        catch (Exception ex)
        {
            await _logger.LogAsync("Checksum metadata could not be read.", ex.ToString(), cancellationToken);
            return null;
        }
    }

    private static async Task<bool> LooksLikePackageAsync(string filePath, UpdateAsset asset, CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(filePath);
        var header = new byte[8];
        var read = await stream.ReadAsync(header, cancellationToken);
        if (read < 4)
        {
            return false;
        }

        var extension = Path.GetExtension(asset.Name);
        if (extension.Equals(".exe", StringComparison.OrdinalIgnoreCase))
        {
            return header[0] == 'M' && header[1] == 'Z';
        }

        if (extension.Equals(".zip", StringComparison.OrdinalIgnoreCase))
        {
            return header[0] == 'P' && header[1] == 'K';
        }

        if (extension.Equals(".msi", StringComparison.OrdinalIgnoreCase))
        {
            return header[0] == 0xD0 && header[1] == 0xCF && header[2] == 0x11 && header[3] == 0xE0;
        }

        return false;
    }

    public static UpdateAsset? PickBestWindowsAsset(IReadOnlyList<UpdateAsset> assets)
    {
        return PickBestWindowsAsset(assets, "Installer");
    }

    public static UpdateAsset? PickBestWindowsAsset(IReadOnlyList<UpdateAsset> assets, string installMode)
    {
        var candidates = assets
            .Where(IsCompatibleWindowsAsset)
            .ToArray();

        if (installMode.Equals("Portable", StringComparison.OrdinalIgnoreCase))
        {
            return candidates.FirstOrDefault(asset => GetPackageType(asset) == UpdatePackageType.PortableZip);
        }

        return candidates.FirstOrDefault(asset => GetPackageType(asset) == UpdatePackageType.Exe && asset.Name.Contains("Setup", StringComparison.OrdinalIgnoreCase))
            ?? candidates.FirstOrDefault(asset => GetPackageType(asset) == UpdatePackageType.Exe && asset.Name.Contains("Installer", StringComparison.OrdinalIgnoreCase))
            ?? candidates.FirstOrDefault(asset => GetPackageType(asset) == UpdatePackageType.Msi)
            ?? candidates.FirstOrDefault(asset => GetPackageType(asset) == UpdatePackageType.Exe);
    }

    public static bool IsCompatibleWindowsAsset(UpdateAsset asset)
    {
        var name = asset.Name;
        if (name.Contains("source", StringComparison.OrdinalIgnoreCase)
            || name.Contains("symbols", StringComparison.OrdinalIgnoreCase)
            || name.Contains("debug", StringComparison.OrdinalIgnoreCase)
            || name.Contains("Checksums", StringComparison.OrdinalIgnoreCase)
            || name.EndsWith(".sha256", StringComparison.OrdinalIgnoreCase)
            || name.EndsWith(".txt", StringComparison.OrdinalIgnoreCase)
            || name.Contains("linux", StringComparison.OrdinalIgnoreCase)
            || name.Contains("mac", StringComparison.OrdinalIgnoreCase)
            || name.Contains("osx", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (name.Contains("arm64", StringComparison.OrdinalIgnoreCase))
        {
            return System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture == System.Runtime.InteropServices.Architecture.Arm64;
        }

        if (name.Contains("x86", StringComparison.OrdinalIgnoreCase) && !name.Contains("x64", StringComparison.OrdinalIgnoreCase))
        {
            return System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture == System.Runtime.InteropServices.Architecture.X86;
        }

        return GetPackageType(asset) != UpdatePackageType.Unknown;
    }

    public static UpdatePackageType GetPackageType(UpdateAsset asset)
    {
        if (asset.Name.EndsWith(".msi", StringComparison.OrdinalIgnoreCase)) return UpdatePackageType.Msi;
        if (asset.Name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) return UpdatePackageType.Exe;
        if (asset.Name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) && asset.Name.Contains("Portable", StringComparison.OrdinalIgnoreCase)) return UpdatePackageType.PortableZip;
        return UpdatePackageType.Unknown;
    }

    public void CleanOldDownloads(int daysToKeep = 30)
    {
        if (!Directory.Exists(_paths.UpdateDownloadsDirectory))
        {
            return;
        }

        var cutoff = DateTime.UtcNow.AddDays(-daysToKeep);
        foreach (var file in Directory.EnumerateFiles(_paths.UpdateDownloadsDirectory))
        {
            try
            {
                if (File.GetLastWriteTimeUtc(file) < cutoff)
                {
                    File.Delete(file);
                }
            }
            catch
            {
                // ignore — file may be locked or in use
            }
        }
    }

    private static string SanitizeFileName(string fileName)
    {
        fileName = Path.GetFileName(fileName);
        foreach (var invalid in Path.GetInvalidFileNameChars())
        {
            fileName = fileName.Replace(invalid, '-');
        }

        return fileName;
    }

    private static async Task<string> ComputeSha256Async(string filePath, CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(filePath);
        return Convert.ToHexString(await SHA256.HashDataAsync(stream, cancellationToken));
    }

    private static void SafeDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
        }
    }

    private static void EnsureDiskSpace(string directory, long requiredBytes)
    {
        if (requiredBytes <= 0)
        {
            return;
        }

        var root = Path.GetPathRoot(Path.GetFullPath(directory));
        if (string.IsNullOrWhiteSpace(root))
        {
            return;
        }

        var drive = new DriveInfo(root);
        if (drive.AvailableFreeSpace < requiredBytes + 50L * 1024 * 1024)
        {
            throw new IOException("Not enough disk space is available for the update download.");
        }
    }

    private static async Task WriteMetadataAsync(string path, PendingUpdateMetadata metadata, CancellationToken cancellationToken)
    {
        var tempPath = $"{path}.{Guid.NewGuid():N}.tmp";
        await using (var stream = File.Create(tempPath))
        {
            await JsonSerializer.SerializeAsync(stream, metadata, new JsonSerializerOptions { WriteIndented = true }, cancellationToken);
        }

        if (File.Exists(path))
        {
            File.Replace(tempPath, path, null);
        }
        else
        {
            File.Move(tempPath, path);
        }
    }

    private static string RuntimeInformation()
    {
        return System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture.ToString();
    }
}
