using GameServerManager.Core.Models;

namespace GameServerManager.GameProviders;

public abstract class GameServerProviderBase : IGameServerProvider
{
    public abstract string GameId { get; }
    public abstract string GameName { get; }
    public virtual int? SteamAppId => null;
    public abstract string DefaultInstallFolder { get; }
    public abstract string ExecutableRelativePath { get; }
    public abstract IReadOnlyList<ServerPort> DefaultPorts { get; }
    public abstract string ConfigFolder { get; }
    public abstract string SavesFolder { get; }
    public abstract string LogsFolder { get; }
    public virtual GameServerFeatures SupportedFeatures => GameServerFeatures.None;
    public virtual bool SupportsMemoryLimit => SupportedFeatures.HasFlag(GameServerFeatures.MemoryLimit);
    public virtual IReadOnlyList<ServerSettingDefinition> SettingsDefinitions => Array.Empty<ServerSettingDefinition>();

    public virtual GameDefinition GetDefinition()
    {
        return new GameDefinition
        {
            GameId = GameId,
            GameName = GameName,
            SteamAppId = SteamAppId,
            DefaultInstallFolder = DefaultInstallFolder,
            ExecutableRelativePath = ExecutableRelativePath,
            DefaultPorts = DefaultPorts.ToList(),
            ConfigFolder = ConfigFolder,
            SavesFolder = SavesFolder,
            LogsFolder = LogsFolder,
            SupportedFeatures = SupportedFeatures,
            SupportsMemoryLimit = SupportsMemoryLimit,
            SettingsDefinitions = SettingsDefinitions.ToList()
        };
    }

    public virtual ServerLaunchCommand BuildStartCommand(ServerProfile profile)
    {
        var executablePath = !string.IsNullOrWhiteSpace(profile.ExecutablePath)
            ? profile.ExecutablePath
            : Path.Combine(profile.InstallPath, ExecutableRelativePath);

        return new ServerLaunchCommand
        {
            ExecutablePath = executablePath,
            WorkingDirectory = profile.InstallPath,
            Arguments = profile.LaunchArgs
        };
    }

    public override string ToString()
    {
        return GameName;
    }
}
