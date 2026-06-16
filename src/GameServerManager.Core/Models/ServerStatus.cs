namespace GameServerManager.Core.Models;

/// <summary>
/// Lifecycle state for a configured game server profile.
/// </summary>
public enum ServerStatus
{
    Unknown,
    Stopped,
    Starting,
    Running,
    Stopping,
    Restarting,
    Updating,
    Error
}
