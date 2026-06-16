namespace GameServerManager.Core.Models
{
    /// <summary>
    /// Represents a server profile configuration for a game server
    /// </summary>
    public class ServerProfile
    {
        /// <summary>
        /// Unique identifier for the profile
        /// </summary>
        public string Id { get; set; } = Guid.NewGuid().ToString();

        /// <summary>
        /// Name of this profile
        /// </summary>
        public string ProfileName { get; set; } = string.Empty;

        /// <summary>
        /// Game ID this profile is associated with
        /// </summary>
        public string GameId { get; set; } = string.Empty;

        /// <summary>
        /// Name of the server (display name)
        /// </summary>
        public string ServerName { get; set; } = string.Empty;

        /// <summary>
        /// Path to the game server install directory
        /// </summary>
        public string InstallPath { get; set; } = string.Empty;

        /// <summary>
        /// Full path to the executable used to launch the server.
        /// </summary>
        public string ExecutablePath { get; set; } = string.Empty;

        /// <summary>
        /// Map or world name
        /// </summary>
        public string MapName { get; set; } = string.Empty;

        /// <summary>
        /// Current lifecycle status. Runtime services refresh this value.
        /// </summary>
        public ServerStatus Status { get; set; } = ServerStatus.Stopped;

        /// <summary>
        /// Whether this server should be pinned to the top of the server list.
        /// </summary>
        public bool IsFavorite { get; set; }

        /// <summary>
        /// Maximum number of players allowed
        /// </summary>
        public int MaxPlayers { get; set; } = 0;

        /// <summary>
        /// Port configuration for the server
        /// </summary>
        public List<ServerPort> Ports { get; set; } = new();

        /// <summary>
        /// Provider-specific settings stored as simple key/value pairs.
        /// </summary>
        public Dictionary<string, string> Settings { get; set; } = new();

        /// <summary>
        /// Server password
        /// </summary>
        public string Password { get; set; } = string.Empty;

        /// <summary>
        /// Admin password for RCON or admin access
        /// </summary>
        public string AdminPassword { get; set; } = string.Empty;

        /// <summary>
        /// List of mods applied to this server
        /// </summary>
        public List<ServerMod> Mods { get; set; } = new();

        /// <summary>
        /// Whether auto-update is enabled
        /// </summary>
        public bool AutoUpdateEnabled { get; set; } = false;

        /// <summary>
        /// Whether auto-backup is enabled
        /// </summary>
        public bool AutoBackupEnabled { get; set; } = false;

        /// <summary>
        /// Restart schedule (ISO 8601 duration or cron expression)
        /// </summary>
        public string? RestartSchedule { get; set; }

        /// <summary>
        /// Custom launch arguments for the server process
        /// </summary>
        public string LaunchArgs { get; set; } = string.Empty;

        /// <summary>
        /// Notes about this profile
        /// </summary>
        public string Notes { get; set; } = string.Empty;

        /// <summary>
        /// Last time this server was started, if known.
        /// </summary>
        public DateTime? LastStartedAt { get; set; }

        /// <summary>
        /// Last time this server was stopped, if known.
        /// </summary>
        public DateTime? LastStoppedAt { get; set; }

        /// <summary>
        /// Last time this server was backed up, if known.
        /// </summary>
        public DateTime? LastBackupAt { get; set; }

        /// <summary>
        /// When the profile was created
        /// </summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Last time this profile was modified
        /// </summary>
        public DateTime ModifiedAt { get; set; } = DateTime.UtcNow;
    }
}
