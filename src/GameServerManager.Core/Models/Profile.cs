using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace GameServerManager.Core.Models
{
    /// <summary>
    /// Represents a complete server profile for saving/loading from disk.
    /// </summary>
    public class Profile
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = Guid.NewGuid().ToString();

        [JsonPropertyName("profileName")]
        public string ProfileName { get; set; } = string.Empty;

        [JsonPropertyName("gameId")]
        public string GameId { get; set; } = string.Empty;

        [JsonPropertyName("serverName")]
        public string ServerName { get; set; } = string.Empty;

        [JsonPropertyName("installPath")]
        public string InstallPath { get; set; } = string.Empty;

        [JsonPropertyName("mapName")]
        public string MapName { get; set; } = string.Empty;

        [JsonPropertyName("maxPlayers")]
        public int MaxPlayers { get; set; } = 10;

        [JsonPropertyName("ports")]
        public List<ServerPort> Ports { get; set; } = new();

        [JsonPropertyName("passwords")]
        public Dictionary<string, string> Passwords { get; set; } = new();

        [JsonPropertyName("adminPassword")]
        public string AdminPassword { get; set; } = string.Empty;

        [JsonPropertyName("mods")]
        public List<ServerMod> Mods { get; set; } = new();

        [JsonPropertyName("autoUpdateEnabled")]
        public bool AutoUpdateEnabled { get; set; } = false;

        [JsonPropertyName("autoBackupEnabled")]
        public bool AutoBackupEnabled { get; set; } = false;

        [JsonPropertyName("restartSchedule")]
        public string? RestartSchedule { get; set; }

        [JsonPropertyName("customLaunchArgs")]
        public string CustomLaunchArgs { get; set; } = string.Empty;

        [JsonPropertyName("notes")]
        public string Notes { get; set; } = string.Empty;

        [JsonPropertyName("createdAt")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [JsonPropertyName("updatedAt")]
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        public void UpdateTimestamp()
        {
            UpdatedAt = DateTime.UtcNow;
        }
    }
}
