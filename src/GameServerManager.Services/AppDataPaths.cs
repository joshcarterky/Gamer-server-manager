namespace GameServerManager.Services;

public class AppDataPaths
{
    public AppDataPaths(string? rootDirectory = null)
    {
        RootDirectory = rootDirectory ?? Path.Combine(Environment.CurrentDirectory, "Data");
        ServersJsonPath = Path.Combine(RootDirectory, "servers.json");
        ProfilesDirectory = Path.Combine(RootDirectory, "Profiles");
        LogsDirectory = Path.Combine(RootDirectory, "Logs");
        ServerLogsDirectory = Path.Combine(LogsDirectory, "Servers");
        SettingsDirectory = Path.Combine(RootDirectory, "Settings");
        BackupsDirectory = Path.Combine(RootDirectory, "Backups");
        ServersDirectory = Path.Combine(Environment.CurrentDirectory, "Servers");
    }

    public string RootDirectory { get; }
    public string ServersJsonPath { get; }
    public string ProfilesDirectory { get; }
    public string LogsDirectory { get; }
    public string ServerLogsDirectory { get; }
    public string SettingsDirectory { get; }
    public string BackupsDirectory { get; }
    public string ServersDirectory { get; }

    public void EnsureCreated()
    {
        Directory.CreateDirectory(RootDirectory);
        Directory.CreateDirectory(ProfilesDirectory);
        Directory.CreateDirectory(LogsDirectory);
        Directory.CreateDirectory(ServerLogsDirectory);
        Directory.CreateDirectory(SettingsDirectory);
        Directory.CreateDirectory(BackupsDirectory);
        Directory.CreateDirectory(ServersDirectory);

        foreach (var serverFolder in new[]
                 {
                     "ARK_Survival_Ascended",
                     "7_Days_To_Die",
                     "Palworld",
                     "Minecraft",
                     "Minecraft_Bedrock",
                     "Valheim",
                     "Rust"
                 })
        {
            Directory.CreateDirectory(Path.Combine(ServersDirectory, serverFolder));
        }
    }
}
