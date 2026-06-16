using System.Diagnostics;
using System.Net.Http.Headers;
using System.Security.Cryptography;

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
        var asset = PickBestWindowsAsset(update.Assets);
        if (asset is null)
        {
            return new UpdateDownloadResult(false, null, UpdateErrorMessages.For("NoDownloadableAsset"));
        }

        try
        {
            Directory.CreateDirectory(_paths.UpdateDownloadsDirectory);
            var destination = Path.Combine(_paths.UpdateDownloadsDirectory, SanitizeFileName(asset.Name));
            await _logger.LogAsync("Downloading update asset.", $"{asset.DownloadUrl} -> {destination}", cancellationToken);

            using var response = await _httpClient.GetAsync(asset.DownloadUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            response.EnsureSuccessStatusCode();

            var totalBytes = response.Content.Headers.ContentLength ?? (asset.SizeBytes > 0 ? asset.SizeBytes : null);
            await using var source = await response.Content.ReadAsStreamAsync(cancellationToken);
            await using var target = File.Create(destination);

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
                progress(new UpdateDownloadProgress(percent, downloaded, totalBytes, downloaded / Math.Max(stopwatch.Elapsed.TotalSeconds, 0.1)));
            }

            if (!File.Exists(destination) || new FileInfo(destination).Length == 0)
            {
                return new UpdateDownloadResult(false, destination, UpdateErrorMessages.For("DownloadFailed", "Downloaded file is missing or empty."));
            }

            var checksumAsset = update.Assets.FirstOrDefault(a =>
                string.Equals(a.Name, "checksums.txt", StringComparison.OrdinalIgnoreCase)
                || a.Name.Contains("Checksums", StringComparison.OrdinalIgnoreCase));
            if (checksumAsset is not null)
            {
                var checksumResult = await VerifyChecksumAsync(destination, checksumAsset.DownloadUrl, cancellationToken);
                if (!checksumResult.Success)
                {
                    return checksumResult;
                }
            }

            await _logger.LogAsync("Download complete.", destination, cancellationToken);
            return new UpdateDownloadResult(true, destination, "Download complete.");
        }
        catch (UnauthorizedAccessException ex)
        {
            await _logger.LogAsync("Download permission denied.", ex.ToString(), cancellationToken);
            return new UpdateDownloadResult(false, null, UpdateErrorMessages.For("PermissionDenied"), ex.ToString());
        }
        catch (Exception ex)
        {
            await _logger.LogAsync("Download failed.", ex.ToString(), cancellationToken);
            return new UpdateDownloadResult(false, null, UpdateErrorMessages.For("DownloadFailed"), ex.ToString());
        }
    }

    private async Task<UpdateDownloadResult> VerifyChecksumAsync(string filePath, string checksumUrl, CancellationToken cancellationToken)
    {
        var checksums = await _httpClient.GetStringAsync(checksumUrl, cancellationToken);
        var fileName = Path.GetFileName(filePath);
        var line = checksums.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault(value => value.Contains(fileName, StringComparison.OrdinalIgnoreCase));
        if (line is null)
        {
            return new UpdateDownloadResult(true, filePath, "Download complete. No matching checksum entry was found.");
        }

        var expected = line.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        if (string.IsNullOrWhiteSpace(expected))
        {
            return new UpdateDownloadResult(false, filePath, UpdateErrorMessages.For("ChecksumFailed", "Checksum entry was empty."));
        }

        await using var stream = File.OpenRead(filePath);
        var actual = Convert.ToHexString(await SHA256.HashDataAsync(stream, cancellationToken));
        if (!string.Equals(expected, actual, StringComparison.OrdinalIgnoreCase))
        {
            await _logger.LogAsync("Checksum failed.", $"Expected {expected}, got {actual}", cancellationToken);
            return new UpdateDownloadResult(false, filePath, UpdateErrorMessages.For("ChecksumFailed"));
        }

        await _logger.LogAsync("Checksum verified.", fileName, cancellationToken);
        return new UpdateDownloadResult(true, filePath, "Download complete and checksum verified.");
    }

    public static UpdateAsset? PickBestWindowsAsset(IReadOnlyList<UpdateAsset> assets)
    {
        var candidates = assets
            .Where(asset => asset.Name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
                || asset.Name.EndsWith(".msi", StringComparison.OrdinalIgnoreCase)
                || asset.Name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
            .ToArray();

        return candidates.FirstOrDefault(asset => asset.Name.Contains("Setup", StringComparison.OrdinalIgnoreCase))
            ?? candidates.FirstOrDefault(asset => asset.Name.Contains("Installer", StringComparison.OrdinalIgnoreCase))
            ?? candidates.FirstOrDefault(asset => asset.Name.Contains("Portable", StringComparison.OrdinalIgnoreCase))
            ?? candidates.FirstOrDefault();
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
        foreach (var invalid in Path.GetInvalidFileNameChars())
        {
            fileName = fileName.Replace(invalid, '-');
        }

        return fileName;
    }
}
