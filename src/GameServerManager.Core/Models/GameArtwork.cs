namespace GameServerManager.Core.Models;

/// <summary>
/// Resolves official Steam store key-art for a game's server tile.
///
/// The image is streamed from Steam's public CDN at runtime — nothing is
/// bundled in the repo, so there is no redistribution of copyrighted art.
/// When a game has no Steam art (Minecraft, generic) or the image can't be
/// fetched, callers fall back to the colored initials tile.
/// </summary>
public static class GameArtwork
{
    // gameId → Steam *store* (client) app id.
    //
    // Providers expose the dedicated-server app id, which usually carries no
    // capsule/library art, so we deliberately map to the client app id here.
    private static readonly Dictionary<string, int> StoreAppIds = new(StringComparer.OrdinalIgnoreCase)
    {
        ["ark-survival-ascended"] = 2399830,
        ["ark_survival_ascended"] = 2399830, // legacy id
        ["ark_survival_evolved"]  = 346110,
        ["palworld"]              = 1623730,
        ["valheim"]               = 892970,
        ["rust"]                  = 252490,
        ["seven_days_to_die"]     = 251570,
        ["conan_exiles"]          = 440900,
        ["project_zomboid"]       = 108600,
        ["satisfactory"]          = 526870,
        ["factorio"]              = 427520,
    };

    /// <summary>
    /// Portrait key-art URL for the game's tile, or <c>null</c> when the game
    /// has no known Steam art. Portrait (600×900) crops cleanly to a square tile.
    /// Returned as a string because WPF's ImageSourceConverter binds string URLs
    /// (not <see cref="Uri"/>) to <c>ImageSource</c>.
    /// </summary>
    public static string? GetTileImageUrl(string gameId)
    {
        if (string.IsNullOrWhiteSpace(gameId) || !StoreAppIds.TryGetValue(gameId, out var appId))
            return null;

        // ponytail: streamed from Steam's CDN; WinINET caches the HTTP GET on disk,
        // so repeat launches and brief offline use are served from cache.
        // Ceiling: the first-ever load needs network. Upgrade path: pre-download to
        // %LOCALAPPDATA%\Nexus Server Manager\Cache\GameArt\{appId}.jpg and bind the file.
        return $"https://cdn.cloudflare.steamstatic.com/steam/apps/{appId}/library_600x900.jpg";
    }
}
