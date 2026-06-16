using System.Text.Json;
using GameServerManager.Core.Models;

namespace GameServerManager.Services.Updates;

public sealed class SafeUpdateService
{
    private readonly AppDataPaths _paths;
    private readonly AppSettingsService _settingsService;

    public SafeUpdateService(AppDataPaths? paths = null, AppSettingsService? settingsService = null)
    {
        _paths = paths ?? new AppDataPaths();
        _settingsService = settingsService ?? new AppSettingsService(_paths);
    }

    public bool IsPortable => _paths.IsPortable;
    public string InstallMode => _paths.IsPortable ? "Portable" : "Installer";

    public async Task<string> BackupSettingsAsync(AppSettings settings, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(_paths.UpdateBackupsDirectory);
        var backupPath = Path.Combine(_paths.UpdateBackupsDirectory, $"appsettings-before-update-{DateTime.Now:yyyyMMdd-HHmmss}.json");
        await using var stream = File.Create(backupPath);
        await JsonSerializer.SerializeAsync(stream, settings, new JsonSerializerOptions { WriteIndented = true }, cancellationToken);
        return backupPath;
    }

    public UpdateInstallReadiness GetInstallReadiness()
    {
        if (_paths.IsPortable)
        {
            return new UpdateInstallReadiness(
                false,
                "Portable builds can check for updates and open GitHub Releases. Install-in-place is disabled until a portable package replacement flow is added.");
        }

        return new UpdateInstallReadiness(true, "Ready to download and install with Velopack. Settings are backed up before install.");
    }

    public Task SaveStateBeforeInstallAsync(AppSettings settings, CancellationToken cancellationToken = default)
    {
        settings.LastUpdateCheckUtc = DateTime.UtcNow;
        return _settingsService.SaveAsync(settings);
    }
}
