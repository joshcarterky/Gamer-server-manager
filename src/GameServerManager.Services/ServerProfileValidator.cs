using GameServerManager.Core.Models;

namespace GameServerManager.Services;

public sealed class ServerProfileValidator
{
    public void Validate(ServerProfile profile)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(profile.GameId))
        {
            errors.Add("Game Type is required.");
        }

        if (string.IsNullOrWhiteSpace(profile.ServerName))
        {
            errors.Add("Server Name is required.");
        }

        if (string.IsNullOrWhiteSpace(profile.InstallPath))
        {
            errors.Add("Install Path is required.");
        }

        if (string.IsNullOrWhiteSpace(profile.ExecutablePath))
        {
            errors.Add("Executable Path is required.");
        }

        if (profile.MaxPlayers < 1)
        {
            errors.Add("Max Players must be at least 1.");
        }

        foreach (var port in profile.Ports)
        {
            if (port.Port < 1 || port.Port > 65535)
            {
                errors.Add($"{port.Name} Port must be between 1 and 65535.");
            }
        }

        if (errors.Count > 0)
        {
            throw new InvalidOperationException(string.Join(" ", errors));
        }
    }
}
