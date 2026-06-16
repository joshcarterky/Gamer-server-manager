using System.Text;

namespace GameServerManager.Services.Configuration;

public sealed class IniDocument
{
    private readonly List<IniLine> _lines = new();

    private IniDocument()
    {
    }

    public static IniDocument Parse(string text)
    {
        var document = new IniDocument();
        var currentSection = string.Empty;
        using var reader = new StringReader(text);
        string? line;
        while ((line = reader.ReadLine()) != null)
        {
            var trimmed = line.Trim();
            if (trimmed.StartsWith("[", StringComparison.Ordinal) && trimmed.EndsWith("]", StringComparison.Ordinal))
            {
                currentSection = trimmed[1..^1];
                document._lines.Add(IniLine.SectionLine(line, currentSection));
                continue;
            }

            if (TryParseKeyValue(line, out var key, out var value))
            {
                document._lines.Add(IniLine.KeyValue(line, currentSection, key, value));
                continue;
            }

            document._lines.Add(IniLine.Raw(line, currentSection));
        }

        return document;
    }

    public static async Task<IniDocument> LoadAsync(string path)
    {
        return File.Exists(path)
            ? Parse(await File.ReadAllTextAsync(path))
            : new IniDocument();
    }

    public IReadOnlyList<string> GetValues(string section, string key)
    {
        return _lines
            .Where(line => line.Kind == IniLineKind.KeyValue &&
                           line.Section.Equals(section, StringComparison.OrdinalIgnoreCase) &&
                           line.Key.Equals(key, StringComparison.OrdinalIgnoreCase))
            .Select(line => line.Value)
            .ToArray();
    }

    public string? GetValue(string section, string key)
    {
        return GetValues(section, key).LastOrDefault();
    }

    public void SetValue(string section, string key, string value)
    {
        EnsureSection(section);
        var matches = _lines
            .Where(line => line.Kind == IniLineKind.KeyValue &&
                           line.Section.Equals(section, StringComparison.OrdinalIgnoreCase) &&
                           line.Key.Equals(key, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (matches.Count == 0)
        {
            InsertInSection(section, IniLine.KeyValue($"{key}={value}", section, key, value));
            return;
        }

        matches[0].Value = value;
        matches[0].OriginalText = $"{key}={value}";
        foreach (var duplicate in matches.Skip(1))
        {
            _lines.Remove(duplicate);
        }
    }

    public void SetRepeatedValues(string section, string key, IEnumerable<string> values)
    {
        EnsureSection(section);
        _lines.RemoveAll(line => line.Kind == IniLineKind.KeyValue &&
                                 line.Section.Equals(section, StringComparison.OrdinalIgnoreCase) &&
                                 line.Key.Equals(key, StringComparison.OrdinalIgnoreCase));

        foreach (var value in values)
        {
            var lineValue = value.Contains('=', StringComparison.Ordinal) && value.StartsWith(key, StringComparison.OrdinalIgnoreCase)
                ? value[(value.IndexOf('=') + 1)..]
                : value;
            InsertInSection(section, IniLine.KeyValue($"{key}={lineValue}", section, key, lineValue));
        }
    }

    public void AddRawLine(string section, string rawLine)
    {
        EnsureSection(section);
        InsertInSection(section, IniLine.Raw(rawLine, section));
    }

    public void EnsureSection(string section)
    {
        if (_lines.Any(line => line.Kind == IniLineKind.Section &&
                               line.Section.Equals(section, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        if (_lines.Count > 0 && !string.IsNullOrWhiteSpace(_lines[^1].OriginalText))
        {
            _lines.Add(IniLine.Raw(string.Empty, string.Empty));
        }

        _lines.Add(IniLine.SectionLine($"[{section}]", section));
    }

    public string Render()
    {
        var builder = new StringBuilder();
        foreach (var line in _lines)
        {
            builder.AppendLine(line.OriginalText);
        }

        return builder.ToString();
    }

    public string CreateDiff(IniDocument updated)
    {
        var before = Render().Replace("\r\n", "\n").Split('\n');
        var after = updated.Render().Replace("\r\n", "\n").Split('\n');
        var max = Math.Max(before.Length, after.Length);
        var builder = new StringBuilder();
        for (var i = 0; i < max; i++)
        {
            var left = i < before.Length ? before[i] : string.Empty;
            var right = i < after.Length ? after[i] : string.Empty;
            if (left.Equals(right, StringComparison.Ordinal))
            {
                continue;
            }

            if (!string.IsNullOrEmpty(left))
            {
                builder.AppendLine($"- {left}");
            }

            if (!string.IsNullOrEmpty(right))
            {
                builder.AppendLine($"+ {right}");
            }
        }

        return builder.ToString();
    }

    public async Task SaveAtomicAsync(string path, bool createBackup = true)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        if (createBackup && File.Exists(path))
        {
            var backupPath = $"{path}.{DateTime.UtcNow:yyyyMMddHHmmss}.bak";
            File.Copy(path, backupPath, overwrite: false);
        }

        var tempPath = $"{path}.{Guid.NewGuid():N}.tmp";
        await File.WriteAllTextAsync(tempPath, Render());
        if (File.Exists(path))
        {
            File.Replace(tempPath, path, null);
        }
        else
        {
            File.Move(tempPath, path);
        }
    }

    private void InsertInSection(string section, IniLine line)
    {
        var lastSectionLineIndex = _lines.FindLastIndex(existing =>
            existing.Section.Equals(section, StringComparison.OrdinalIgnoreCase));

        if (lastSectionLineIndex < 0)
        {
            _lines.Add(line);
            return;
        }

        var insertIndex = lastSectionLineIndex + 1;
        while (insertIndex < _lines.Count && _lines[insertIndex].Kind != IniLineKind.Section)
        {
            insertIndex++;
        }

        _lines.Insert(insertIndex, line);
    }

    private static bool TryParseKeyValue(string line, out string key, out string value)
    {
        key = string.Empty;
        value = string.Empty;
        var trimmed = line.Trim();
        if (trimmed.Length == 0 || trimmed.StartsWith(";", StringComparison.Ordinal) || trimmed.StartsWith("#", StringComparison.Ordinal))
        {
            return false;
        }

        var equalsIndex = line.IndexOf('=');
        if (equalsIndex <= 0)
        {
            return false;
        }

        key = line[..equalsIndex].Trim();
        value = line[(equalsIndex + 1)..].Trim();
        return key.Length > 0;
    }
}

internal enum IniLineKind
{
    Raw,
    Section,
    KeyValue
}

internal sealed class IniLine
{
    private IniLine(IniLineKind kind, string originalText, string section, string key, string value)
    {
        Kind = kind;
        OriginalText = originalText;
        Section = section;
        Key = key;
        Value = value;
    }

    public IniLineKind Kind { get; }
    public string OriginalText { get; set; }
    public string Section { get; }
    public string Key { get; }
    public string Value { get; set; }

    public static IniLine Raw(string text, string section)
    {
        return new IniLine(IniLineKind.Raw, text, section, string.Empty, string.Empty);
    }

    public static IniLine SectionLine(string text, string section)
    {
        return new IniLine(IniLineKind.Section, text, section, string.Empty, string.Empty);
    }

    public static IniLine KeyValue(string text, string section, string key, string value)
    {
        return new IniLine(IniLineKind.KeyValue, text, section, key, value);
    }
}
