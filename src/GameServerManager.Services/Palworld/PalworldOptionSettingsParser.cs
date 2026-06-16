using System.Text;
using GameServerManager.Core.Models;

namespace GameServerManager.Services.Palworld;

// Parses and writes the Palworld PalWorldSettings.ini format.
//
// The active config lives at:
//   {InstallPath}/Pal/Saved/Config/WindowsServer/PalWorldSettings.ini
//
// The file format is NOT standard key=value INI. Almost every setting lives
// inside a single OptionSettings tuple on one line:
//
//   [/Script/Pal.PalGameWorldSettings]
//   OptionSettings=(Difficulty=0.000000,DayTimeSpeedRate=1.000000,ServerName="My Server",...)
//
// Values can be:
//   numbers       → DayTimeSpeedRate=1.000000
//   booleans      → bEnableInvaderEnemy=True
//   quoted strings → ServerName="My Server"
//   enum names    → DeathPenalty=All
//   paren arrays  → CrossplayPlatforms=("Steam","Xbox")
//
// The parser preserves every unknown key so future Palworld 1.0 settings
// are never silently dropped.

public sealed class PalworldConfigDocument
{
    private readonly List<string> _lines;
    private int _optionSettingsLineIndex = -1;

    // Ordered list of all entries (preserves original file order).
    // RawValue is exactly what appeared after the '=' in the tuple:
    //   ServerName → "My Server"   (WITH quotes)
    //   DayTimeSpeedRate → 1.000000
    //   CrossplayPlatforms → ("Steam","Xbox")
    private readonly List<(string Key, string RawValue)> _entries = new();
    private readonly Dictionary<string, int> _entryIndex = new(StringComparer.OrdinalIgnoreCase);

    private PalworldConfigDocument(List<string> lines)
    {
        _lines = lines;
    }

    // ── Parse ─────────────────────────────────────────────────────────────────

    public static PalworldConfigDocument Parse(string text)
    {
        var lines = new List<string>();
        using var reader = new StringReader(text);
        string? line;
        while ((line = reader.ReadLine()) != null)
            lines.Add(line);

        var doc = new PalworldConfigDocument(lines);
        doc.FindAndParseOptionSettings();
        return doc;
    }

    public static async Task<PalworldConfigDocument> LoadAsync(string path)
    {
        return File.Exists(path)
            ? Parse(await File.ReadAllTextAsync(path, Encoding.UTF8))
            : CreateEmpty();
    }

    public static PalworldConfigDocument CreateEmpty()
    {
        var doc = new PalworldConfigDocument(new List<string>
        {
            $"[{PalworldServerProfile.PalGameWorldSettingsSection}]",
            "OptionSettings=()"
        });
        doc._optionSettingsLineIndex = 1;
        return doc;
    }

    private void FindAndParseOptionSettings()
    {
        for (var i = 0; i < _lines.Count; i++)
        {
            var trimmed = _lines[i].TrimStart();
            if (!trimmed.StartsWith("OptionSettings=", StringComparison.OrdinalIgnoreCase))
                continue;

            _optionSettingsLineIndex = i;

            var eqPos = trimmed.IndexOf('=');
            if (eqPos < 0) break;

            var rest = trimmed[(eqPos + 1)..].TrimStart();
            if (!rest.StartsWith('(') || !rest.EndsWith(')'))
                break;

            // Strip outer parens
            var inner = rest[1..^1];
            ParseTupleContent(inner);
            break;
        }
    }

    // Splits "Key=Value,Key=Value,..." at commas that are not inside
    // nested parens or quoted strings.
    private void ParseTupleContent(string inner)
    {
        var pairs = SplitTopLevel(inner, ',');
        foreach (var pair in pairs)
        {
            var eqPos = pair.IndexOf('=');
            if (eqPos <= 0) continue;

            var key = pair[..eqPos].Trim();
            var rawValue = pair[(eqPos + 1)..];   // do NOT trim — quotes are significant

            if (!_entryIndex.ContainsKey(key))
            {
                _entryIndex[key] = _entries.Count;
                _entries.Add((key, rawValue));
            }
        }
    }

    // ── Getters ──────────────────────────────────────────────────────────────

    public bool HasKey(string key) => _entryIndex.ContainsKey(key);

    // Returns the raw value exactly as stored in the tuple (includes quotes for strings).
    public string GetRaw(string key)
        => _entryIndex.TryGetValue(key, out var idx) ? _entries[idx].RawValue : string.Empty;

    // Returns the string value with surrounding quotes stripped.
    // e.g.  "My Server" → My Server
    public string GetString(string key, string defaultValue = "")
    {
        var raw = GetRaw(key);
        if (string.IsNullOrEmpty(raw)) return defaultValue;
        return raw.Length >= 2 && raw[0] == '"' && raw[^1] == '"'
            ? raw[1..^1]
            : raw;
    }

    public bool GetBool(string key, bool defaultValue = false)
    {
        var raw = GetRaw(key);
        if (string.IsNullOrEmpty(raw)) return defaultValue;
        return raw.Equals("True", StringComparison.OrdinalIgnoreCase) ? true
            : raw.Equals("False", StringComparison.OrdinalIgnoreCase) ? false
            : defaultValue;
    }

