using GameServerManager.Core.Models;

namespace GameServerManager.GameProviders;

public interface IGameServerProvider
{
    string GameId { get; }
    string GameName { get; }
    int? SteamAppId { get; }
    string DefaultInstallFolder { get; }
    string ExecutableRelativePath { get; }
    IReadOnlyList<ServerPort> DefaultPorts { get; }
    string ConfigFolder { get; }
    string SavesFolder { get; }
    string LogsFolder { get; }
    GameServerFeatures SupportedFeatures { get; }
    bool SupportsMemoryLimit { get; }
    IReadOnlyList<ServerSettingDefinition> SettingsDefinitions { get; }

    GameDefinition GetDefinition();
    ServerLaunchCommand BuildStartCommand(ServerProfile profile);
}
