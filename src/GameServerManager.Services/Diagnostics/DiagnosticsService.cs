using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using GameServerManager.Core.Models;

namespace GameServerManager.Services.Diagnostics;

public sealed class DiagnosticsService
{
    private readonly AppDataPaths _paths;

    public DiagnosticsService(AppDataPaths? paths = null)
    {
        _paths = paths ?? new AppDataPaths();
    }

    public async Task<string> ExportAsync(AppSettings settings, int serverCount, IEnumerable<string> gameProfiles, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(_paths.DiagnosticsDirectory);
        var reportPath = Path.Combine(_paths.DiagnosticsDirectory, $"diagnostic-report-{DateTime.Now:yyyyMMdd-HHmmss}.json");
        var report = new
        {
            AppVersion = AppVersion.Current,
            OS = RuntimeInformation.OSDescription,
            InstallMode = _paths.IsPortable ? "Portable" : "Installer",
            settings.UpdateChannel,
            GameProfilesLoaded = gameProfiles.ToArray(),
            ServerCount = serverCount,
            ConfigPaths = new
            {
                _paths.RootDirectory,
                _paths.SettingsDirectory,
                _paths.ServersJsonPath,
                _paths.ServersDirectory,
                _paths.BackupsDirectory
            },
            LatestUpdateCheckUtc = settings.LastUpdateCheckUtc,
            SteamCMDPath = "Not configured in app settings",
            LoggingLevel = settings.LoggingLevel
        };

        await File.WriteAllTextAsync(reportPath, MaskSecrets(JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true })), cancellationToken);
        return reportPath;
    }

    public string CreateGitHubIssueTemplate(AppSettings settings)
    {
        var builder = new StringBuilder();
        builder.AppendLine("## Summary");
        builder.AppendLine();
        builder.AppendLine("## What happened?");
        builder.AppendLine();
        builder.AppendLine("## What did you expect?");
        builder.AppendLine();
        builder.AppendLine("## Steps to reproduce");
        builder.AppendLine("1. ");
        builder.AppendLine();
        builder.AppendLine("## Environment");
        builder.AppendLine($"- App version: {AppVersion.Current}");
        builder.AppendLine($"- Windows version: {RuntimeInformation.OSDescription}");
        builder.AppendLine($"- Install mode: {(_paths.IsPortable ? "Portable" : "Installer")}");
        builder.AppendLine($"- Update channel: {settings.UpdateChannel}");
        builder.AppendLine();
        builder.AppendLine("Do not paste server passwords, API keys, RCON passwords, or private tokens.");
        return builder.ToString();
    }

    public static string MaskSecrets(string input)
    {
        var secretWords = new[] { "password", "apikey", "api_key", "token", "secret", "rcon" };
        var lines = input.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
        for (var index = 0; index < lines.Length; index++)
        {
            if (secretWords.Any(word => lines[index].Contains(word, StringComparison.OrdinalIgnoreCase)))
            {
                var separatorIndex = lines[index].IndexOf(':', StringComparison.Ordinal);
                lines[index] = separatorIndex >= 0 ? $"{lines[index][..(separatorIndex + 1)]} \"********\"," : "********";
            }
        }

        return string.Join(Environment.NewLine, lines);
    }
}
