using System.Text.Json;
using GameServerManager.Core.Models;
using GameServerManager.GameProviders;

namespace GameServerManager.Services;

public class ServerProfileService
{
    private readonly AppDataPaths _paths;
    private readonly GameProviderRegistry _providers;
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public ServerProfileService(AppDataPaths paths, GameProviderRegistry providers)
    {
        _paths = paths;
        _providers = providers;
        _paths.EnsureCreated();
    }

    public async Task<IReadOnlyList<ServerProfile>> LoadAllProfilesAsync()
    {
        if (!Directory.Exists(_paths.ProfilesDirectory))
        {
            return Array.Empty<ServerProfile>();
        }

        var profiles = new List<ServerProfile>();
        foreach (var file in Directory.EnumerateFiles(_paths.ProfilesDirectory, "*.json", SearchOption.AllDirectories))
        {
            try
            {
                await using var stream = File.OpenRead(file);
                var profile = await JsonSerializer.DeserializeAsync<ServerProfile>(stream, _jsonOptions);
                if (profile != null)
                {
                    profiles.Add(profile);
                }
            }
            catch
            {
                // Invalid profile files should not block the whole Servers tab from loading.
            }
        }

        return profiles.OrderBy(p => p.GameId).ThenBy(p => p.ProfileName).ToList();
    }

    public async Task<IReadOnlyList<ServerProfile>> LoadProfilesByGameAsync(string gameId)
    {
        var profiles = await LoadAllProfilesAsync();
        return profiles.Where(p => p.GameId.Equals(gameId, StringComparison.OrdinalIgnoreCase)).ToList();
    }

    public async Task SaveProfileAsync(ServerProfile profile)
    {
        var validation = ValidateProfile(profile);
        if (!validation.IsValid)
        {
            throw new InvalidOperationException(string.Join(Environment.NewLine, validation.Errors));
        }

        profile.ModifiedAt = DateTime.UtcNow;
        if (string.IsNullOrWhiteSpace(profile.Id))
        {
            profile.Id = Guid.NewGuid().ToString();
        }

        var path = GetProfilePath(profile);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await using var stream = File.Create(path);
        await JsonSerializer.SerializeAsync(stream, profile, _jsonOptions);
    }

    public async Task DeleteProfileAsync(string profileId)
    {
        var profiles = await LoadAllProfilesAsync();
        var profile = profiles.FirstOrDefault(p => p.Id.Equals(profileId, StringComparison.OrdinalIgnoreCase));
        if (profile == null)
        {
            return;
        }

        var path = GetProfilePath(profile);
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    public string GetProfilePath(ServerProfile profile)
    {
        var gameFolder = SanitizePathSegment(profile.GameId);
        var profileFile = SanitizePathSegment(profile.ProfileName);
        return Path.Combine(_paths.ProfilesDirectory, gameFolder, $"{profileFile}.json");
    }

    public ProfileValidationResult ValidateProfile(ServerProfile profile)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(profile.GameId))
        {
            errors.Add("Game is required.");
        }
        else if (!_providers.TryGetProvider(profile.GameId, out _))
        {
            errors.Add($"Unsupported game ID '{profile.GameId}'.");
        }

        if (string.IsNullOrWhiteSpace(profile.ProfileName))
        {
            errors.Add("Profile name is required.");
        }

        if (string.IsNullOrWhiteSpace(profile.ServerName))
        {
            errors.Add("Server name is required.");
        }

        if (string.IsNullOrWhiteSpace(profile.InstallPath))
        {
            errors.Add("Install path is required.");
        }

        foreach (var port in profile.Ports)
        {
            if (port.Port < 1 || port.Port > 65535)
            {
                errors.Add($"{port.Name} port must be between 1 and 65535.");
            }
        }

        return new ProfileValidationResult(errors.Count == 0, errors);
    }

    private static string SanitizePathSegment(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var cleaned = new string(value.Select(ch => invalid.Contains(ch) ? '_' : ch).ToArray());
        return string.IsNullOrWhiteSpace(cleaned) ? "Unnamed" : cleaned;
    }
}

public record ProfileValidationResult(bool IsValid, IReadOnlyList<string> Errors);
