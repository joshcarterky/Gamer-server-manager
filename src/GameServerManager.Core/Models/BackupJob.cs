namespace GameServerManager.Core.Models;

/// <summary>
/// Represents a backup job/schedule for a game server
/// </summary>
public class BackupJob
{
    /// <summary>
    /// Unique identifier for the backup job
    /// </summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Name of this backup configuration
    /// </summary>
    public string Name { get; set; } = "Default Backup";

    /// <summary>
    /// Type of backup to perform
    /// </summary>
    public BackupType BackupType { get; set; } = BackupType.SaveOnly;

    /// <summary>
    /// Schedule interval for automatic backups (in minutes)
    /// Set to 0 to disable scheduled backups
    /// </summary>
    public int IntervalMinutes { get; set; } = 1440; // Default: once per day

    /// <summary>
    /// Whether this backup is enabled/scheduled
    /// </summary>
    public bool IsEnabled { get; set; } = false;

    /// <summary>
    /// Maximum number of backups to retain (0 = unlimited)
    /// </summary>
    public int MaxBackups { get; set; } = 10;

    /// <summary>
    /// Whether to compress backups into zip files
    /// </summary>
    public bool Compress { get; set; } = true;

    /// <summary>
    /// Backup destination folder path
    /// </summary>
    public string? DestinationPath { get; set; }

    /// <summary>
    /// Whether to include server config files in backup
    /// </summary>
    public bool IncludeConfig { get; set; } = true;

    /// <summary>
    /// Whether to include world/save data in backup
    /// </summary>
    public bool IncludeWorldData { get; set; } = true;

    /// <summary>
    /// Whether to include logs in backup
    /// </summary>
    public bool IncludeLogs { get; set; } = false;
}

/// <summary>
/// Types of backups supported by the system
/// </summary>
public enum BackupType
{
    /// <summary>Save only - backs up world/save files</summary>
    SaveOnly,

    /// <summary>Config only - backs up configuration files</summary>
    ConfigOnly,

    /// <summary>Full server backup including all files</summary>
    FullServer
}