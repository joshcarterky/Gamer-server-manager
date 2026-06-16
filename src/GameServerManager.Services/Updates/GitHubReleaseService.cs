using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace GameServerManager.Services.Updates;

public sealed class GitHubReleaseService
{
    private readonly HttpClient _httpClient;

    public GitHubReleaseService(HttpClient? httpClient = null)
    {
        _httpClient = httpClient ?? new HttpClient();
        if (!_httpClient.DefaultRequestHeaders.UserAgent.Any())
        {
            _httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("NexusServerManager", AppVersion.Current));
        }
    }

    public async Task<UpdateCheckResult> CheckLatestAsync(
        string repositoryUrl,
        string currentVersion,
        bool includePrerelease,
        string skippedVersion,
        CancellationToken cancellationToken = default)
    {
        var channel = includePrerelease ? "Beta" : "Stable";
        if (!TryBuildApiUrl(repositoryUrl, out var apiUrl))
        {
            return UpdateCheckResult.Error(currentVersion, channel, "GitHub repository URL is not configured. Set RepositoryUrl before publishing releases.");
        }

        try
        {
            using var response = await _httpClient.GetAsync(apiUrl, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return UpdateCheckResult.Error(currentVersion, channel, UpdateErrorMessages.For("GitHubUnreachable", response.ReasonPhrase));
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            var releases = await JsonSerializer.DeserializeAsync<List<GitHubRelease>>(stream, JsonOptions, cancellationToken)
                ?? new List<GitHubRelease>();

            var current = SemanticVersionInfo.Parse(currentVersion);
            var latest = releases
                .Where(release => !release.Draft)
                .Where(release => includePrerelease || !release.Prerelease)
                .Select(release => new { Release = release, Parsed = TryParseTag(release.TagName) })
                .Where(item => item.Parsed is not null)
                .OrderByDescending(item => item.Parsed)
                .FirstOrDefault();

            if (latest is null || latest.Parsed is null)
            {
                return UpdateCheckResult.Error(currentVersion, channel, includePrerelease ? "No beta or stable GitHub releases were found." : "No stable GitHub releases were found.");
            }

            var latestVersion = latest.Parsed;
            if (latestVersion.CompareTo(current) <= 0 || string.Equals(latestVersion.ToString(), skippedVersion, StringComparison.OrdinalIgnoreCase))
            {
                return UpdateCheckResult.NoUpdate(currentVersion, channel);
            }

            var assets = latest.Release.Assets
                .Select(asset => new UpdateAsset(asset.Name, asset.BrowserDownloadUrl, asset.Size, asset.ContentType))
                .ToArray();

            var preferredAsset = assets.FirstOrDefault(asset =>
                asset.Name.EndsWith(".nupkg", StringComparison.OrdinalIgnoreCase)
                || asset.Name.Contains("Setup", StringComparison.OrdinalIgnoreCase)
                || asset.Name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase));

            return new UpdateCheckResult(
                true,
                currentVersion,
                latestVersion.ToString(),
                channel,
                latestVersion.GetUpdateTypeComparedTo(current),
                latest.Release.Name,
                latest.Release.Body,
                latest.Release.PublishedAt,
                latest.Release.HtmlUrl,
                preferredAsset?.SizeBytes,
                assets,
                null);
        }
        catch (HttpRequestException ex)
        {
            return UpdateCheckResult.Error(currentVersion, channel, UpdateErrorMessages.For("NoInternet", ex.Message));
        }
        catch (JsonException ex)
        {
            return UpdateCheckResult.Error(currentVersion, channel, UpdateErrorMessages.For("InvalidMetadata", ex.Message));
        }
        catch (Exception ex)
        {
            return UpdateCheckResult.Error(currentVersion, channel, UpdateErrorMessages.For("UpdateCheckFailed", ex.Message));
        }
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private static bool TryBuildApiUrl(string repositoryUrl, out string apiUrl)
    {
        apiUrl = string.Empty;
        if (!Uri.TryCreate(repositoryUrl, UriKind.Absolute, out var uri))
        {
            return false;
        }

        var parts = uri.AbsolutePath.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2)
        {
            return false;
        }

        apiUrl = $"https://api.github.com/repos/{parts[0]}/{parts[1]}/releases";
        return true;
    }

    private static SemanticVersionInfo? TryParseTag(string tagName) =>
        SemanticVersionInfo.TryParse(tagName, out var version) ? version : null;

    private sealed class GitHubRelease
    {
        [JsonPropertyName("tag_name")]
        public string TagName { get; set; } = string.Empty;

        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("body")]
        public string? Body { get; set; }

        [JsonPropertyName("html_url")]
        public string? HtmlUrl { get; set; }

        [JsonPropertyName("published_at")]
        public DateTimeOffset? PublishedAt { get; set; }

        [JsonPropertyName("draft")]
        public bool Draft { get; set; }

        [JsonPropertyName("prerelease")]
        public bool Prerelease { get; set; }

        [JsonPropertyName("assets")]
        public List<GitHubAsset> Assets { get; set; } = new();
    }

    private sealed class GitHubAsset
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("browser_download_url")]
        public string BrowserDownloadUrl { get; set; } = string.Empty;

        [JsonPropertyName("size")]
        public long Size { get; set; }

        [JsonPropertyName("content_type")]
        public string? ContentType { get; set; }
    }
}
