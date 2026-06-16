using GameServerManager.Core.Models;

namespace GameServerManager.GameProviders;

public class RustProvider : GameServerProviderBase
{
    public override string GameId => "rust";
    public override string GameName => "Rust";
    public override int? SteamAppId => 258550;
    public override string DefaultInstallFolder => "Servers/Rust";
    public override string ExecutableRelativePath => "RustDedicated.exe";
    public override string ConfigFolder => "server";
    public override string SavesFolder => "server";
    public override string LogsFolder => "logs";

    public override IReadOnlyList<ServerPort> DefaultPorts => new[]
    {
        new ServerPort { Name = "Game", Port = 28015, DefaultPort = 28015, Protocol = PortProtocol.UDP, Description = "Game traffic" },
        new ServerPort { Name = "RCON", Port = 28016, DefaultPort = 28016, Protocol = PortProtocol.TCP, Description = "Remote console" }
    };

    public override GameServerFeatures SupportedFeatures =>
        GameServerFeatures.SteamCmdInstall |
        GameServerFeatures.Rcon |
        GameServerFeatures.Mods |
        GameServerFeatures.Backups |
        GameServerFeatures.Console |
        GameServerFeatures.WorldName |
        GameServerFeatures.MaxPlayers;

    public override ServerLaunchCommand BuildStartCommand(ServerProfile profile)
    {
        var command = base.BuildStartCommand(profile);
        var identity = profile.Settings.TryGetValue("Identity", out var value) ? value : profile.ProfileName.Replace(' ', '_');
        command.Arguments = $"-batchmode +server.identity \"{identity}\" +server.port {FindPort(profile, "Game", 28015)} +rcon.port {FindPort(profile, "RCON", 28016)} +server.maxplayers {profile.MaxPlayers} +server.hostname \"{profile.ServerName}\" {profile.LaunchArgs}".Trim();
        return command;
    }

    private static int FindPort(ServerProfile profile, string name, int fallback) =>
        profile.Ports.FirstOrDefault(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase))?.Port ?? fallback;
}
