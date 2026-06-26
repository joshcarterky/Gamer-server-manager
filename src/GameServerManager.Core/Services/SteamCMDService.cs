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

        public event EventHandler<string>? StatusChanged;
        public event EventHandler<string>? LogOutput;

        public SteamCMDService()
            : this(
                Path.Combine(AppContext.BaseDirectory, "Tools", "SteamCMD"),
                Path.Combine(AppContext.BaseDirectory, "Data", "Logs"))
        {
        }

        public SteamCMDService(string steamCmdDirectory, string logDirectory)
        {
            _installedDirectory = steamCmdDirectory;
            _logDirectory = logDirectory;

            Directory.CreateDirectory(_installedDirectory);
            Directory.CreateDirectory(_logDirectory);
            _isInstalled = File.Exists(Path.Combine(_installedDirectory, SteamCMDFileName));
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

        /// <summary>Result of a RunInstallOrUpdateAsync operation.</summary>
        public sealed record SteamCmdRunResult(int ExitCode, string LogPath, bool Cancelled);

        /// <summary>
        /// Runs SteamCMD with the given arguments, captures output, and returns the exit code.
        /// Kills the entire process tree when the cancellation token is triggered.
        /// </summary>
        public async Task<SteamCmdRunResult> RunInstallOrUpdateAsync(
            string arguments,
            string logFile,
            IProgress<string>? progress,
            CancellationToken cancellationToken)
        {
            var logBuilder = new StringBuilder();
            Process? process = null;
            var killed = false;

            try
            {
                process = new Process();
                process.StartInfo.FileName = Path.Combine(_installedDirectory, SteamCMDFileName);
                process.StartInfo.Arguments = arguments;
                process.StartInfo.UseShellExecute = false;
                process.StartInfo.RedirectStandardOutput = true;
                process.StartInfo.RedirectStandardError = true;
                process.StartInfo.CreateNoWindow = true;

                // SteamCMD must run from its own directory so it can find its config and log files
                process.StartInfo.WorkingDirectory = _installedDirectory;
                process.Start();

                StatusChanged?.Invoke(this, $"SteamCMD started (PID {process.Id})");

                void HandleLine(string line)
                {
                    if (string.IsNullOrWhiteSpace(line)) return;
                    logBuilder.AppendLine(line);
                    progress?.Report(line);
                    StatusChanged?.Invoke(this, line);
                    LogOutput?.Invoke(this, line);
                }

                // stdout/stderr catch the banner; content_log.txt has the actual download progress
                // SteamCMD writes progress via Windows Console API (not stdout/stderr), so we tail its log file
                var stdoutTask = PipeStreamAsync(process.StandardOutput, HandleLine);
                var stderrTask = PipeStreamAsync(process.StandardError, HandleLine);
                using var tailCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                var contentLogPath = Path.Combine(_installedDirectory, "logs", "content_log.txt");
                var tailTask = TailSteamCmdLogAsync(contentLogPath, HandleLine, tailCts.Token);

                using var reg = cancellationToken.Register(() =>
                {
                    try
                    {
                        if (!process.HasExited)
                        {
                            killed = true;
                            process.Kill(entireProcessTree: true);
                        }
                    }
                    catch { }
                });

                await process.WaitForExitAsync(CancellationToken.None);
                tailCts.Cancel();
                await Task.WhenAll(stdoutTask, stderrTask, tailTask);

                var dir = Path.GetDirectoryName(logFile);
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                File.WriteAllText(logFile, logBuilder.ToString());

                return new SteamCmdRunResult(process.ExitCode, logFile, killed || cancellationToken.IsCancellationRequested);
            }
            catch
            {
                try
                {
                    var dir = Path.GetDirectoryName(logFile);
                    if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                    File.WriteAllText(logFile, logBuilder.ToString());
                }
                catch { }
                return new SteamCmdRunResult(-1, logFile, killed || cancellationToken.IsCancellationRequested);
            }
            finally
            {
                process?.Dispose();
            }
        }

        /// <summary>
        /// Gets the SteamCMD installation directory.
        /// </summary>
        public string GetInstalledDirectory() => _installedDirectory;

        // Tails steamcmd/logs/content_log.txt while SteamCMD is running.
        // SteamCMD writes real download progress there, not to stdout/stderr.
        private static async Task TailSteamCmdLogAsync(string path, Action<string> onLine, CancellationToken ct)
        {
            try
            {
                var deadline = DateTime.UtcNow.AddSeconds(15);
                while (!File.Exists(path) && !ct.IsCancellationRequested)
                {
                    if (DateTime.UtcNow > deadline) return;
                    await Task.Delay(200, ct);
                }

                using var stream = new FileStream(path, FileMode.Open, FileAccess.Read,
                    FileShare.ReadWrite | FileShare.Delete);
                // Seek to end so we only see new lines from this run
                stream.Seek(0, SeekOrigin.End);
                using var reader = new StreamReader(stream);

                while (!ct.IsCancellationRequested)
                {
                    var line = await reader.ReadLineAsync();
                    if (line is not null)
                        onLine(line);
                    else
                        await Task.Delay(150, ct);
                }
            }
            catch (OperationCanceledException) { }
            catch { }
        }

        // Reads a stream char-by-char and fires onLine for every \n or \r boundary.
        // BeginOutputReadLine only fires on \n — SteamCMD uses \r for progress overwrites.
        private static async Task PipeStreamAsync(StreamReader reader, Action<string> onLine)
        {
            var buf = new char[4096];
            var current = new System.Text.StringBuilder();
            int read;
            while ((read = await reader.ReadAsync(buf, 0, buf.Length)) > 0)
            {
                for (int i = 0; i < read; i++)
                {
                    var ch = buf[i];
                    if (ch == '\n' || ch == '\r')
                    {
                        if (current.Length > 0)
                        {
                            onLine(current.ToString());
                            current.Clear();
                        }
                    }
                    else
                    {
                        current.Append(ch);
                    }
                }
            }
            if (current.Length > 0)
                onLine(current.ToString());
        }

        public void Dispose()
        {
        }
    }
}
