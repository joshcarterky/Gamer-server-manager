using System.Collections.Generic;
using GameServerManager.Core.Models;

namespace GameServerManager.Core.Services;

/// <summary>
/// Interface for server management operations.
/// </summary>
public interface IServerService
{
    /// <summary>
    /// Gets all servers.
    /// </summary>
    IReadOnlyList<Server> Servers { get; }

    /// <summary>
    /// Gets a server by its ID.
    /// </summary>
    Server? GetServerById(string id);

    /// <summary>
    /// Adds a new server to the collection.
    /// </summary>
    void AddServer(Server server);

    /// <summary>
    /// Edits an existing server.
    /// </summary>
    void EditServer(Server updatedServer);

    /// <summary>
    /// Deletes a server by ID.
    /// </summary>
    void DeleteServer(string id);

    /// <summary>
    /// Toggles the favorite status of a server.
    /// </summary>
    void ToggleFavorite(string id);

    /// <summary>
    /// Saves all servers to the JSON repository.
    /// </summary>
    void SaveServers();

    /// <summary>
    /// Loads all servers from the JSON repository.
    /// </summary>
    void LoadServers();
}