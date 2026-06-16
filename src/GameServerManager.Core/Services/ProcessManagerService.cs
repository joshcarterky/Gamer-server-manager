using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Timers;

namespace GameServerManager.Core.Services
{
    /// <summary>
    /// Manages the lifecycle of a game server process.
    /// Supports start, stop, restart, crash detection, and console output capture.
    /// </summary>
    public class ProcessManagerService : IDisposable
    {
        private readonly string _executablePath;
        private readonly string _workingDirectory;
        private readonly string _logFileName;
        private Process? _process;
        private bool _isRunning => _process?.HasExited == false && _process != null;
        private DateTime _startTime;
        private int _pid;

        public event Action<string>? OnConsoleOutput;
        public event Action? OnProcessStarted;
        public event Action? OnProcessStopped;
        public event Action<int, string>? OnCrashed;

        public bool IsRunning => _isRunning;
        public DateTime StartTime => _startTime;
        public int ProcessId => _pid;
        public TimeSpan Uptime => _process != null && !_process.HasExited ? DateTime.Now - _startTime : TimeSpan.Zero;

        public ProcessManagerService(string executablePath, string workingDirectory, string logFileName)
        {
            _executablePath = executablePath;
            _workingDirectory = workingDirectory;
            _logFileName = logFileName;
        }

        public void Start(string? arguments = null)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = _executablePath,
                WorkingDirectory = _workingDirectory,
                Arguments = arguments ?? string.Empty,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                RedirectStandardInput = true,
                CreateNoWindow = true,
            };

            _process = new Process { StartInfo = startInfo };
            _process.OutputDataReceived += OnProcessOutput;
            _process.ErrorDataReceived += OnProcessError;
            _process.Exited += OnProcessExited;

            _process.Start();
            _pid = _process.Id;
            _startTime = DateTime.Now;
            _process.BeginOutputReadLine();

            OnProcessStarted?.Invoke();
        }

        public void Stop()
        {
            if (_process == null || _process.HasExited) return;
            _process.Close();
            _process.WaitForExit(5000);
            if (!_process!.HasExited)
                _process.Kill(true);
            OnProcessStopped?.Invoke();
        }

        public void Restart(string? arguments = null)
        {
            Stop();
            Start(arguments);
        }

        private void OnProcessOutput(object sender, DataReceivedEventArgs e)
        {
            if (e.Data != null)
            {
                OnConsoleOutput?.Invoke(e.Data);
            }
        }

        private void OnProcessError(object sender, DataReceivedEventArgs e)
        {
            if (e.Data != null)
            {
                OnConsoleOutput?.Invoke($"[ERR] {e.Data}");
            }
        }

        private void OnProcessExited(object? sender, EventArgs e)
        {
            OnProcessStopped?.Invoke();
        }

        public void Dispose()
        {
            _process?.Dispose();
        }
    }
}