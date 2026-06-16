using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;

namespace GameServerManager.Services.CurseForge;

public sealed class CurseForgeService : IDisposable
{
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };
    private readonly HttpClient _http;
    private readonly int _gameId;

    public CurseForgeService(string apiKey, int gameId, int timeoutSeconds = 10)
    {
        _gameId = gameId;
        _http = new HttpClient
        {
            BaseAddress = new Uri("https://api.curseforge.com/v1/"),
            Timeout = TimeSpan.FromSeconds(Math.Max(5, timeoutSeconds))
        };
        _http.DefaultRequestHeaders.Add("x-api-key", apiKey);
        _http.DefaultRequestHeaders.Add("Accept", "application/json");
    }

    public async Task<CurseForgeLookupResult> LookupBySlugAsync(string slug, CancellationToken ct = default)
    {
        if (_gameId == 0)
            return Fail(CurseForgeLookupStatus.GameIdNotConfigured,
                "ARK ASA game ID is not configured. Set it in Settings → Integrations.");

        try
        {
            var url = $"mods/search?gameId={_gameId}&slug={Uri.EscapeDataString(slug)}";
            var response = await _http.GetFromJsonAsync<CfSearchResponse>(url, JsonOpts, ct);

            var mod = response?.Data
                .FirstOrDefault(m => m.Slug.Equals(slug, StringComparison.OrdinalIgnoreCase))
                ?? response?.Data.FirstOrDefault();

            if (mod is null)
                return Fail(CurseForgeLookupStatus.NotFound,
                    $"No mod found with slug '{slug}'. Verify the game ID in Settings → Integrations matches ARK: Survival Ascended.");

            return new CurseForgeLookupResult { Status = CurseForgeLookupStatus.Success, Mod = MapMod(mod) };
        }
        catch (HttpRequestException ex) when (
            ex.StatusCode is System.Net.HttpStatusCode.Unauthorized or System.Net.HttpStatusCode.Forbidden)
        {
            return Fail(CurseForgeLookupStatus.Unauthorized, "API key rejected — check your CurseForge API key in Settings → Integrations.");
        }
        catch (OperationCanceledException)
        {
            return Fail(CurseForgeLookupStatus.NetworkError, "Request timed out.");
        }
        catch (Exception ex)
        {
            return Fail(CurseForgeLookupStatus.NetworkError, $"Network error: {ex.Message}");
        }
    }

    public async Task<CurseForgePagedResult> SearchAsync(CurseForgeSearchRequest request, CancellationToken ct = default)
    {
        if (_gameId == 0)
            return new CurseForgePagedResult { ErrorMessage = "ARK ASA game ID is not configured. Set it in Settings → Integrations." };

        try
        {
            var sb = new System.Text.StringBuilder(
                $"mods/search?gameId={_gameId}&pageSize={request.PageSize}&index={request.Index}&sortField={request.SortField}&sortOrder={request.SortOrder}");

            if (!string.IsNullOrWhiteSpace(request.SearchFilter))
                sb.Append($"&searchFilter={Uri.EscapeDataString(request.SearchFilter)}");

            var response = await _http.GetFromJsonAsync<CfSearchResponse>(sb.ToString(), JsonOpts, ct);
            var mods = response?.Data.Select(MapMod).ToList() ?? new List<CurseForgeModInfo>();

            return new CurseForgePagedResult
            {
                Mods = mods,
                TotalCount = response?.Pagination?.TotalCount ?? mods.Count,
                Index = request.Index,
                PageSize = request.PageSize
            };
        }
        catch (HttpRequestException ex) when (
            ex.StatusCode is System.Net.HttpStatusCode.Unauthorized or System.Net.HttpStatusCode.Forbidden)
        {
            return new CurseForgePagedResult { ErrorMessage = "API key rejected — check your CurseForge API key in Settings → Integrations." };
        }
        catch (OperationCanceledException)
        {
            return new CurseForgePagedResult { ErrorMessage = "Request timed out." };
        }
        catch (Exception ex)
        {
            return new CurseForgePagedResult { ErrorMessage = $"Search failed: {ex.Message}" };
        }
    }

    public async Task<CurseForgeLookupResult> TestConnectionAsync(CancellationToken ct = default)
    {
        try
        {
            // Verify the API key is valid by listing games (lightweight endpoint)
            var response = await _http.GetAsync("games?index=0&pageSize=1", ct);
            if (response.IsSuccessStatusCode)
                return new CurseForgeLookupResult { Status = CurseForgeLookupStatus.Success };

            if (response.StatusCode is System.Net.HttpStatusCode.Unauthorized or System.Net.HttpStatusCode.Forbidden)
                return Fail(CurseForgeLookupStatus.Unauthorized, "API key rejected.");

            return Fail(CurseForgeLookupStatus.Unknown, $"Unexpected status: {(int)response.StatusCode}");
        }
        catch (HttpRequestException ex) when (
            ex.StatusCode is System.Net.HttpStatusCode.Unauthorized or System.Net.HttpStatusCode.Forbidden)
        {
            return Fail(CurseForgeLookupStatus.Unauthorized, "API key rejected.");
        }
        catch (OperationCanceledException)
        {
            return Fail(CurseForgeLookupStatus.NetworkError, "Connection timed out.");
        }
        catch (Exception ex)
        {
            return Fail(CurseForgeLookupStatus.NetworkError, ex.Message);
        }
    }

    private static CurseForgeLookupResult Fail(CurseForgeLookupStatus status, string message) =>
        new() { Status = status, ErrorMessage = message };

    private static CurseForgeModInfo MapMod(CfModData data) => new()
    {
        ProjectId = data.Id,
        Name = data.Name,
        Slug = data.Slug,
        Summary = data.Summary,
        Author = data.Authors.FirstOrDefault()?.Name ?? string.Empty,
        WebsiteUrl = data.Links?.WebsiteUrl ?? string.Empty,
        IconUrl = data.Logo?.ThumbnailUrl ?? string.Empty,
        DownloadCount = data.DownloadCount,
        DateModified = data.DateModified
    };

    public void Dispose() => _http.Dispose();
}
