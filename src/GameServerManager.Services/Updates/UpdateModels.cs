namespace GameServerManager.Services.Updates;

public enum UpdateState
{
    Idle,
    Checking,
    UpToDate,
    UpdateAvailable,
    NoInstallerFound,
    PreparingDownload,
    Downloading,
    DownloadPaused,
    Downloaded,
    Verifying,
    VerificationFailed,
    ReadyToInstall,
    Installing,
    WaitingForApplicationExit,
    Restarting,
    Completed,
    InstallStarted,
    Failed,
    Cancelled
}

public sealed record UpdateAsset(
    string Name,
    string DownloadUrl,
    long SizeBytes,
    string? ContentType);

public enum UpdatePackageType
{
    Exe,
    Msi,
    PortableZip,
    Unknown
}

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
    double BytesPerSecond,
    TimeSpan? EstimatedRemaining = null,
    string? FileName = null);

public sealed record UpdateDownloadResult(
    bool Success,
    string? FilePath,
    string Message,
    string? TechnicalDetails = null,
    UpdateAsset? Asset = null,
    string? Sha256 = null,
    string? MetadataPath = null);

public sealed record PendingUpdateMetadata(
    string CurrentVersion,
    string TargetVersion,
    string ReleaseTag,
    string? ReleaseName,
    string ReleaseChannel,
    string AssetName,
    string AssetUrl,
    string PackageType,
    string Architecture,
    long? ExpectedSize,
    long ActualSize,
    string? ExpectedSha256,
    string? ActualSha256,
    string DownloadedPath,
    DateTimeOffset DownloadedAt,
    string VerificationStatus,
    string InstallStatus,
    int? InstallerProcessId,
    string RestartExecutable,
    string PreviousVersion,
    string? LastError);

public sealed record UpdateHistoryEntry(
    DateTimeOffset Timestamp,
    string Version,
    string Action,
    string Status,
    string Details);
