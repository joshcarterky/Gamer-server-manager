using GameServerManager.Core.Models;

namespace GameServerManager.GameProviders;

public class ValheimProvider : GameServerProviderBase
{
    public override string GameId => "valheim";
    public override string GameName => "Valheim";
    public override int? SteamAppId => 896660;
    public override string DefaultInstallFolder => "Servers/Valheim";
    public override string ExecutableRelativePath => "valheim_server.exe";
    public override string ConfigFolder => ".";
    public override string SavesFolder => "worlds";
    public override string LogsFolder => "logs";

    public override IReadOnlyList<ServerPort> DefaultPorts => new[]
    {
        new ServerPort { Name = "Game", Port = 2456, DefaultPort = 2456, Protocol = PortProtocol.UDP, Description = "Game traffic" },
        new ServerPort { Name = "Query", Port = 2457, DefaultPort = 2457, Protocol = PortProtocol.UDP, Description = "Server query" }
    };

    public override GameServerFeatures SupportedFeatures =>
        GameServerFeatures.SteamCmdInstall |
        GameServerFeatures.Password |
        GameServerFeatures.Backups |
        GameServerFeatures.Console |
        GameServerFeatures.WorldName;

    public override ServerLaunchCommand BuildStartCommand(ServerProfile profile)
    {
        var command = base.BuildStartCommand(profile);
        var world = string.IsNullOrWhiteSpace(profile.MapName) ? profile.ProfileName.Replace(' ', '_') : profile.MapName;
        var password = string.IsNullOrWhiteSpace(profile.Password) ? "changeme" : profile.Password;
        command.Arguments = $"-nographics -batchmode -name \"{profile.ServerName}\" -port {FindPort(profile, "Game", 2456)} -world \"{world}\" -password \"{password}\" {profile.LaunchArgs}".Trim();
        return command;
    }

    private static int FindPort(ServerProfile profile, string name, int fallback) =>
        profile.Ports.FirstOrDefault(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase))?.Port ?? fallback;
}
