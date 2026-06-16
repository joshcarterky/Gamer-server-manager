using System.Text.Json.Serialization;

namespace GameServerManager.Services.CurseForge;

public sealed class CurseForgeModInfo
{
    public int ProjectId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public string Author { get; set; } = string.Empty;
    public string WebsiteUrl { get; set; } = string.Empty;
    public string IconUrl { get; set; } = string.Empty;
    public long DownloadCount { get; set; }
    public DateTime? DateModified { get; set; }
}

public enum CurseForgeLookupStatus
{
    Success,
    NotFound,
    ApiKeyMissing,
    Unauthorized,
    NetworkError,
    GameIdNotConfigured,
    Unknown
}

public sealed class CurseForgeLookupResult
{
    public CurseForgeLookupStatus Status { get; set; }
    public CurseForgeModInfo? Mod { get; set; }
    public string ErrorMessage { get; set; } = string.Empty;
}

// ── Public search models ─────────────────────────────────────

public sealed class CurseForgeSearchRequest
{
    public string SearchFilter { get; init; } = string.Empty;
    public int SortField { get; init; } = 2; // 2 = Popularity
    public string SortOrder { get; init; } = "desc";
    public int PageSize { get; init; } = 20;
    public int Index { get; init; } = 0;
}

public sealed class CurseForgePagedResult
{
    public List<CurseForgeModInfo> Mods { get; init; } = new();
    public int TotalCount { get; init; }
    public int Index { get; init; }
    public int PageSize { get; init; }
    public bool HasMore => Index + Mods.Count < TotalCount;
    public string ErrorMessage { get; init; } = string.Empty;
    public bool IsSuccess => string.IsNullOrEmpty(ErrorMessage);
}

// ── Internal API response shapes ─────────────────────────────

internal sealed class CfSearchResponse
{
    [JsonPropertyName("data")]
    public List<CfModData> Data { get; set; } = new();

    [JsonPropertyName("pagination")]
    public CfPagination? Pagination { get; set; }
}

internal sealed class CfSingleResponse
{
    [JsonPropertyName("data")]
    public CfModData? Data { get; set; }
}

internal sealed class CfModData
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("slug")]
    public string Slug { get; set; } = string.Empty;

    [JsonPropertyName("summary")]
    public string Summary { get; set; } = string.Empty;

    [JsonPropertyName("downloadCount")]
    public long DownloadCount { get; set; }

    [JsonPropertyName("dateModified")]
    public DateTime? DateModified { get; set; }

    [JsonPropertyName("links")]
    public CfLinks? Links { get; set; }

    [JsonPropertyName("authors")]
    public List<CfAuthor> Authors { get; set; } = new();

    [JsonPropertyName("logo")]
    public CfLogo? Logo { get; set; }
}

internal sealed class CfLinks
{
    [JsonPropertyName("websiteUrl")]
    public string WebsiteUrl { get; set; } = string.Empty;
}

internal sealed class CfAuthor
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
}

internal sealed class CfLogo
{
    [JsonPropertyName("thumbnailUrl")]
    public string ThumbnailUrl { get; set; } = string.Empty;
}

internal sealed class CfPagination
{
    [JsonPropertyName("index")]
    public int Index { get; set; }

    [JsonPropertyName("pageSize")]
    public int PageSize { get; set; }

    [JsonPropertyName("resultCount")]
    public int ResultCount { get; set; }

    [JsonPropertyName("totalCount")]
    public int TotalCount { get; set; }
}
