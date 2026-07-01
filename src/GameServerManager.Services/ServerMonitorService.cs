using GameServerManager.Core.Models;

namespace GameServerManager.Services;

public sealed class ServerMonitorService
{
    private readonly ServerProcessService _processService;
    private readonly ServerQueryService _queryService;
    private readonly Dictionary<string, ServerStatus> _lastStatuses = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, CpuSample> _cpuSamples = new(StringComparer.OrdinalIgnoreCase);

    public ServerMonitorService(ServerProcessService processService, ServerQueryService queryService)
    {
        _processService = processService;
        _queryService = queryService;
    }

    public async Task<ServerMonitorSnapshot> GetSnapshotAsync(ServerProfile profile)
    {
        return await Task.Run(async () =>
        {
            var isRunning = _processService.TryGetProcess(profile, out var process);
            // If the OS process isn't actually running, the profile can't truthfully
            // be Running/Starting/Stopping/Restarting — collapse to Stopped so a
            // crash-on-boot (bad config, missing files, port conflict, etc.) or a
            // stop/start that threw/raced never leaves the card lying forever.
            // Stopped/Error/Unknown/Updating are left as-is (Updating tracks a
            // separate SteamCMD process, not this one).
            var status = isRunning
                ? ServerStatus.Running
                : profile.Status is ServerStatus.Stopping or ServerStatus.Starting
                    or ServerStatus.Restarting or ServerStatus.Running
                    ? ServerStatus.Stopped
                    : profile.Status;
            TrackStatusTransition(profile, status);

            var uptime = status == ServerStatus.Running && profile.LastStartedAt.HasValue
                ? DateTime.UtcNow - profile.LastStartedAt.Value
                : TimeSpan.Zero;
            var query = status == ServerStatus.Running
                ? await _queryService.QueryAsync(profile)
                : new ServerQueryResult(false, 0, profile.MaxPlayers, "Server is not running.");

            return new ServerMonitorSnapshot
            {
                ProfileId = profile.Id,
                Status = status,
                CpuPercent = isRunning && process != null ? GetCpuPercent(profile.Id, process) : 0,
                RamMb = isRunning && process != null ? GetRamMb(process) : 0,
                RamLimitMb = GetConfiguredRamLimit(profile),
                Uptime = uptime,
                PlayerCount = query.PlayerCount,
                LastStartedAt = profile.LastStartedAt,
                LastStoppedAt = profile.LastStoppedAt
            };
        });
    }

    private void TrackStatusTransition(ServerProfile profile, ServerStatus currentStatus)
    {
        _lastStatuses.TryGetValue(profile.Id, out var previousStatus);
        if (previousStatus == currentStatus)
        {
            return;
        }

        if (currentStatus == ServerStatus.Running && profile.LastStartedAt == null)
        {
            profile.LastStartedAt = DateTime.UtcNow;
        }

        if (previousStatus == ServerStatus.Running && currentStatus != ServerStatus.Running)
        {
            profile.LastStoppedAt = DateTime.UtcNow;
        }

        _lastStatuses[profile.Id] = currentStatus;
    }

    private int GetCpuPercent(string profileId, System.Diagnostics.Process process)
    {
        try
        {
            process.Refresh();
            var now = DateTime.UtcNow;
            var totalCpu = process.TotalProcessorTime;

            if (!_cpuSamples.TryGetValue(profileId, out var previous))
            {
                _cpuSamples[profileId] = new CpuSample(totalCpu, now);
                return 0;
            }

            var cpuDeltaMs = (totalCpu - previous.TotalProcessorTime).TotalMilliseconds;
            var elapsedMs = (now - previous.SampledAt).TotalMilliseconds;
            _cpuSamples[profileId] = new CpuSample(totalCpu, now);

            if (elapsedMs <= 0)
            {
                return 0;
            }

            var percent = cpuDeltaMs / (elapsedMs * Environment.ProcessorCount) * 100;
            return Math.Clamp((int)Math.Round(percent), 0, 100);
        }
        catch
        {
            return 0;
        }
    }

    private static int GetRamMb(System.Diagnostics.Process process)
    {
        try
        {
            process.Refresh();
            return (int)Math.Max(0, process.WorkingSet64 / 1024 / 1024);
        }
        catch
        {
            return 0;
        }
    }

    private static int? GetConfiguredRamLimit(ServerProfile profile)
    {
        var memoryMode = profile.Settings.TryGetValue("MemoryMode", out var mode) ? mode : "Auto";
        if (!memoryMode.Equals("Custom", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        if (profile.Settings.TryGetValue("CustomMemoryMb", out var customValue) && int.TryParse(customValue, out var customParsed))
        {
            return Math.Max(customParsed, 1);
        }

        return profile.Settings.TryGetValue("MemoryMb", out var legacyValue) && int.TryParse(legacyValue, out var legacyParsed)
            ? Math.Max(legacyParsed, 1)
            : null;
    }
}

internal sealed record CpuSample(TimeSpan TotalProcessorTime, DateTime SampledAt);

public sealed class ServerMonitorSnapshot
{
    public string ProfileId { get; init; } = string.Empty;
    public ServerStatus Status { get; init; }
    public int CpuPercent { get; init; }
    public int RamMb { get; init; }
    public int? RamLimitMb { get; init; }
    public TimeSpan Uptime { get; init; }
    public int PlayerCount { get; init; }
    public DateTime? LastStartedAt { get; init; }
    public DateTime? LastStoppedAt { get; init; }
}
