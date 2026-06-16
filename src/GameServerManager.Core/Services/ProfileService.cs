using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using GameServerManager.Core.Models;

namespace GameServerManager.Core.Services;

/// <summary>
/// Service for managing user profiles with JSON repository persistence.
/// </summary>
public class ProfileService : IProfileService
{
    private readonly string _filePath;
    private ObservableCollection<Profile>? _profiles;

    /// <summary>
    /// Gets the observable collection of profiles.
    /// </summary>
    public ObservableCollection<Profile> Profiles { get; }
    public IReadOnlyList<Profile> LoadedProfiles => Profiles;

    /// <summary>
    /// Event raised when the profile collection changes.
    /// </summary>
    public event Action? ProfilesChanged;

    public ProfileService()
    {
        _filePath = Path.Combine(AppContext.BaseDirectory, "profiles.json");
        Profiles = new ObservableCollection<Profile>();
        LoadProfiles();
    }

    public void LoadProfiles()
    {
        if (File.Exists(_filePath))
        {
            var json = File.ReadAllText(_filePath);
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };
            _profiles = JsonSerializer.Deserialize<ObservableCollection<Profile>>(json, options);
            if (_profiles != null)
            {
                Profiles.Clear();
                foreach (var profile in _profiles)
                {
                    Profiles.Add(profile);
                }
            }
        }
    }

    public void SaveProfiles()
    {
        var json = JsonSerializer.Serialize(Profiles, new JsonSerializerOptions());
        File.WriteAllText(_filePath, json);
    }

    public ObservableCollection<Profile> GetAllProfiles()
    {
        return Profiles;
    }

    public Profile? GetProfileById(string id)
    {
        return Profiles.FirstOrDefault(p => p.Id == id);
    }

    public Profile? GetProfile(string profileId)
    {
        return GetProfileById(profileId);
    }

    public IReadOnlyList<Profile> GetProfilesByGameId(string gameId)
    {
        return Profiles
            .Where(profile => profile.GameId.Equals(gameId, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    public void AddProfile(Profile profile)
    {
        Profiles.Add(profile);
        ProfilesChanged?.Invoke();
        SaveProfiles();
    }

    public void SaveProfile(Profile profile)
    {
        var existing = GetProfileById(profile.Id);
        if (existing == null)
        {
            AddProfile(profile);
            return;
        }

        Replace(Profiles, existing, profile);
        ProfilesChanged?.Invoke();
        SaveProfiles();
    }

    public void EditProfile(Profile profile)
    {
        var existing = GetProfileById(profile.Id);
        if (existing != null)
        {
            Replace(Profiles, existing, profile);
            ProfilesChanged?.Invoke();
            SaveProfiles();
        }
    }

    public void DeleteProfile(string id)
    {
        var profile = GetProfileById(id);
        if (profile != null)
        {
            Profiles.Remove(profile);
            ProfilesChanged?.Invoke();
            SaveProfiles();
        }
    }

    private static void Replace<T>(ObservableCollection<T> collection, T oldItem, T newItem)
    {
        var index = collection.IndexOf(oldItem);
        if (index >= 0)
        {
            collection[index] = newItem;
        }
    }
}
