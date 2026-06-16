using GameServerManager.Core.Models;
using static GameServerManager.GameProviders.AdditionalGameProviderDefaults;

namespace GameServerManager.GameProviders;

public class ArkSurvivalEvolvedProvider : GameServerProviderBase
{
    public override string GameId => "ark_survival_evolved";
    public override string GameName => "ARK: Survival Evolved";
    public override int? SteamAppId => 376030;
    public override string DefaultInstallFolder => "Servers/ARK_Survival_Evolved";
    public override string ExecutableRelativePath => "ShooterGame/Binaries/Win64/ShooterGameServer.exe";
    public override string ConfigFolder => "ShooterGame/Saved/Config/WindowsServer";
    public override string SavesFolder => "ShooterGame/Saved/SavedArks";
    public override string LogsFolder => "ShooterGame/Saved/Logs";
    public override IReadOnlyList<ServerPort> DefaultPorts => Ports(7777, 27015, 27020);
    public override GameServerFeatures SupportedFeatures => CommonSteamFeatures;
}

public class ConanExilesProvider : GameServerProviderBase
{
    public override string GameId => "conan_exiles";
    public override string GameName => "Conan Exiles";
    public override int? SteamAppId => 443030;
    public override string DefaultInstallFolder => "Servers/Conan_Exiles";
    public override string ExecutableRelativePath => "ConanSandboxServer.exe";
    public override string ConfigFolder => "ConanSandbox/Saved/Config/WindowsServer";
    public override string SavesFolder => "ConanSandbox/Saved";
    public override string LogsFolder => "ConanSandbox/Saved/Logs";
    public override IReadOnlyList<ServerPort> DefaultPorts => Ports(7777, 27015, 25575);
    public override GameServerFeatures SupportedFeatures => CommonSteamFeatures;
}

public class ProjectZomboidProvider : GameServerProviderBase
{
    public override string GameId => "project_zomboid";
    public override string GameName => "Project Zomboid";
    public override int? SteamAppId => 380870;
    public override string DefaultInstallFolder => "Servers/Project_Zomboid";
    public override string ExecutableRelativePath => "StartServer64.bat";
    public override string ConfigFolder => "Zomboid/Server";
    public override string SavesFolder => "Zomboid/Saves/Multiplayer";
    public override string LogsFolder => "Zomboid/Logs";
    public override IReadOnlyList<ServerPort> DefaultPorts => Ports(16261, 16262, 27015);
    public override GameServerFeatures SupportedFeatures => CommonSteamFeatures;
}

public class SatisfactoryProvider : GameServerProviderBase
{
    public override string GameId => "satisfactory";
    public override string GameName => "Satisfactory";
    public override int? SteamAppId => 1690800;
    public override string DefaultInstallFolder => "Servers/Satisfactory";
    public override string ExecutableRelativePath => "FactoryServer.exe";
    public override string ConfigFolder => "FactoryGame/Saved/Config/WindowsServer";
    public override string SavesFolder => "FactoryGame/Saved/SaveGames";
    public override string LogsFolder => "FactoryGame/Saved/Logs";
    public override IReadOnlyList<ServerPort> DefaultPorts => Ports(7777, 15777, 15000);
    public override GameServerFeatures SupportedFeatures => CommonSteamFeatures;
}

public class FactorioProvider : GameServerProviderBase
{
    public override string GameId => "factorio";
    public override string GameName => "Factorio";
    public override int? SteamAppId => null;
    public override string DefaultInstallFolder => "Servers/Factorio";
    public override string ExecutableRelativePath => "bin/x64/factorio.exe";
    public override string ConfigFolder => "config";
    public override string SavesFolder => "saves";
    public override string LogsFolder => "logs";
    public override IReadOnlyList<ServerPort> DefaultPorts => Ports(34197, 34197, 0);
    public override GameServerFeatures SupportedFeatures =>
        GameServerFeatures.Backups |
        GameServerFeatures.Console |
        GameServerFeatures.Mods |
        GameServerFeatures.WorldName |
        GameServerFeatures.MaxPlayers;
}

public class GenericServerProvider : GameServerProviderBase
{
    public override string GameId => "generic_server";
    public override string GameName => "Generic Server";
    public override string DefaultInstallFolder => "Servers/Generic";
    public override string ExecutableRelativePath => "server.exe";
    public override string ConfigFolder => "config";
    public override string SavesFolder => "saves";
    public override string LogsFolder => "logs";
    public override IReadOnlyList<ServerPort> DefaultPorts => Ports(7777, 27015, 25575);
    public override GameServerFeatures SupportedFeatures =>
        GameServerFeatures.Backups |
        GameServerFeatures.Console |
        GameServerFeatures.MaxPlayers;
}

internal static class AdditionalGameProviderDefaults
{
    public static GameServerFeatures CommonSteamFeatures =>
        GameServerFeatures.SteamCmdInstall |
        GameServerFeatures.Password |
        GameServerFeatures.AdminPassword |
        GameServerFeatures.Rcon |
        GameServerFeatures.Mods |
        GameServerFeatures.Backups |
        GameServerFeatures.Console |
        GameServerFeatures.WorldName |
        GameServerFeatures.MaxPlayers;

    public static IReadOnlyList<ServerPort> Ports(int gamePort, int queryPort, int rconPort)
    {
        var ports = new List<ServerPort>
        {
            Port("Game", gamePort, PortProtocol.UDP, "Game traffic"),
            Port("Query", queryPort, PortProtocol.UDP, "Server query")
        };

        if (rconPort > 0)
        {
            ports.Add(Port("RCON", rconPort, PortProtocol.TCP, "Remote console", false));
        }

        return ports;
    }

    private static ServerPort Port(string name, int port, PortProtocol protocol, string description, bool required = true)
    {
        return new ServerPort
        {
            Name = name,
            Port = port,
            DefaultPort = port,
            Protocol = protocol,
            Description = description,
            IsRequired = required
        };
    }
}
