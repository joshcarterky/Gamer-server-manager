using System.Net;
using System.Net.Sockets;
using System.Text;
using GameServerManager.Core.Models;

namespace GameServerManager.Services;

public interface IServerQueryClient
{
    Task<ServerQueryResult> QueryAsync(ServerProfile profile, CancellationToken cancellationToken = default);
}

public class ServerQueryService
{
    private static readonly HashSet<string> SteamQueryGameIds = new(StringComparer.OrdinalIgnoreCase)
    {
        "ark_survival_ascended",
        "ark_survival_evolved",
        "seven_days_to_die",
        "palworld",
        "rust",
        "valheim",
        "conan_exiles",
        "project_zomboid",
        "satisfactory"
    };

    public async Task<ServerQueryResult> QueryAsync(ServerProfile profile, CancellationToken cancellationToken = default)
    {
        if (SteamQueryGameIds.Contains(profile.GameId))
        {
            return await QuerySteamInfoAsync(profile, cancellationToken);
        }

        return new ServerQueryResult(
            profile.Status == ServerStatus.Running,
            0,
            profile.MaxPlayers,
            $"No query adapter is available for {profile.GameId} yet.");
    }

    private static async Task<ServerQueryResult> QuerySteamInfoAsync(ServerProfile profile, CancellationToken cancellationToken)
    {
        var host = GetHost(profile);
        var port = GetPort(profile, "Query") ?? GetPort(profile, "Game");
        if (string.IsNullOrWhiteSpace(host) || port is null or <= 0)
        {
            return new ServerQueryResult(false, 0, profile.MaxPlayers, "Missing query endpoint.");
        }

        try
        {
            using var udp = new UdpClient();
            udp.Client.ReceiveTimeout = 1500;
            udp.Client.SendTimeout = 1500;

            var request = BuildA2SInfoRequest(null);
            await udp.SendAsync(request, request.Length, host, port.Value);
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(TimeSpan.FromMilliseconds(1500));

            var response = await ReceiveAsync(udp, timeoutCts.Token);
            if (response.Length >= 5 && response[4] == 0x41 && response.Length >= 9)
            {
                var challenge = BitConverter.ToInt32(response, 5);
                request = BuildA2SInfoRequest(challenge);
                await udp.SendAsync(request, request.Length, host, port.Value);
                response = await ReceiveAsync(udp, timeoutCts.Token);
            }

            var parsed = TryParseA2SInfo(response, profile.MaxPlayers);
            return parsed ?? new ServerQueryResult(true, 0, profile.MaxPlayers, "Query responded but could not be parsed.");
        }
        catch (OperationCanceledException)
        {
            return new ServerQueryResult(false, 0, profile.MaxPlayers, "Query timed out.");
        }
        catch (Exception ex) when (ex is SocketException or ObjectDisposedException or IOException)
        {
            return new ServerQueryResult(false, 0, profile.MaxPlayers, $"Query failed: {ex.Message}");
        }
    }

    private static async Task<byte[]> ReceiveAsync(UdpClient udp, CancellationToken cancellationToken)
    {
        var receiveTask = udp.ReceiveAsync();
        var completed = await Task.WhenAny(receiveTask, Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken));
        if (completed != receiveTask)
        {
            throw new OperationCanceledException(cancellationToken);
        }

        return receiveTask.Result.Buffer;
    }

    private static byte[] BuildA2SInfoRequest(int? challenge)
    {
        var header = new byte[] { 0xFF, 0xFF, 0xFF, 0xFF, 0x54 };
        var payload = Encoding.ASCII.GetBytes("Source Engine Query\0");
        if (challenge == null)
        {
            return header.Concat(payload).ToArray();
        }

        return header
            .Concat(payload)
            .Concat(BitConverter.GetBytes(challenge.Value))
            .ToArray();
    }

    private static ServerQueryResult? TryParseA2SInfo(byte[] response, int fallbackMaxPlayers)
    {
        if (response.Length < 6 || response[4] != 0x49)
        {
            return null;
        }

        var offset = 6;
        if (!SkipNullTerminated(response, ref offset) ||
            !SkipNullTerminated(response, ref offset) ||
            !SkipNullTerminated(response, ref offset) ||
            !SkipNullTerminated(response, ref offset))
        {
            return null;
        }

        offset += 2;
        if (offset + 2 >= response.Length)
        {
            return null;
        }

        var players = response[offset];
        var maxPlayers = response[offset + 1];
        return new ServerQueryResult(true, players, maxPlayers == 0 ? fallbackMaxPlayers : maxPlayers, "A2S_INFO query succeeded.");
    }

    private static bool SkipNullTerminated(byte[] buffer, ref int offset)
    {
        while (offset < buffer.Length)
        {
            if (buffer[offset++] == 0)
            {
                return true;
            }
        }

        return false;
    }

    private static string GetHost(ServerProfile profile)
    {
        if (!profile.Settings.TryGetValue("ipAddress", out var value) || string.IsNullOrWhiteSpace(value) || value == "0.0.0.0")
        {
            return "127.0.0.1";
        }

        return value;
    }

    private static int? GetPort(ServerProfile profile, string name)
    {
        return profile.Ports
            .FirstOrDefault(port => port.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
            ?.Port;
    }
}

public record ServerQueryResult(bool Online, int PlayerCount, int MaxPlayers, string Message);
