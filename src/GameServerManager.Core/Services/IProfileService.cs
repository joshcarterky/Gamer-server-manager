using GameServerManager.Core.Models;

namespace GameServerManager.Core.Services;

public interface IProfileService
{
    event Action? ProfilesChanged;

    IReadOnlyList<Profile> LoadedProfiles { get; }

    void LoadProfiles();
    void SaveProfile(Profile profile);
    void DeleteProfile(string profileId);
    Profile? GetProfile(string profileId);
    IReadOnlyList<Profile> GetProfilesByGameId(string gameId);
}
