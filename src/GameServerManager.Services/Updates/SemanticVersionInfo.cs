namespace GameServerManager.Services.Updates;

public sealed record SemanticVersionInfo(int Major, int Minor, int Patch, string? Prerelease = null)
    : IComparable<SemanticVersionInfo>
{
    public static SemanticVersionInfo Parse(string value)
    {
        if (!TryParse(value, out var version))
        {
            throw new FormatException($"Invalid semantic version: {value}");
        }

        return version;
    }

    public static bool TryParse(string? value, out SemanticVersionInfo version)
    {
        version = new SemanticVersionInfo(0, 0, 0);
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var normalized = value.Trim();
        if (normalized.StartsWith('v') || normalized.StartsWith('V'))
        {
            normalized = normalized[1..];
        }

        var metadataIndex = normalized.IndexOf('+', StringComparison.Ordinal);
        if (metadataIndex >= 0)
        {
            normalized = normalized[..metadataIndex];
        }

        string? prerelease = null;
        var prereleaseIndex = normalized.IndexOf('-', StringComparison.Ordinal);
        if (prereleaseIndex >= 0)
        {
            prerelease = normalized[(prereleaseIndex + 1)..];
            normalized = normalized[..prereleaseIndex];
        }

        var parts = normalized.Split('.');
        if (parts.Length < 2 || parts.Length > 3)
        {
            return false;
        }

        var patch = 0;
        if (!int.TryParse(parts[0], out var major)
            || !int.TryParse(parts[1], out var minor)
            || (parts.Length == 3 && !int.TryParse(parts[2], out patch)))
        {
            return false;
        }

        version = new SemanticVersionInfo(major, minor, patch, prerelease);
        return true;
    }

    public bool IsPrerelease => !string.IsNullOrWhiteSpace(Prerelease);

    public int CompareTo(SemanticVersionInfo? other)
    {
        if (other is null)
        {
            return 1;
        }

        var major = Major.CompareTo(other.Major);
        if (major != 0) return major;
        var minor = Minor.CompareTo(other.Minor);
        if (minor != 0) return minor;
        var patch = Patch.CompareTo(other.Patch);
        if (patch != 0) return patch;

        if (IsPrerelease == other.IsPrerelease) return string.Compare(Prerelease, other.Prerelease, StringComparison.OrdinalIgnoreCase);
        return IsPrerelease ? -1 : 1;
    }

    public string GetUpdateTypeComparedTo(SemanticVersionInfo current)
    {
        if (Major > current.Major) return "Major";
        if (Minor > current.Minor) return "Minor";
        if (Patch > current.Patch) return "Patch";
        return "None";
    }

    public override string ToString() => $"v{Major}.{Minor}.{Patch}{(IsPrerelease ? $"-{Prerelease}" : string.Empty)}";
}
