using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace GameServerManager.Services.Updates;

public sealed class GitHubReleaseService
{
    private readonly HttpClient _httpClient;
    private readonly UpdateLogger? _logger;

    public GitHubReleaseService(HttpClient? httpClient = null, UpdateLogger? logger = null)
    {
        _httpClient = httpClient ?? new HttpClient();
        _logger = logger;
        if (!_httpClient.DefaultRequestHeaders.UserAgent.Any())
        {
            _httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("NexusServerManager-Updater", AppVersion.Current));
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
        if (!TryBuildApiUrl(repositoryUrl, includePrerelease, out var apiUrl))
        {
            return UpdateCheckResult.Error(currentVersion, channel, "GitHub repository URL is not configured. Set RepositoryUrl before publishing releases.");
        }

        try
        {
            await LogAsync("GitHub update check started.", $"Current={currentVersion}; ApiUrl={apiUrl}; Channel={channel}", cancellationToken);
            using var response = await _httpClient.GetAsync(apiUrl, cancellationToken);
            await LogAsync("GitHub update check HTTP response.", $"Status={(int)response.StatusCode} {response.ReasonPhrase}", cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return UpdateCheckResult.Error(currentVersion, channel, MessageForHttpFailure(response));
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            var current = SemanticVersionInfo.Parse(currentVersion);
            var latest = includePrerelease
                ? await ReadLatestFromReleaseListAsync(stream, cancellationToken)
                : await ReadLatestStableReleaseAsync(stream, cancellationToken);

            if (latest is null || latest.Parsed is null)
            {
                await LogAsync("No matching GitHub release found.", $"Channel={channel}", cancellationToken);
                return UpdateCheckResult.Error(currentVersion, channel, includePrerelease ? "No beta or stable GitHub releases were found." : "No stable GitHub releases were found.");
            }

            await LogAsync("Latest GitHub release parsed.", $"Tag={latest.Release.TagName}; Parsed={latest.Parsed}", cancellationToken);
            var latestVersion = latest.Parsed;
            if (latestVersion.CompareTo(current) <= 0 || string.Equals(latestVersion.ToString(), skippedVersion, StringComparison.OrdinalIgnoreCase))
            {
                await LogAsync("Version comparison result.", $"No update. Current={current}; Latest={latestVersion}; Skipped={skippedVersion}", cancellationToken);
                return UpdateCheckResult.NoUpdate(currentVersion, channel);
            }

            var assets = latest.Release.Assets
                .Select(asset => new UpdateAsset(asset.Name, asset.BrowserDownloadUrl, asset.Size, asset.ContentType))
                .ToArray();

            var preferredAsset = assets.FirstOrDefault(asset => asset.Name.Contains("Setup", StringComparison.OrdinalIgnoreCase))
                ?? assets.FirstOrDefault(asset => asset.Name.Contains("Installer", StringComparison.OrdinalIgnoreCase))
                ?? assets.FirstOrDefault(asset => asset.Name.Contains("Portable", StringComparison.OrdinalIgnoreCase))
                ?? assets.FirstOrDefault(asset => asset.Name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase));
            await LogAsync(
                "Version comparison result.",
                $"UpdateAvailable=True; Current={current}; Latest={latestVersion}; PreferredAsset={preferredAsset?.Name ?? "none"}; AssetCount={assets.Length}",
                cancellationToken);

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
            await LogAsync("GitHub update check network error.", ex.ToString(), cancellationToken);
            return UpdateCheckResult.Error(currentVersion, channel, UpdateErrorMessages.For("NoInternet", ex.Message));
        }
        catch (JsonException ex)
        {
            await LogAsync("GitHub update check invalid JSON.", ex.ToString(), cancellationToken);
            return UpdateCheckResult.Error(currentVersion, channel, UpdateErrorMessages.For("InvalidMetadata", ex.Message));
        }
        catch (FormatException ex)
        {
            await LogAsync("GitHub update check version parse failed.", ex.ToString(), cancellationToken);
            return UpdateCheckResult.Error(currentVersion, channel, $"Version parse failed: {ex.Message}");
        }
        catch (Exception ex)
        {
            await LogAsync("GitHub update check failed.", ex.ToString(), cancellationToken);
            return UpdateCheckResult.Error(currentVersion, channel, UpdateErrorMessages.For("UpdateCheckFailed", ex.Message));
        }
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private static bool TryBuildApiUrl(string repositoryUrl, bool includePrerelease, out string apiUrl)
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

        apiUrl = includePrerelease
            ? $"https://api.github.com/repos/{parts[0]}/{parts[1]}/releases"
            : $"https://api.github.com/repos/{parts[0]}/{parts[1]}/releases/latest";
        return true;
    }

    private static SemanticVersionInfo? TryParseTag(string tagName) =>
        SemanticVersionInfo.TryParse(tagName, out var version) ? version : null;

    private static async Task<ParsedRelease?> ReadLatestStableReleaseAsync(Stream stream, CancellationToken cancellationToken)
    {
        var release = await JsonSerializer.DeserializeAsync<GitHubRelease>(stream, JsonOptions, cancellationToken);
        var parsed = release is null || release.Draft || release.Prerelease ? null : TryParseTag(release.TagName);
        return release is null || parsed is null ? null : new ParsedRelease(release, parsed);
    }

    private static async Task<ParsedRelease?> ReadLatestFromReleaseListAsync(Stream stream, CancellationToken cancellationToken)
    {
        var releases = await JsonSerializer.DeserializeAsync<List<GitHubRelease>>(stream, JsonOptions, cancellationToken)
            ?? new List<GitHubRelease>();

        return releases
            .Where(release => !release.Draft)
            .Select(release => new ParsedRelease(release, TryParseTag(release.TagName)))
            .Where(item => item.Parsed is not null)
            .OrderByDescending(item => item.Parsed)
            .FirstOrDefault();
    }

    private static string MessageForHttpFailure(HttpResponseMessage response)
    {
        return response.StatusCode switch
        {
            System.Net.HttpStatusCode.NotFound => "GitHub release not found. Check the repository name and make sure at least one release is published.",
            System.Net.HttpStatusCode.Forbidden when response.Headers.TryGetValues("X-RateLimit-Remaining", out var values) && values.Contains("0") =>
                "GitHub API rate limit reached. Try again later.",
            System.Net.HttpStatusCode.Forbidden => "GitHub rejected the update check. Try again later or verify repository access.",
            _ => UpdateErrorMessages.For("GitHubUnreachable", $"{(int)response.StatusCode} {response.ReasonPhrase}")
        };
    }

    private Task LogAsync(string message, string? detail, CancellationToken cancellationToken)
    {
        return _logger?.LogAsync(message, detail, cancellationToken) ?? Task.CompletedTask;
    }

    private sealed record ParsedRelease(GitHubRelease Release, SemanticVersionInfo? Parsed);

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
