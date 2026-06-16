using GameServerManager.Core.Models;
using GameServerManager.Services.Repositories;

namespace GameServerManager.Services;

public class ServersJsonService
{
    private readonly IServerProfileRepository _repository;

    public ServersJsonService(AppDataPaths? paths = null)
        : this(new JsonServerProfileRepository(paths))
    {
    }

    public ServersJsonService(IServerProfileRepository repository)
    {
        _repository = repository;
    }

    public string ServersJsonPath => _repository.StoragePath;

    public async Task<IReadOnlyList<ServerProfile>> LoadServersAsync()
    {
        var profiles = await _repository.LoadAsync();
        return profiles
            .OrderBy(profile => profile.GameId)
            .ThenBy(profile => profile.ServerName)
            .ToList();
    }

    public Task SaveServersAsync(IEnumerable<ServerProfile> profiles)
    {
        return _repository.SaveAsync(profiles);
    }

    public async Task AddServerAsync(ServerProfile profile)
    {
        var profiles = (await _repository.LoadAsync()).ToList();
        EnsureProfileIdentity(profile);
        profiles.Add(profile);
        await _repository.SaveAsync(profiles);
    }

    public async Task UpdateServerAsync(ServerProfile profile)
    {
        var profiles = (await _repository.LoadAsync()).ToList();
        EnsureProfileIdentity(profile);
        var index = profiles.FindIndex(existing => existing.Id.Equals(profile.Id, StringComparison.OrdinalIgnoreCase));
        if (index < 0)
        {
            profiles.Add(profile);
        }
        else
        {
            profiles[index] = profile;
        }

        profile.ModifiedAt = DateTime.UtcNow;
        await _repository.SaveAsync(profiles);
    }

    public async Task DeleteServerAsync(string profileId)
    {
        var profiles = (await _repository.LoadAsync()).ToList();
        profiles.RemoveAll(profile => profile.Id.Equals(profileId, StringComparison.OrdinalIgnoreCase));
        await _repository.SaveAsync(profiles);
    }

    private static void EnsureProfileIdentity(ServerProfile profile)
    {
        if (string.IsNullOrWhiteSpace(profile.Id))
        {
            profile.Id = Guid.NewGuid().ToString();
        }

        if (profile.CreatedAt == default)
        {
            profile.CreatedAt = DateTime.UtcNow;
        }
    }
}