    public decimal GetDecimal(string key, decimal defaultValue = 0m)
    {
        var raw = GetRaw(key);
        return decimal.TryParse(raw, System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture, out var v) ? v : defaultValue;
    }

    public int GetInt(string key, int defaultValue = 0)
    {
        var raw = GetRaw(key);
        return int.TryParse(raw, out var v) ? v : defaultValue;
    }

    // ── Setters ──────────────────────────────────────────────────────────────

    // Stores a string value. Automatically adds surrounding quotes.
    public void SetString(string key, string value)
    {
        var escaped = value.Replace("\"", "\\\"", StringComparison.Ordinal);
        SetRaw(key, $"\"{escaped}\"");
    }

    public void SetBool(string key, bool value)
        => SetRaw(key, value ? "True" : "False");

    public void SetDecimal(string key, decimal value, string format = "0.000000")
        => SetRaw(key, value.ToString(format, System.Globalization.CultureInfo.InvariantCulture));

    public void SetInt(string key, int value)
        => SetRaw(key, value.ToString());

    // Stores a value exactly as given (for enum values, array values, unknown settings).
    public void SetRaw(string key, string rawValue)
    {
        if (_entryIndex.TryGetValue(key, out var idx))
            _entries[idx] = (key, rawValue);
        else
        {
            _entryIndex[key] = _entries.Count;
            _entries.Add((key, rawValue));
        }
    }

    public void Remove(string key)
    {
        if (!_entryIndex.TryGetValue(key, out var idx)) return;
        _entries.RemoveAt(idx);
        _entryIndex.Remove(key);
        // Rebuild index for entries that shifted
        for (var i = idx; i < _entries.Count; i++)
            _entryIndex[_entries[i].Key] = i;
    }

    // Returns all entry keys in their original file order.
    public IEnumerable<string> Keys => _entries.Select(e => e.Key);

    // ── Serialize ─────────────────────────────────────────────────────────────

    // Reconstructs the full file content, replacing only the OptionSettings line.
    public string Serialize()
    {
        var optionLine = BuildOptionSettingsLine();

        if (_optionSettingsLineIndex >= 0 && _optionSettingsLineIndex < _lines.Count)
        {
            var result = new List<string>(_lines);
            result[_optionSettingsLineIndex] = optionLine;
            return string.Join(Environment.NewLine, result);
        }

        // OptionSettings line was not in the original file — append it
        var all = new List<string>(_lines) { optionLine };
        return string.Join(Environment.NewLine, all);
    }

    private string BuildOptionSettingsLine()
    {
        var sb = new StringBuilder("OptionSettings=(");
        var first = true;
        foreach (var (key, rawValue) in _entries)
        {
            if (!first) sb.Append(',');
            sb.Append(key);
            sb.Append('=');
            sb.Append(rawValue);
            first = false;
        }
        sb.Append(')');
        return sb.ToString();
    }

    // ── Atomic save with backup ───────────────────────────────────────────────

    public async Task SaveAsync(string path, bool createBackup = true)
    {
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        if (createBackup && File.Exists(path))
        {
            var bakPath = $"{path}.{DateTime.UtcNow:yyyyMMddHHmmss}.bak";
            File.Copy(path, bakPath, overwrite: false);
        }

        var content = Serialize();
        var tmp = path + ".tmp";
        await File.WriteAllTextAsync(tmp, content, Encoding.UTF8);
        File.Move(tmp, path, overwrite: true);
    }

    // ── Utilities ─────────────────────────────────────────────────────────────

    // Splits 'input' on 'delimiter' at depth 0 (ignoring delimiters inside
    // nested parentheses or double-quoted strings).
    internal static List<string> SplitTopLevel(string input, char delimiter)
    {
        var parts = new List<string>();
        var current = new StringBuilder();
        var depth = 0;
        var inQuote = false;

        for (var i = 0; i < input.Length; i++)
        {
            var ch = input[i];

            if (inQuote)
            {
                current.Append(ch);
                if (ch == '\\' && i + 1 < input.Length)
                {
                    // Consume escaped character
                    current.Append(input[++i]);
                }
                else if (ch == '"')
                {
                    inQuote = false;
                }
            }
            else if (ch == '"')
            {
                inQuote = true;
                current.Append(ch);
            }
            else if (ch == '(')
            {
                depth++;
                current.Append(ch);
            }
            else if (ch == ')')
            {
                depth--;
                current.Append(ch);
            }
            else if (ch == delimiter && depth == 0)
            {
                parts.Add(current.ToString());
                current.Clear();
            }
            else
            {
                current.Append(ch);
            }
        }

        if (current.Length > 0)
            parts.Add(current.ToString());

        return parts;
    }

    // Extracts the entries that are NOT in knownKeys.
    public Dictionary<string, string> GetUnknownEntries(IReadOnlyCollection<string> knownKeys)
    {
        return _entries
            .Where(e => !knownKeys.Contains(e.Key, StringComparer.OrdinalIgnoreCase))
            .ToDictionary(e => e.Key, e => e.RawValue, StringComparer.OrdinalIgnoreCase);
    }
}
