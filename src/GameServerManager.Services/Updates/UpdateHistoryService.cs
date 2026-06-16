using System.Text.Json;

namespace GameServerManager.Services.Updates;

public sealed class UpdateHistoryService
{
    private readonly AppDataPaths _paths;
    private readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };

    public UpdateHistoryService(AppDataPaths? paths = null)
    {
        _paths = paths ?? new AppDataPaths();
        _paths.EnsureCreated();
    }

    public string HistoryPath => Path.Combine(_paths.SettingsDirectory, "update-history.json");

    public async Task<IReadOnlyList<UpdateHistoryEntry>> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(HistoryPath))
        {
            return Array.Empty<UpdateHistoryEntry>();
        }

        await using var stream = File.OpenRead(HistoryPath);
        return await JsonSerializer.DeserializeAsync<List<UpdateHistoryEntry>>(stream, _jsonOptions, cancellationToken)
            ?? new List<UpdateHistoryEntry>();
    }

    public async Task AddAsync(UpdateHistoryEntry entry, CancellationToken cancellationToken = default)
    {
        var entries = (await LoadAsync(cancellationToken)).ToList();
        entries.Insert(0, entry);
        Directory.CreateDirectory(_paths.SettingsDirectory);
        await using var stream = File.Create(HistoryPath);
        await JsonSerializer.SerializeAsync(stream, entries.Take(50).ToArray(), _jsonOptions, cancellationToken);
    }
}
