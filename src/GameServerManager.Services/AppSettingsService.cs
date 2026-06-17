using System.Text.Json;
using GameServerManager.Core.Models;

namespace GameServerManager.Services;

public class AppSettingsService
{
    private readonly AppDataPaths _paths;
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public AppSettingsService(AppDataPaths? paths = null)
    {
        _paths = paths ?? new AppDataPaths();
        _paths.EnsureCreated();
    }

    public string SettingsPath => Path.Combine(_paths.SettingsDirectory, "appsettings.json");
    public string ImportPath => Path.Combine(_paths.SettingsDirectory, "import-settings.json");
    public string ExportDirectory => Path.Combine(_paths.SettingsDirectory, "Exports");

    public async Task<AppSettings> LoadAsync()
    {
        if (!File.Exists(SettingsPath))
        {
            var defaults = NormalizeVersion(new AppSettings());
            await SaveAsync(defaults);
            return defaults;
        }

        AppSettings? settings;
        await using (var stream = File.OpenRead(SettingsPath))
        {
            settings = await JsonSerializer.DeserializeAsync<AppSettings>(stream, _jsonOptions);
        }

        settings ??= new AppSettings();
        var previousVersion = settings.Version;
        NormalizeVersion(settings);

        if (!string.Equals(previousVersion, settings.Version, StringComparison.OrdinalIgnoreCase))
        {
            await SaveAsync(settings);
        }

        return settings;
    }

    public async Task SaveAsync(AppSettings settings)
    {
        Directory.CreateDirectory(_paths.SettingsDirectory);
        NormalizeVersion(settings);
        settings.LastModifiedUtc = DateTime.UtcNow;

        await using var stream = File.Create(SettingsPath);
        await JsonSerializer.SerializeAsync(stream, settings, _jsonOptions);
    }

    public async Task<AppSettings> ResetAsync()
    {
        var defaults = NormalizeVersion(new AppSettings());
        await SaveAsync(defaults);
        return defaults;
    }

    public async Task<string> ExportAsync(AppSettings settings)
    {
        Directory.CreateDirectory(ExportDirectory);
        var fileName = $"appsettings-{DateTime.Now:yyyyMMdd-HHmmss}.json";
        var exportPath = Path.Combine(ExportDirectory, fileName);

        await using var stream = File.Create(exportPath);
        await JsonSerializer.SerializeAsync(stream, settings, _jsonOptions);
        return exportPath;
    }

    public async Task<AppSettings> ImportAsync()
    {
        if (!File.Exists(ImportPath))
        {
            throw new FileNotFoundException("No settings import file was found.", ImportPath);
        }

        await using var stream = File.OpenRead(ImportPath);
        var settings = await JsonSerializer.DeserializeAsync<AppSettings>(stream, _jsonOptions)
            ?? throw new InvalidOperationException("The settings import file is empty or invalid.");

        NormalizeVersion(settings);
        await SaveAsync(settings);
        return settings;
    }

    private static AppSettings NormalizeVersion(AppSettings settings)
    {
        settings.Version = AppVersion.Current;
        return settings;
    }
}
