namespace GameServerManager.Core.Models;

/// <summary>
/// Represents a running server process with its state and metrics
/// </summary>
public class ServerProcessInfo
{
    /// <summary>
    /// Unique identifier for this process instance
    /// </summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// The profile ID this process belongs to
    /// </summary>
    public string ProfileId { get; set; } = string.Empty;

    /// <summary>
    /// Server name from the profile
    /// </summary>
    public string ServerName { get; set; } = string.Empty;

    /// <summary>
    /// Game ID this server belongs to
    /// </summary>
    public string GameId { get; set; } = string.Empty;

    /// <summary>
    /// Current process ID (0 if not running)
    /// </summary>
    public int ProcessId { get; set; }

    /// <summary>
    /// Whether the server is currently running
    /// </summary>
    public bool IsRunning => ProcessId > 0 && !HasExited;

    /// <summary>
    /// Whether the process has exited
    /// </summary>
    public bool HasExited { get; set; }

    /// <summary>
    /// CPU usage percentage (0-100)
    /// </summary>
    public double CpuUsage { get; set; }

    /// <summary>
    /// Memory usage in bytes
    /// </summary>
    public long MemoryUsage { get; set; }

    /// <summary>
    /// Timestamp of last status update
    /// </summary>
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Server uptime in ticks (only valid when running)
    /// </summary>
    public TimeSpan Uptime { get; set; }

    /// <summary>
    /// Number of connected players (if supported by game)
    /// </summary>
    public int ConnectedPlayers { get; set; }

    /// <summary>
    /// Maximum number of players supported
    /// </summary>
    public int MaxPlayersSupported { get; set; }

    /// <summary>
    /// Current server tick count (game-specific)
    /// </summary>
    public long TickCount { get; set; }

    /// <summary>
    /// Average FPS of the server
    /// </summary>
    public double AvgFPS { get; set; }

    /// <summary>
    /// Console output captured from the process
    /// </summary>
    public string ConsoleOutput { get; set; } = string.Empty;

    /// <summary>
    /// Last console command sent to the server
    /// </summary>
    public string LastCommand { get; set; } = string.Empty;

    /// <summary>
    /// Server status enum representation
    /// </summary>
    public ServerStatus Status { get; set; } = ServerStatus.Stopped;

    /// <summary>
    /// Error message if the process failed to start or crashed
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Exit code of the process (valid when HasExited is true)
    /// </summary>
    public int ExitCode { get; set; }

    /// <summary>
    /// Whether this process was detected as crashed
    /// </summary>
    public bool IsCrashed { get; set; }

    /// <summary>
    /// Timestamp when the server started
    /// </summary>
    public DateTime? StartTime { get; set; }

    /// <summary>
    /// Whether auto-restart is enabled for this process
    /// </summary>
    public bool AutoRestartEnabled { get; set; }

    /// <summary>
    /// Number of times the server has been restarted automatically
    /// </summary>
    public int RestartCount { get; set; }

    /// <summary>
    /// Launch arguments used to start this server process
    /// </summary>
    public string? LaunchArgs { get; set; }

    /// <summary>
    /// Working directory of the server process
    /// </summary>
    public string? WorkingDirectory { get; set; }

    /// <summary>
    /// Associated profile data for reference
    /// </summary>
    public ServerProfile? Profile { get; set; }
}
