using GameServerManager.Core.Models;
using GameServerManager.Core.Services;

namespace GameServerManager.Services;

public class GameRconService
{
    public async Task<string> SendCommandAsync(ServerProfile profile, string host, int port, string password, string command)
    {
        using var rcon = new RCONService(host, port, password);
        var connected = await rcon.ConnectAsync();
        if (!connected)
        {
            throw new InvalidOperationException($"Unable to connect to RCON for '{profile.ProfileName}'.");
        }

        return await rcon.SendCommandAsync(command);
    }
}
