namespace GameServerManager.Services.CurseForge;

public sealed record ParsedCurseForgeUrl
{
    public bool IsValid { get; init; }
    public string GameSlug { get; init; } = string.Empty;
    public string ModSlug { get; init; } = string.Empty;
    public string CleanUrl { get; init; } = string.Empty;
    public bool IsArkAsa => GameSlug.Equals("ark-survival-ascended", StringComparison.OrdinalIgnoreCase);
}

public static class CurseForgeUrlParser
{
    public static ParsedCurseForgeUrl Parse(string input)
    {
        if (!Uri.TryCreate(input.Trim(), UriKind.Absolute, out var uri))
            return new ParsedCurseForgeUrl();

        if (!uri.Host.EndsWith("curseforge.com", StringComparison.OrdinalIgnoreCase))
            return new ParsedCurseForgeUrl();

        // Expected path: /game-slug/mods/mod-slug[/optional-segments]
        var segments = uri.AbsolutePath.Trim('/').Split('/');
        if (segments.Length < 3 || !segments[1].Equals("mods", StringComparison.OrdinalIgnoreCase))
            return new ParsedCurseForgeUrl();

        return new ParsedCurseForgeUrl
        {
            IsValid = true,
            GameSlug = segments[0],
            ModSlug = segments[2],
            CleanUrl = $"https://www.curseforge.com/{segments[0]}/mods/{segments[2]}"
        };
    }

    public static bool IsCurseForgeUrl(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return false;
        return Uri.TryCreate(input.Trim(), UriKind.Absolute, out var uri)
            && uri.Host.EndsWith("curseforge.com", StringComparison.OrdinalIgnoreCase);
    }

    public static string FormatSlugAsName(string slug)
    {
        if (string.IsNullOrWhiteSpace(slug)) return slug;
        return string.Join(' ', slug.Split('-').Select(part =>
            part.Length > 0 ? char.ToUpper(part[0]) + part[1..] : part));
    }
}
