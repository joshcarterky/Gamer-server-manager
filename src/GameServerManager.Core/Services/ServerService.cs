using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using GameServerManager.Core.Models;

namespace GameServerManager.Core.Services;

/// <summary>
/// Service for managing game servers with JSON repository persistence.
/// </summary>
public class ServerService : IServerService
{
    private readonly string _filePath;
    private List<Server>? _servers;

    /// <summary>
    /// Gets the list of servers.
    /// </summary>
    public IReadOnlyList<Server> Servers => _servers ?? new List<Server>();

    /// <summary>
    /// Initializes the service and loads servers from disk.
    /// </summary>
    public ServerService()
    {
        _filePath = Path.Combine(AppContext.BaseDirectory, "servers.json");
        LoadServers();
    }

    /// <summary>
    /// Gets all servers.
    /// </summary>
    public IReadOnlyList<Server> GetAllServers()
    {
        return Servers;
    }

    /// <summary>
    /// Gets a server by its ID.
    /// </summary>
    public Server? GetServerById(string id)
    {
        return _servers?.Find(s => s.Id == id);
    }

    /// <summary>
    /// Adds a new server to the collection and persists it.
    /// </summary>
    public void AddServer(Server server)
    {
        if (_servers is null)
        {
            _servers = new List<Server>();
        }

        _servers.Add(server);
        SaveServers();
    }

    /// <summary>
    /// Edits an existing server and persists the changes.
    /// </summary>
    public void EditServer(Server updatedServer)
    {
        if (_servers is null) return;

        var existing = GetServerById(updatedServer.Id);
        if (existing != null)
        {
            _servers.Remove(existing);
            _servers.Add(updatedServer);
            SaveServers();
        }
    }

    /// <summary>
    /// Deletes a server by ID and persists the change.
    /// </summary>
    public void DeleteServer(string id)
    {
        if (_servers is null) return;

        var server = GetServerById(id);
        if (server != null)
        {
            _servers.Remove(server);
            SaveServers();
        }
    }

    /// <summary>
    /// Toggles the favorite status of a server.
    /// </summary>
    public void ToggleFavorite(string id)
    {
        if (_servers is null) return;

        var server = GetServerById(id);
        if (server != null)
        {
            server.IsFavorite = !server.IsFavorite;
            SaveServers();
        }
    }

    /// <summary>
    /// Saves all servers to the JSON repository.
    /// </summary>
    public void SaveServers()
    {
        if (_servers is null) return;

        var json = JsonSerializer.Serialize(_servers, new JsonSerializerOptions());
        File.WriteAllText(_filePath, json);
    }

    /// <summary>
    /// Loads all servers from the JSON repository.
    /// </summary>
    public void LoadServers()
    {
        if (File.Exists(_filePath))
        {
            var json = File.ReadAllText(_filePath);
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };

            _servers = JsonSerializer.Deserialize<List<Server>>(json, options) ?? new List<Server>();
        }
    }
}
