using GameServerManager.Core.Models;

namespace GameServerManager.Services.ArkSurvivalAscended;

public sealed class ArkClusterModSyncService
{
    private readonly ServersJsonService _serversJson;
    private readonly ArkClusterLogger _logger;

    public ArkClusterModSyncService(ServersJsonService serversJson, ArkClusterLogger? logger = null)
    {
        _serversJson = serversJson;
        _logger = logger ?? new ArkClusterLogger();
    }

    /// <summary>
    /// Pushes a master mod list to every map in the cluster, merging without removing existing per-map mods.
    /// </summary>
    public async Task<ArkModSyncResult> SyncModsToClusterAsync(
        string clusterId,
        IReadOnlyList<string> masterModIds,
        CancellationToken ct = default)
    {
        var profiles = (await _serversJson.LoadServersAsync()).ToList();
        var clusterProfiles = profiles.Where(p => IsClusterProfile(p, clusterId)).ToList();

        if (clusterProfiles.Count == 0)
        {
            return new ArkModSyncResult(0, 0,
                string.IsNullOrWhiteSpace(clusterId)
                    ? "No cluster maps found."
                    : $"No cluster maps found with ID '{clusterId}'.");
        }

        int updated = 0, skipped = 0;
        foreach (var profile in clusterProfiles)
        {
            var existing = GetModIds(profile);
            var toAdd = masterModIds
                .Where(id => !existing.Any(e => e.Equals(id, StringComparison.OrdinalIgnoreCase)))
                .ToList();

            if (toAdd.Count == 0) { skipped++; continue; }

            var merged = existing.Concat(toAdd)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            SetModIds(profile, merged);
            await _serversJson.UpdateServerAsync(profile);
            await _logger.LogModsAsync(
                $"Synced mods to '{profile.ProfileName}'",
                $"Added {toAdd.Count} mod(s): {string.Join(", ", toAdd)}", ct);
            updated++;
        }

        var message = updated > 0
            ? $"Synced {masterModIds.Count} mod(s) to {updated} map(s). {skipped} already up to date."
            : $"All {skipped} map(s) already have those mods.";
        await _logger.LogClusterAsync("Cluster mod sync complete.", message, ct);
        return new ArkModSyncResult(updated, skipped, message);
    }

    /// <summary>
    /// Removes a set of mod IDs from every map in the cluster.
    /// </summary>
    public async Task<ArkModSyncResult> RemoveModsFromClusterAsync(
        string clusterId,
        IReadOnlyList<string> modIds,
        CancellationToken ct = default)
    {
        var profiles = (await _serversJson.LoadServersAsync()).ToList();
        var clusterProfiles = profiles.Where(p => IsClusterProfile(p, clusterId)).ToList();

        if (clusterProfiles.Count == 0)
            return new ArkModSyncResult(0, 0, "No cluster maps found.");

        int updated = 0, skipped = 0;
        foreach (var profile in clusterProfiles)
        {
            var existing = GetModIds(profile);
            var filtered = existing
                .Where(id => !modIds.Any(m => m.Equals(id, StringComparison.OrdinalIgnoreCase)))
                .ToList();

            if (filtered.Count == existing.Count) { skipped++; continue; }

            SetModIds(profile, filtered);
            await _serversJson.UpdateServerAsync(profile);
            await _logger.LogModsAsync($"Removed mods from '{profile.ProfileName}'",
                $"Removed {existing.Count - filtered.Count} mod(s).", ct);
            updated++;
        }

        return new ArkModSyncResult(updated, skipped,
            $"Removed mods from {updated} map(s). {skipped} did not have those mods.");
    }

    /// <summary>
    /// Checks mod consistency across all maps in the cluster.
    /// </summary>
    public async Task<ArkModConsistencyReport> CheckConsistencyAsync(string clusterId)
    {
        var profiles = (await _serversJson.LoadServersAsync()).ToList();
        var clusterProfiles = profiles.Where(p => IsClusterProfile(p, clusterId)).ToList();

        if (clusterProfiles.Count == 0)
            return new ArkModConsistencyReport(new List<string>(), new Dictionary<string, IReadOnlyList<string>>());

        var mapMods = clusterProfiles.ToDictionary(
            p => p.ProfileName,
            p => (IReadOnlyList<string>)GetModIds(p));

        var allMods = mapMods.Values
            .SelectMany(m => m)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var issues = new List<string>();
        foreach (var modId in allMods)
        {
            var withMod = mapMods.Where(kvp => kvp.Value.Any(id => id.Equals(modId, StringComparison.OrdinalIgnoreCase)))
                .Select(kvp => kvp.Key).ToList();
            var without = mapMods.Keys.Except(withMod).ToList();
            if (without.Count > 0)
            {
                issues.Add($"Mod {modId} is missing from: {string.Join(", ", without)}.");
            }
        }

        return new ArkModConsistencyReport(issues, mapMods);
    }

    private static bool IsClusterProfile(ServerProfile p, string clusterId)
    {
        var isArk = p.GameId.Equals(ArkSurvivalAscendedServerProfile.GameId, StringComparison.OrdinalIgnoreCase)
                 || p.GameId.Equals(ArkSurvivalAscendedServerProfile.LegacyGameId, StringComparison.OrdinalIgnoreCase);
        if (!isArk) return false;

        if (!p.Settings.TryGetValue("ClusterEnabled", out var enabled)
            || !bool.TryParse(enabled, out var isEnabled) || !isEnabled)
            return false;

        if (string.IsNullOrWhiteSpace(clusterId)) return true;
        return p.Settings.TryGetValue("ClusterID", out var cid)
            && cid.Equals(clusterId, StringComparison.OrdinalIgnoreCase);
    }

    private static IReadOnlyList<string> GetModIds(ServerProfile profile)
    {
        if (profile.Settings.TryGetValue("ModIDs", out var value) && !string.IsNullOrWhiteSpace(value))
        {
            return value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        }

        var fromMods = profile.Mods.Where(m => m.IsEnabled).Select(m => m.Id)
            .Where(id => !string.IsNullOrWhiteSpace(id)).ToList();
        return fromMods;
    }

    private static void SetModIds(ServerProfile profile, IReadOnlyList<string> modIds)
    {
        profile.Settings["ModIDs"] = string.Join(',', modIds);

        var existingIds = profile.Mods.Select(m => m.Id)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var id in modIds.Where(id => !existingIds.Contains(id)))
        {
            profile.Mods.Add(new ServerMod { Id = id, Name = id, IsEnabled = true });
        }
    }
}

public sealed record ArkModSyncResult(int Updated, int Skipped, string Message)
{
    public bool HasChanges => Updated > 0;
}

public sealed record ArkModConsistencyReport(
    IReadOnlyList<string> Issues,
    IReadOnlyDictionary<string, IReadOnlyList<string>> MapMods)
{
    public bool IsConsistent => Issues.Count == 0;

    public int TotalUniqueMods => MapMods.Values
        .SelectMany(m => m)
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .Count();
}
