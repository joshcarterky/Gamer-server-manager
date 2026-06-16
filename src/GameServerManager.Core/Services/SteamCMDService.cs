using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using GameServerManager.Core.Models;

namespace GameServerManager.Core.Services
{
    /// <summary>
    /// Service for managing SteamCMD operations including installation, updates, and validation.
    /// </summary>
    public class SteamCMDService : IDisposable
    {
        private static readonly string SteamCMDFileName = "steamcmd.exe";
        private string _installedDirectory;
        private string _logDirectory;
        private bool _isInstalled;
        private volatile bool _isRunning;

        public event EventHandler<string>? StatusChanged;
        public event EventHandler<string>? LogOutput;

        public SteamCMDService()
        {
            _installedDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "GameServerManager", "Tools", "SteamCMD");
            _logDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "GameServerManager", "Logs");

            Directory.CreateDirectory(_installedDirectory);
            Directory.CreateDirectory(_logDirectory);
        }

        /// <summary>
        /// Gets whether SteamCMD is installed.
        /// </summary>
        public bool IsInstalled => _isInstalled;

        /// <summary>
        /// Downloads and installs SteamCMD to the local tools directory.
        /// </summary>
        public async Task<bool> InstallSteamCMDAsync()
        {
            const string steamCMDUrl = "https://steamcdn-a.akamaihd.net/client/installer/steamcmd.zip";
            var zipPath = Path.Combine(_installedDirectory, "steamcmd.zip");

            try
            {
                StatusChanged?.Invoke(this, "Downloading SteamCMD...");
                using var httpClient = new HttpClient();
                var bytes = await httpClient.GetByteArrayAsync(steamCMDUrl);
                await File.WriteAllBytesAsync(zipPath, bytes);

                StatusChanged?.Invoke(this, "Extracting SteamCMD...");
                ZipFile.ExtractToDirectory(zipPath, _installedDirectory, overwriteFiles: true);
                _isInstalled = File.Exists(Path.Combine(_installedDirectory, SteamCMDFileName));
                StatusChanged?.Invoke(this, _isInstalled ? "SteamCMD installed." : "SteamCMD install failed.");
                return _isInstalled;
            }
            catch (Exception ex)
            {
                StatusChanged?.Invoke(this, $"SteamCMD install failed: {ex.Message}");
                return false;
            }
            finally
            {
                if (File.Exists(zipPath))
                {
                    File.Delete(zipPath);
                }
            }
        }

        /// <summary>
        /// Installs or updates a game server using SteamCMD.
        /// </summary>
        public async Task InstallServerAsync(ServerProfile profile, CancellationToken cancellationToken = default)
        {
            if (!IsInstalled)
                await InstallSteamCMDAsync();

            var logFile = Path.Combine(_logDirectory, $"install_{profile.GameId}_{DateTime.Now:yyyyMMdd_HHmmss}.txt");

            var command = $"+login anonymous +force_install_dir \"{profile.InstallPath}\" +quit";

            StatusChanged?.Invoke(this, $"Installing server '{profile.ProfileName}'...");

            await RunSteamCMDAsync(command, logFile, cancellationToken);
        }

        /// <summary>
        /// Updates an existing game server using SteamCMD.
        /// </summary>
        public async Task UpdateServerAsync(ServerProfile profile, CancellationToken cancellationToken = default)
        {
            if (!IsInstalled)
                throw new InvalidOperationException("SteamCMD is not installed.");

            var logFile = Path.Combine(_logDirectory, $"update_{profile.GameId}_{DateTime.Now:yyyyMMdd_HHmmss}.txt");

            var command = $"+login anonymous +force_install_dir \"{profile.InstallPath}\" +quit";

            StatusChanged?.Invoke(this, $"Updating server '{profile.ProfileName}'...");

            await RunSteamCMDAsync(command, logFile, cancellationToken);
        }

        /// <summary>
        /// Validates server files using SteamCMD.
        /// </summary>
        public async Task ValidateServerFilesAsync(ServerProfile profile, CancellationToken cancellationToken = default)
        {
            if (!IsInstalled)
                throw new InvalidOperationException("SteamCMD is not installed.");

            var logFile = Path.Combine(_logDirectory, $"validate_{profile.GameId}_{DateTime.Now:yyyyMMdd_HHmmss}.txt");

            var command = $"+login anonymous +force_install_dir \"{profile.InstallPath}\" +quit";

            StatusChanged?.Invoke(this, $"Validating files for '{profile.ProfileName}'...");

            await RunSteamCMDAsync(command, logFile, cancellationToken);
        }

        private async Task RunSteamCMDAsync(string command, string logFile, CancellationToken cancellationToken)
        {
            using var process = new Process();
            process.StartInfo.FileName = Path.Combine(_installedDirectory, SteamCMDFileName);
            process.StartInfo.Arguments = "-batch -verbose " + command;
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.RedirectStandardInput = true;
            process.StartInfo.CreateNoWindow = true;

            var logBuilder = new StringBuilder();

            process.OutputDataReceived += (sender, e) =>
            {
                var line = e.Data ?? string.Empty;
                logBuilder.AppendLine(line);
                StatusChanged?.Invoke(this, line);
                LogOutput?.Invoke(this, line);
            };

            try
            {
                process.Start();
                process.BeginOutputReadLine();

                while (!process.HasExited)
                {
                    if (cancellationToken.IsCancellationRequested)
                        break;
                    await Task.Delay(100, cancellationToken);
                }

                process.WaitForExit();
            }
            finally
            {
                process.CancelOutputRead();
                process.Close();
            }

            File.WriteAllText(logFile, logBuilder.ToString());
        }

        /// <summary>
        /// Gets the SteamCMD installation directory.
        /// </summary>
        public string GetInstalledDirectory() => _installedDirectory;

        /// <summary>
        /// Disposes the service.
        /// </summary>
        public void Dispose()
        {
            // Cleanup if needed
        }
    }
}
