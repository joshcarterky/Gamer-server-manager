using GameServerManager.Core.Models;

namespace GameServerManager.GameProviders;

public class MinecraftJavaProvider : GameServerProviderBase
{
    public override string GameId => "minecraft_java";
    public override string GameName => "Minecraft Java";
    public override string DefaultInstallFolder => "Servers/Minecraft";
    public override string ExecutableRelativePath => "server.jar";
    public override string ConfigFolder => ".";
    public override string SavesFolder => "world";
    public override string LogsFolder => "logs";

    public override IReadOnlyList<ServerPort> DefaultPorts => new[]
    {
        Port("Game", 25565, PortProtocol.TCP, "Game traffic and status query"),
        Port("Query", 25565, PortProtocol.UDP, "GameSpy query", false),
        Port("RCON", 25575, PortProtocol.TCP, "Remote console", false)
    };

    public override GameServerFeatures SupportedFeatures =>
        GameServerFeatures.Password |
        GameServerFeatures.Rcon |
        GameServerFeatures.Mods |
        GameServerFeatures.Backups |
        GameServerFeatures.Console |
        GameServerFeatures.WorldName |
        GameServerFeatures.MaxPlayers;

    public override IReadOnlyList<ServerSettingDefinition> SettingsDefinitions => new[]
    {
        Text("JavaPath", "Java Path", "java"),
        Number("MemoryMb", "Memory MB", "4096", 512, 65536),
        Number("MaxPlayers", "Max Players", "20", 1, 1000),
        Text("JarFile", "Server Jar", "server.jar", true)
    };

    public override ServerLaunchCommand BuildStartCommand(ServerProfile profile)
    {
        var memory = profile.Settings.TryGetValue("MemoryMb", out var value) ? value : "4096";
        var jar = profile.Settings.TryGetValue("JarFile", out var jarFile) ? jarFile : "server.jar";
        var javaPath = profile.Settings.TryGetValue("JavaPath", out var java) ? java : "java";

        return new ServerLaunchCommand
        {
            ExecutablePath = javaPath,
            WorkingDirectory = profile.InstallPath,
            Arguments = $"-Xms{memory}M -Xmx{memory}M -jar \"{jar}\" nogui {profile.LaunchArgs}".Trim()
        };
    }

    private static ServerPort Port(string name, int port, PortProtocol protocol, string description, bool required = true) =>
        new() { Name = name, Port = port, DefaultPort = port, Protocol = protocol, Description = description, IsRequired = required };

    private static ServerSettingDefinition Text(string key, string displayName, string defaultValue, bool required = false) =>
        new() { SettingKey = key, DisplayName = displayName, DefaultValue = defaultValue, ControlType = SettingControlType.TextBox, IsRequired = required };

    private static ServerSettingDefinition Number(string key, string displayName, string defaultValue, int min, int max) =>
        new() { SettingKey = key, DisplayName = displayName, DefaultValue = defaultValue, ControlType = SettingControlType.NumberBox, MinValue = min, MaxValue = max };
}
