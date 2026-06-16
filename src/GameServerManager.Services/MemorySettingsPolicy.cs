using GameServerManager.Core.Models;
using GameServerManager.GameProviders;

namespace GameServerManager.Services;

public static class MemorySettingsPolicy
{
    public const string MemoryModeKey = "MemoryMode";
    public const string CustomMemoryMbKey = "CustomMemoryMb";
    public const string LegacyMemoryMbKey = "MemoryMb";
    public const string LegacyRamLimitMbKey = "ramLimitMb";

    public static bool ApplyProfileMigration(ServerProfile profile, IGameServerProvider provider, out string message)
    {
        message = string.Empty;
        var changed = false;

        if (!provider.SupportsMemoryLimit)
        {
            changed |= profile.Settings.Remove(LegacyRamLimitMbKey);
            changed |= profile.Settings.Remove(LegacyMemoryMbKey);
            changed |= profile.Settings.Remove(CustomMemoryMbKey);
            changed |= profile.Settings.Remove(MemoryModeKey);
            if (changed)
            {
                profile.ModifiedAt = DateTime.UtcNow;
                message = $"{profile.ServerName} uses {provider.GameName}, which does not support app-level memory allocation. Memory limit settings were removed and memory mode is Game Default.";
            }

            return changed;
        }

        if (!profile.Settings.ContainsKey(MemoryModeKey))
        {
            profile.Settings[MemoryModeKey] = "Auto";
            profile.ModifiedAt = DateTime.UtcNow;
            message = $"{profile.ServerName} memory mode set to Game Default / Auto.";
            return true;
        }

        return false;
    }

    public static string GetStartLogMessage(ServerProfile profile, IGameServerProvider provider)
    {
        if (!provider.SupportsMemoryLimit)
        {
            return $"Memory Mode: Game Default. No memory limit applied because {provider.GameName} does not support app-level memory allocation.";
        }

        var mode = profile.Settings.TryGetValue(MemoryModeKey, out var savedMode) ? savedMode : "Auto";
        if (!mode.Equals("Custom", StringComparison.OrdinalIgnoreCase))
        {
            return "Memory Mode: Game Default. No memory limit arguments applied.";
        }

        var memoryMb = profile.Settings.TryGetValue(CustomMemoryMbKey, out var customMemory)
            ? customMemory
            : profile.Settings.TryGetValue(LegacyMemoryMbKey, out var legacyMemory)
                ? legacyMemory
                : string.Empty;

        return int.TryParse(memoryMb, out var parsed) && parsed > 0
            ? $"Memory Mode: Custom. Using memory limit: {parsed} MB."
            : "Memory Mode: Custom requested, but no valid memory value was configured. No memory limit arguments applied.";
    }
}
