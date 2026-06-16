namespace GameServerManager.Services.ArkSurvivalAscended;

public sealed class ArkClusterLogger
{
    private readonly string _clusterLogPath;
    private readonly string _modsLogPath;
    private readonly SemaphoreSlim _lock = new(1, 1);

    public ArkClusterLogger(AppDataPaths? paths = null)
    {
        var p = paths ?? new AppDataPaths();
        _clusterLogPath = Path.Combine(p.LogsDirectory, "ark-cluster.log");
        _modsLogPath = Path.Combine(p.LogsDirectory, "ark-mods.log");
    }

    public string ClusterLogPath => _clusterLogPath;
    public string ModsLogPath => _modsLogPath;

    public async Task LogClusterAsync(string action, string detail = "", CancellationToken ct = default)
        => await WriteAsync(_clusterLogPath, action, detail, ct);

    public async Task LogModsAsync(string action, string detail = "", CancellationToken ct = default)
        => await WriteAsync(_modsLogPath, action, detail, ct);

    private async Task WriteAsync(string path, string action, string detail, CancellationToken ct)
    {
        await _lock.WaitAsync(ct);
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            var line = $"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}Z] {action}";
            if (!string.IsNullOrWhiteSpace(detail))
                line += $" | {detail}";
            await File.AppendAllTextAsync(path, line + Environment.NewLine, ct);
        }
        catch { }
        finally { _lock.Release(); }
    }
}
