using GameServerManager.Core.Models;

namespace GameServerManager.Core.Services;

/// <summary>
/// Manages network ports for game servers including conflict detection and allocation
/// </summary>
public class PortManagerService : IDisposable
{
    private readonly List<string> _allocatedPorts = new();
    private readonly Dictionary<string, List<int>> _profilePortMap = new();

    /// <summary>
    /// Gets the list of ports currently allocated by other profiles
    /// </summary>
    public IReadOnlyList<string> AllocatedPorts => _allocatedPorts.AsReadOnly();

    /// <summary>
    /// Registers a profile's ports for conflict detection
    /// </summary>
    public void RegisterProfile(string profileId, List<ServerPort> ports)
    {
        if (!_profilePortMap.ContainsKey(profileId))
            _profilePortMap[profileId] = new();

        var portNumbers = ports.Select(p => p.Port).ToList();
        _profilePortMap[profileId] = portNumbers;

        foreach (var port in portNumbers)
        {
            if (!_allocatedPorts.Contains(port.ToString()))
                _allocatedPorts.Add(port.ToString());
        }
    }

    /// <summary>
    /// Unregisters a profile's ports from conflict detection
    /// </summary>
    public void UnregisterProfile(string profileId)
    {
        if (_profilePortMap.TryGetValue(profileId, out var ports))
        {
            foreach (var port in ports)
            {
                _allocatedPorts.Remove(port.ToString());
            }
            _profilePortMap.Remove(profileId);
        }
    }

    /// <summary>
    /// Checks if a set of ports conflicts with any existing profile's ports
    /// </summary>
    public List<string> CheckPortConflicts(List<ServerPort> ports)
    {
        var conflicts = new List<string>();
        foreach (var port in ports)
        {
            if (_allocatedPorts.Contains(port.Port.ToString()))
                conflicts.Add(port.Port.ToString());
        }
        return conflicts;
    }

    /// <summary>
    /// Suggests alternative ports when conflicts are detected
    /// </summary>
    public List<int> SuggestPorts(List<ServerPort> requiredPorts, int startFrom = 27000)
    {
        var suggestions = new List<int>();
        var candidate = startFrom;

        foreach (var required in requiredPorts)
        {
            while (_allocatedPorts.Contains(candidate.ToString()) || _allocatedPorts.Contains($"{candidate - 1}".ToString()))
            {
                candidate++;
            }
            suggestions.Add(candidate);
            candidate += 5; // Skip a few ports to avoid edge conflicts
        }

        return suggestions;
    }

    /// <summary>
    /// Validates a port number (range 1-65535)
    /// </summary>
    public bool IsValidPort(int port)
    {
        return port >= 1 && port <= 65535;
    }

    /// <summary>
    /// Checks if a port is in the well-known ports range (0-1023)
    /// </summary>
    public bool IsWellKnownPort(int port)
    {
        return port >= 0 && port <= 1023;
    }

    public void Dispose()
    {
        _allocatedPorts.Clear();
        _profilePortMap.Clear();
    }
}
