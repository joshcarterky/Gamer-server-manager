using GameServerManager.Core.Models;

namespace GameServerManager.GameProviders;

public class MinecraftBedrockProvider : GameServerProviderBase
{
    public override string GameId => "minecraft_bedrock";
    public override string GameName => "Minecraft Bedrock";
    public override string DefaultInstallFolder => "Servers/Minecraft_Bedrock";
    public override string ExecutableRelativePath => "bedrock_server.exe";
    public override string ConfigFolder => ".";
    public override string SavesFolder => "worlds";
    public override string LogsFolder => "logs";

    public override IReadOnlyList<ServerPort> DefaultPorts => new[]
    {
        new ServerPort { Name = "Game", Port = 19132, DefaultPort = 19132, Protocol = PortProtocol.UDP, Description = "IPv4 game traffic" },
        new ServerPort { Name = "GameIPv6", Port = 19133, DefaultPort = 19133, Protocol = PortProtocol.UDP, Description = "IPv6 game traffic", IsRequired = false }
    };

    public override GameServerFeatures SupportedFeatures =>
        GameServerFeatures.Backups |
        GameServerFeatures.Console |
        GameServerFeatures.WorldName |
        GameServerFeatures.MaxPlayers;

    public override IReadOnlyList<ServerSettingDefinition> SettingsDefinitions => new[]
    {
        new ServerSettingDefinition { SettingKey = "LevelName", DisplayName = "Level Name", DefaultValue = "Bedrock level", ControlType = SettingControlType.TextBox },
        new ServerSettingDefinition { SettingKey = "MaxPlayers", DisplayName = "Max Players", DefaultValue = "10", ControlType = SettingControlType.NumberBox, MinValue = 1, MaxValue = 1000 }
    };
}
