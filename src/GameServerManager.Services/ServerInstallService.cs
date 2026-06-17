using System.Collections.Concurrent;
using GameServerManager.Core.Models;
using GameServerManager.Core.Services;
using GameServerManager.GameProviders;

namespace GameServerManager.Services;

public sealed record ServerInstallProgress(
    string Stage,
    string StatusLine,
    bool IsIndeterminate = true,
    int Percent = 0);

public sealed record ServerInstallResult(
    bool Success,
    bool Cancelled,
    string Message,
    string LogPath,
    Exception? Error = null);

public sealed class ServerInstallService : IDisposable
{
    private readonly SteamCMDService _steamCmd;
    private readonly string _logsDirectory;
    private readonly ConcurrentDictionary<string, byte> _activeOperations = new();

    public ServerInstallService(AppDataPaths paths)
    {
        _steamCmd = new SteamCMDService();
        _logsDirectory = paths.LogsDirectory;
    }

    public bool IsOperationActive(string serverId) => _activeOperations.ContainsKey(serverId);

    public async Task<ServerInstallResult> InstallOrUpdateAsync(
        ServerProfile profile,
        IGameServerProvider provider,
        bool validate,
        IProgress<ServerInstallProgress>? progress,
        CancellationToken cancellationToken)
    {
        if (!_activeOperations.TryAdd(profile.Id, 0))
            return new ServerInstallResult(false, false, "An install or update is already running for this server.", string.Empty);

        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        var logFile = Path.Combine(_logsDirectory, $"steaminstall_{SafeFileName(profile.ProfileName)}_{timestamp}.txt");
        var log = new System.Text.StringBuilder();

        try
        {
            if (string.IsNullOrWhiteSpace(profile.InstallPath))
                return Fail("Install path is not configured for this server.", logFile, log);

            if (!provider.SupportedFeatures.HasFlag(GameServerFeatures.SteamCmdInstall))
                return Fail($"{provider.GameName} does not support SteamCMD installation.", logFile, log);

            if (provider.SteamAppId is null)
                return Fail($"{provider.GameName} has no Steam App ID configured.", logFile, log);

            Log(log, $"ServerId={profile.Id} Server={profile.ServerName} Provider={provider.GameName} SteamAppId={provider.SteamAppId}");
            Log(log, $"InstallPath={profile.InstallPath} Validate={validate}");

            if (!_steamCmd.IsInstalled)
            {
                progress?.Report(new ServerInstallProgress("Preparing", "Downloading SteamCMD..."));
                Log(log, "SteamCMD not found — downloading...");
                var installed = await _steamCmd.InstallSteamCMDAsync();
                if (!installed)
                    return Fail("Failed to download SteamCMD. Check your internet connection.", logFile, log);
                Log(log, "SteamCMD downloaded successfully");
            }

            Directory.CreateDirectory(profile.InstallPath);

            var validateFlag = validate ? " validate" : string.Empty;
            var arguments = $"+force_install_dir \"{profile.InstallPath}\" +login anonymous +app_update {provider.SteamAppId}{validateFlag} +quit";

            var exePath = Path.Combine(profile.InstallPath, provider.ExecutableRelativePath);
            var isUpdate = File.Exists(exePath);
            var mode = isUpdate ? "Update" : "Install";

            Log(log, $"Mode={mode}");
            progress?.Report(new ServerInstallProgress(mode, $"Starting {mode.ToLower()}..."));

            var result = await _steamCmd.RunInstallOrUpdateAsync(
                arguments,
                logFile,
                new Progress<string>(line =>
                {
                    Log(log, line);
                    progress?.Report(new ServerInstallProgress(mode, line));
                }),
                cancellationToken);

            if (result.Cancelled)
            {
                Log(log, "Operation cancelled by user");
                await FlushLog(logFile, log);
                return new ServerInstallResult(false, true, "Operation cancelled.", logFile);
            }

            Log(log, $"SteamCMD exit code: {result.ExitCode}");

            if (result.ExitCode != 0)
            {
                await FlushLog(logFile, log);
                return new ServerInstallResult(false, false, $"SteamCMD exited with code {result.ExitCode}. See the install log for details.", logFile);
            }

            progress?.Report(new ServerInstallProgress("Validating", "Checking installation..."));
            if (!File.Exists(exePath))
            {
                Log(log, $"Executable not found after install: {exePath}");
                await FlushLog(logFile, log);
                return new ServerInstallResult(false, false, $"Installation completed but the server executable was not found at the expected path.", logFile);
            }

            Log(log, "Operation completed successfully");
            await FlushLog(logFile, log);
            return new ServerInstallResult(true, false, $"{profile.ServerName} {(isUpdate ? "updated" : "installed")} successfully.", logFile);
        }
        catch (OperationCanceledException)
        {
            Log(log, "Operation cancelled");
            await FlushLog(logFile, log);
            return new ServerInstallResult(false, true, "Operation cancelled.", logFile);
        }
        catch (Exception ex)
        {
            Log(log, $"Unexpected error: {ex.GetType().Name}: {ex.Message}");
            Log(log, ex.StackTrace ?? string.Empty);
            await FlushLog(logFile, log);
            return new ServerInstallResult(false, false, $"Unexpected error: {ex.Message}", logFile, ex);
        }
        finally
        {
            _activeOperations.TryRemove(profile.Id, out _);
        }
    }

    private ServerInstallResult Fail(string message, string logFile, System.Text.StringBuilder log)
    {
        Log(log, $"Validation failed: {message}");
        _ = FlushLog(logFile, log);
        return new ServerInstallResult(false, false, message, logFile);
    }

    private static void Log(System.Text.StringBuilder sb, string line) =>
        sb.AppendLine($"[{DateTimeOffset.Now:HH:mm:ss.fff}] {line}");

    private static async Task FlushLog(string path, System.Text.StringBuilder content)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            await File.WriteAllTextAsync(path, content.ToString());
        }
        catch { }
    }

    private static string SafeFileName(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        return new string(value.Select(c => invalid.Contains(c) ? '_' : c).ToArray());
    }

    public void Dispose() => _steamCmd.Dispose();
}
