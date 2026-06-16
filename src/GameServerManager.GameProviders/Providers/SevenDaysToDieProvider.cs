using GameServerManager.Core.Models;

namespace GameServerManager.GameProviders;

public class SevenDaysToDieProvider : GameServerProviderBase
{
    public override string GameId => "seven_days_to_die";
    public override string GameName => "7 Days to Die";
    public override int? SteamAppId => 294420;
    public override string DefaultInstallFolder => "Servers/7_Days_To_Die";
    public override string ExecutableRelativePath => "7DaysToDieServer.exe";
    public override string ConfigFolder => ".";
    public override string SavesFolder => "Saves";
    public override string LogsFolder => "logs";

    public override IReadOnlyList<ServerPort> DefaultPorts => new[]
    {
        new ServerPort { Name = "Game", Port = 26900, DefaultPort = 26900, Protocol = PortProtocol.UDP, Description = "Game traffic" },
        new ServerPort { Name = "GamePlus1", Port = 26901, DefaultPort = 26901, Protocol = PortProtocol.UDP, Description = "Adjacent game traffic" },
        new ServerPort { Name = "Web", Port = 8080, DefaultPort = 8080, Protocol = PortProtocol.TCP, Description = "Web admin", IsRequired = false }
    };

    public override GameServerFeatures SupportedFeatures =>
        GameServerFeatures.SteamCmdInstall |
        GameServerFeatures.Password |
        GameServerFeatures.Backups |
        GameServerFeatures.Console |
        GameServerFeatures.Mods |
        GameServerFeatures.WorldName |
        GameServerFeatures.MaxPlayers;

    public override ServerLaunchCommand BuildStartCommand(ServerProfile profile)
    {
        var command = base.BuildStartCommand(profile);
        command.Arguments = $"-configfile=serverconfig.xml {profile.LaunchArgs}".Trim();
        return command;
    }
}
