namespace GameServerManager.Core.Models;

/// <summary>
/// Fully resolved command line used to start a server process.
/// </summary>
public class ServerLaunchCommand
{
    public string ExecutablePath { get; set; } = string.Empty;
    public string WorkingDirectory { get; set; } = string.Empty;
    public string Arguments { get; set; } = string.Empty;

    public bool IsValid => !string.IsNullOrWhiteSpace(ExecutablePath)
        && !string.IsNullOrWhiteSpace(WorkingDirectory);
}
