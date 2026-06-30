using System.Diagnostics;
using System.IO;
using GameServerManager.Core.Models;
using GameServerManager.GameProviders;
using GameServerManager.Services.SevenDaysToDie;

namespace GameServerManager.Services;

public class ServerProcessService : IDisposable
{
    private readonly GameProviderRegistry _providers;
    private readonly AppDataPaths _paths;
    private readonly Dictionary<string, Process> _runningProcesses = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _intentionallyStopped = new(StringComparer.OrdinalIgnoreCase);

    public event EventHandler<ServerProfile>? ServerCrashed;

    public ServerProcessService(GameProviderRegistry providers, AppDataPaths paths)
    {
        _providers = providers;
        _paths = paths;
        _paths.EnsureCreated();
    }

    public async Task StartServerAsync(ServerProfile profile)
    {
        if (IsRunning(profile))
        {
            return;
        }

        var provider = _providers.GetProvider(profile.GameId);
        MemorySettingsPolicy.ApplyProfileMigration(profile, provider, out _);

        // For 7 Days to Die, sync profile settings to serverconfig.xml before launch
        // so that UI-saved settings are always applied to the actual game config.
        if (profile.GameId.Equals("seven_days_to_die", StringComparison.OrdinalIgnoreCase) &&
            !string.IsNullOrWhiteSpace(profile.InstallPath))
        {
            var configPath = Path.Combine(profile.InstallPath, "serverconfig.xml");
            var configService = new SevenDaysToDieConfigService();
            await SevenDaysToDieConfigService.EnsureConfigExistsAsync(configPath, profile.ServerName);
            await configService.SaveAsync(profile, configPath, createBackup: false);
        }

        var command = provider.BuildStartCommand(profile);
        if (!command.IsValid)
        {
            throw new InvalidOperationException("The provider generated an invalid start command.");
        }

        Directory.CreateDirectory(command.WorkingDirectory);
        Directory.CreateDirectory(_paths.ServerLogsDirectory);

        var logPath = Path.Combine(_paths.ServerLogsDirectory, $"{SafeName(profile.ProfileName)}.log");
        AppendLog(logPath, $"Starting {profile.ServerName}");
        AppendLog(logPath, MemorySettingsPolicy.GetStartLogMessage(profile, provider));
        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = command.ExecutablePath,
                WorkingDirectory = command.WorkingDirectory,
                Arguments = command.Arguments,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                RedirectStandardInput = true,
                CreateNoWindow = true
            },
            EnableRaisingEvents = true
        };

        process.OutputDataReceived += (_, e) => AppendLog(logPath, e.Data);
        process.ErrorDataReceived += (_, e) => AppendLog(logPath, e.Data == null ? null : $"[ERR] {e.Data}");
        process.Exited += (_, _) =>
        {
            _runningProcesses.Remove(profile.Id);
            var wasStopped = _intentionallyStopped.Remove(profile.Id);
            if (!wasStopped)
                ServerCrashed?.Invoke(this, profile);
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        _runningProcesses[profile.Id] = process;
        profile.Status = ServerStatus.Running;
        profile.LastStartedAt = DateTime.UtcNow;
        profile.LastStoppedAt = null;

        await Task.CompletedTask;
    }

    public async Task StopServerAsync(ServerProfile profile)
    {
        if (!_runningProcesses.TryGetValue(profile.Id, out var process))
        {
            return;
        }

        _intentionallyStopped.Add(profile.Id);

        if (!process.HasExited)
        {
            try
            {
                process.CloseMainWindow();
                if (!process.WaitForExit(5000))
                {
                    process.Kill(entireProcessTree: true);
                }
            }
            finally
            {
                _runningProcesses.Remove(profile.Id);
                process.Dispose();
            }
        }

        profile.Status = ServerStatus.Stopped;
        profile.LastStoppedAt = DateTime.UtcNow;
        await Task.CompletedTask;
    }

    public async Task RestartServerAsync(ServerProfile profile)
    {
        await StopServerAsync(profile);
        await StartServerAsync(profile);
    }

    public bool IsRunning(ServerProfile profile)
    {
        return _runningProcesses.TryGetValue(profile.Id, out var process) && !process.HasExited;
    }

    public ServerStatus GetStatus(ServerProfile profile)
    {
        return IsRunning(profile) ? ServerStatus.Running : ServerStatus.Stopped;
    }

    public bool TryGetProcess(ServerProfile profile, out Process? process)
    {
        if (_runningProcesses.TryGetValue(profile.Id, out var runningProcess) && !runningProcess.HasExited)
        {
            process = runningProcess;
            return true;
        }

        if (TryFindProcessByLaunchCommand(profile, out var discoveredProcess))
        {
            _runningProcesses[profile.Id] = discoveredProcess;
            process = discoveredProcess;
            return true;
        }

        process = null;
        return false;
    }

    private bool TryFindProcessByLaunchCommand(ServerProfile profile, out Process process)
    {
        process = null!;
        try
        {
            var provider = _providers.GetProvider(profile.GameId);
            var command = provider.BuildStartCommand(profile);
            if (!command.IsValid || string.IsNullOrWhiteSpace(command.ExecutablePath))
            {
                return false;
            }

            var expectedPath = Path.GetFullPath(command.ExecutablePath);
            var processName = Path.GetFileNameWithoutExtension(expectedPath);
            foreach (var candidate in Process.GetProcessesByName(processName))
            {
                try
                {
                    if (candidate.HasExited)
                    {
                        candidate.Dispose();
                        continue;
                    }

                    var actualPath = candidate.MainModule?.FileName;
                    if (!string.IsNullOrWhiteSpace(actualPath) &&
                        string.Equals(Path.GetFullPath(actualPath), expectedPath, StringComparison.OrdinalIgnoreCase))
                    {
                        candidate.EnableRaisingEvents = true;
                        candidate.Exited += (_, _) => _runningProcesses.Remove(profile.Id);
                        process = candidate;
                        return true;
                    }
                }
                catch
                {
                    candidate.Dispose();
                }
            }
        }
        catch
        {
            return false;
        }

        return false;
    }

    public string GetConsoleLogPath(ServerProfile profile)
    {
        return Path.Combine(_paths.ServerLogsDirectory, $"{SafeName(profile.ProfileName)}.log");
    }

    private static void AppendLog(string logPath, string? line)
    {
        if (line == null)
        {
            return;
        }

        File.AppendAllText(logPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {line}{Environment.NewLine}");
    }

    private static string SafeName(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        return new string(value.Select(ch => invalid.Contains(ch) ? '_' : ch).ToArray());
    }

    public void Dispose()
    {
        foreach (var process in _runningProcesses.Values)
        {
            process.Dispose();
        }

        _runningProcesses.Clear();
    }
}
