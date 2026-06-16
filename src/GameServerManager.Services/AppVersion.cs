using System.Diagnostics;
using System.Reflection;

namespace GameServerManager.Services;

public static class AppVersion
{
    public static string Current
    {
        get
        {
            var assembly = Assembly.GetEntryAssembly() ?? typeof(AppVersion).Assembly;
            var version = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
                ?? FileVersionInfo.GetVersionInfo(assembly.Location).ProductVersion
                ?? assembly.GetName().Version?.ToString()
                ?? "0.0.0";

            var metadataIndex = version.IndexOf('+', StringComparison.Ordinal);
            return metadataIndex >= 0 ? version[..metadataIndex] : version;
        }
    }

    public static string RepositoryUrl
    {
        get
        {
            var assembly = Assembly.GetEntryAssembly() ?? typeof(AppVersion).Assembly;
            return assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
                       .FirstOrDefault(attribute => attribute.Key == "RepositoryUrl")?.Value
                   ?? "https://github.com/joshcarterky/Gamer-server-manager";
        }
    }

    public static bool HasConfiguredRepository =>
        Uri.TryCreate(RepositoryUrl, UriKind.Absolute, out _)
        && !RepositoryUrl.Contains("YOUR-USERNAME", StringComparison.OrdinalIgnoreCase);
}
