namespace GameServerManager.Core.Models;

/// <summary>
/// Represents a mod installed on or associated with a game server profile
/// </summary>
public class ServerMod
{
    /// <summary>
    /// Unique identifier for the mod (e.g. Steam Workshop ID)
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Display name of the mod
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Whether the mod is currently enabled
    /// </summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// Priority order for loading (lower = loaded first)
    /// </summary>
    public int LoadOrder { get; set; }

    /// <summary>
    /// External source identifier (Steam Workshop ID, ModDB ID, etc.)
    /// </summary>
    public string? SourceId { get; set; }

    /// <summary>
    /// URL to the mod source
    /// </summary>
    public string? SourceUrl { get; set; }

    /// <summary>
    /// Version of the mod
    /// </summary>
    public string? Version { get; set; }

    /// <summary>
    /// Description of the mod
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Whether this mod requires a restart to apply changes
    /// </summary>
    public bool RequiresRestart { get; set; } = true;
}