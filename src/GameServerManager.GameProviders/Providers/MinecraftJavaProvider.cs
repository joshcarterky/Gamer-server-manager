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
        GameServerFeatures.MaxPlayers |
        GameServerFeatures.MemoryLimit;

    public override IReadOnlyList<ServerSettingDefinition> SettingsDefinitions => new[]
    {
        Text("JavaPath", "Java Path", "java"),
        Dropdown("MemoryMode", "Memory Mode", "Auto", new[] { "Auto:Game Default / Auto", "Custom:Custom Limit" }, advanced: true),
        Number("CustomMemoryMb", "Custom Memory Limit (MB)", null, 512, 65536, advanced: true),
        Number("MaxPlayers", "Max Players", "20", 1, 1000),
        Text("JarFile", "Server Jar", "server.jar", true)
    };

    public override ServerLaunchCommand BuildStartCommand(ServerProfile profile)
    {
        var jar = profile.Settings.TryGetValue("JarFile", out var jarFile) ? jarFile : "server.jar";
        var javaPath = profile.Settings.TryGetValue("JavaPath", out var java) ? java : "java";
        var memoryArgs = GetMemoryArguments(profile);

        return new ServerLaunchCommand
        {
            ExecutablePath = javaPath,
            WorkingDirectory = profile.InstallPath,
            Arguments = $"{memoryArgs} -jar \"{jar}\" nogui {profile.LaunchArgs}".Trim()
        };
    }

    private static ServerPort Port(string name, int port, PortProtocol protocol, string description, bool required = true) =>
        new() { Name = name, Port = port, DefaultPort = port, Protocol = protocol, Description = description, IsRequired = required };

    private static ServerSettingDefinition Text(string key, string displayName, string defaultValue, bool required = false) =>
        new() { SettingKey = key, DisplayName = displayName, DefaultValue = defaultValue, ControlType = SettingControlType.TextBox, IsRequired = required };

    private static ServerSettingDefinition Number(string key, string displayName, string? defaultValue, int min, int max, bool advanced = false) =>
        new() { SettingKey = key, DisplayName = displayName, DefaultValue = defaultValue, ControlType = SettingControlType.NumberBox, MinValue = min, MaxValue = max, IsAdvanced = advanced };

    private static ServerSettingDefinition Dropdown(string key, string displayName, string defaultValue, string[] options, bool advanced = false) =>
        new() { SettingKey = key, DisplayName = displayName, DefaultValue = defaultValue, ControlType = SettingControlType.Dropdown, Options = options.ToList(), IsAdvanced = advanced };

    private static string GetMemoryArguments(ServerProfile profile)
    {
        var mode = profile.Settings.TryGetValue("MemoryMode", out var memoryMode) ? memoryMode : "Auto";
        if (!mode.Equals("Custom", StringComparison.OrdinalIgnoreCase))
        {
            return string.Empty;
        }

        var memoryValue = profile.Settings.TryGetValue("CustomMemoryMb", out var customMemory)
            ? customMemory
            : profile.Settings.TryGetValue("MemoryMb", out var legacyMemory)
                ? legacyMemory
                : string.Empty;

        return int.TryParse(memoryValue, out var memoryMb) && memoryMb > 0
            ? $"-Xms{memoryMb}M -Xmx{memoryMb}M"
            : string.Empty;
    }
}
