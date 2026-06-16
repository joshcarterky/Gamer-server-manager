namespace GameServerManager.Services.Updates;

public sealed record UpdateAsset(
    string Name,
    string DownloadUrl,
    long SizeBytes,
    string? ContentType);

public sealed record UpdateCheckResult(
    bool IsUpdateAvailable,
    string CurrentVersion,
    string? LatestVersion,
    string Channel,
    string UpdateType,
    string? ReleaseName,
    string? ReleaseNotes,
    DateTimeOffset? PublishedAt,
    string? ReleaseUrl,
    long? DownloadSizeBytes,
    IReadOnlyList<UpdateAsset> Assets,
    string? ErrorMessage)
{
    public static UpdateCheckResult NoUpdate(string currentVersion, string channel) =>
        new(false, currentVersion, currentVersion, channel, "None", null, null, null, null, null, Array.Empty<UpdateAsset>(), null);

    public static UpdateCheckResult Error(string currentVersion, string channel, string message) =>
        new(false, currentVersion, null, channel, "Unknown", null, null, null, null, null, Array.Empty<UpdateAsset>(), message);
}

public sealed record UpdateInstallReadiness(bool CanInstall, string Message);

public sealed record UpdateDownloadProgress(
    int Percent,
    long DownloadedBytes,
    long? TotalBytes,
    double BytesPerSecond);

public sealed record UpdateDownloadResult(
    bool Success,
    string? FilePath,
    string Message,
    string? TechnicalDetails = null);

public sealed record UpdateHistoryEntry(
    DateTimeOffset Timestamp,
    string Version,
    string Action,
    string Status,
    string Details);
