namespace GameServerManager.Core.Models;

/// <summary>
/// Represents a network port used by a game server
/// </summary>
public class ServerPort
{
    /// <summary>
    /// Stable name for this port, such as Game, Query, RCON, or Beacon.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// The port number
    /// </summary>
    public int Port { get; set; }

    /// <summary>
    /// Protocol type (UDP/TCP)
    /// </summary>
    public PortProtocol Protocol { get; set; } = PortProtocol.UDP;

    /// <summary>
    /// Description of what this port is used for
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Whether this port is required by the server
    /// </summary>
    public bool IsRequired { get; set; } = true;

    /// <summary>
    /// Default port value for reference games
    /// </summary>
    public int DefaultPort { get; set; }
}

/// <summary>
/// Network protocol types supported by the server
/// </summary>
public enum PortProtocol
{
    /// <summary>UDP - User Datagram Protocol</summary>
    UDP,

    /// <summary>TCP - Transmission Control Protocol</summary>
    TCP,

    /// <summary>Both UDP and TCP</summary>
    Both
}
