using GameServerManager.Core.Models;

namespace GameServerManager.Services.Repositories;

public interface IServerProfileRepository
{
    string StoragePath { get; }

    Task<IReadOnlyList<ServerProfile>> LoadAsync();
    Task SaveAsync(IEnumerable<ServerProfile> profiles);
}
