using System.Text.Json;
using GameServerManager.Core.Models;

namespace GameServerManager.Services.Repositories;

public sealed class JsonServerProfileRepository : IServerProfileRepository
{
    private readonly AppDataPaths _paths;
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public JsonServerProfileRepository(AppDataPaths? paths = null)
    {
        _paths = paths ?? new AppDataPaths();
        _paths.EnsureCreated();
    }

    public string StoragePath => _paths.ServersJsonPath;

    public async Task<IReadOnlyList<ServerProfile>> LoadAsync()
    {
        await EnsureStorageExistsAsync();

        try
        {
            await using var stream = File.OpenRead(StoragePath);
            var profiles = await JsonSerializer.DeserializeAsync<List<ServerProfile>>(stream, _jsonOptions);
            return profiles ?? new List<ServerProfile>();
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException($"Could not read servers.json. Check the JSON format in {StoragePath}.", ex);
        }
    }

    public async Task SaveAsync(IEnumerable<ServerProfile> profiles)
    {
        Directory.CreateDirectory(_paths.RootDirectory);
        await using var stream = File.Create(StoragePath);
        await JsonSerializer.SerializeAsync(stream, profiles.ToList(), _jsonOptions);
    }

    private async Task EnsureStorageExistsAsync()
    {
        Directory.CreateDirectory(_paths.RootDirectory);
        if (File.Exists(StoragePath))
        {
            return;
        }

        await File.WriteAllTextAsync(StoragePath, "[]");
    }
}
