namespace GameServerManager.Services.Updates;

public sealed class UpdateLogger
{
    private readonly AppDataPaths _paths;

    public UpdateLogger(AppDataPaths? paths = null)
    {
        _paths = paths ?? new AppDataPaths();
        _paths.EnsureCreated();
    }

    public string LogPath => Path.Combine(_paths.LogsDirectory, "updater.log");

    public async Task LogAsync(string message, string? detail = null, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(_paths.LogsDirectory);
        var line = $"[{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss zzz}] {message}";
        if (!string.IsNullOrWhiteSpace(detail))
        {
            line += $" | {detail}";
        }

        await File.AppendAllTextAsync(LogPath, line + Environment.NewLine, cancellationToken);
    }
}
