using System.Security.Cryptography;

namespace GameServerManager.Services;

/// <summary>
/// Content fingerprint of a config file, used for drift detection — telling
/// "the app wrote this" apart from "something changed it outside the app".
///
/// Pure and side-effect-free so it can be unit-tested headlessly; the WPF
/// file-watch glue lives in the App layer and calls into this.
/// </summary>
public static class ConfigFileSnapshot
{
    /// <summary>
    /// SHA-256 hex digest of the file's bytes, or empty string when the file
    /// is missing or unreadable. A missing file hashes to "" so that deleting
    /// a watched file registers as drift against a non-empty baseline.
    /// </summary>
    public static string Compute(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            return string.Empty;

        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            var hash = SHA256.HashData(stream);
            return Convert.ToHexString(hash);
        }
        catch (IOException)
        {
            // File is locked mid-write by another process — treat as "unknown",
            // the caller will retry on the next change notification.
            return string.Empty;
        }
        catch (UnauthorizedAccessException)
        {
            return string.Empty;
        }
    }

    /// <summary>
    /// True when the file's current content differs from a previously captured
    /// baseline. An empty current hash (locked/unreadable) is treated as
    /// "no decision yet" — not drift — to avoid false positives during writes.
    /// </summary>
    public static bool HasDrifted(string baselineHash, string path)
    {
        var current = Compute(path);
        if (current.Length == 0 && File.Exists(path))
            return false; // momentarily unreadable; don't cry drift

        return !string.Equals(current, baselineHash, StringComparison.Ordinal);
    }
}
