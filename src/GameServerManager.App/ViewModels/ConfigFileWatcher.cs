using System.IO;
using System.Windows;
using System.Windows.Threading;
using GameServerManager.Services;

namespace GameServerManager.App.ViewModels;

/// <summary>
/// Watches a single config file and raises <see cref="DriftDetected"/> when it
/// is changed outside the application. The app's own saves are ignored: after
/// the app reads or writes the file it calls <see cref="MarkSynced"/>, which
/// re-baselines the content fingerprint so the resulting file-system event
/// doesn't register as drift.
///
/// Native file-system events arrive on a thread-pool thread; this class
/// debounces them and marshals onto the UI dispatcher, so <see cref="DriftDetected"/>
/// always fires on the UI thread.
/// </summary>
public sealed class ConfigFileWatcher : IDisposable
{
    private readonly string _filePath;
    private readonly Dispatcher _dispatcher;
    private readonly DispatcherTimer _debounce;
    private FileSystemWatcher? _watcher;
    private string _baseline = string.Empty;
    private bool _disposed;

    public event EventHandler? DriftDetected;

    public ConfigFileWatcher(string filePath)
    {
        _filePath = filePath;
        _dispatcher = Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher;
        _debounce = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(600) };
        _debounce.Tick += OnDebounceTick;
    }

    /// <summary>Begins watching. Captures the current file as the baseline.</summary>
    public void Start()
    {
        var directory = Path.GetDirectoryName(_filePath);
        var fileName = Path.GetFileName(_filePath);
        if (string.IsNullOrWhiteSpace(directory) || string.IsNullOrWhiteSpace(fileName) || !Directory.Exists(directory))
            return; // nothing to watch yet (e.g. install folder not created)

        _baseline = ConfigFileSnapshot.Compute(_filePath);

        _watcher = new FileSystemWatcher(directory, fileName)
        {
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.Size,
            IncludeSubdirectories = false,
        };
        _watcher.Changed += OnFsEvent;
        _watcher.Created += OnFsEvent;
        _watcher.Deleted += OnFsEvent;
        _watcher.Renamed += OnFsEvent;
        _watcher.EnableRaisingEvents = true;
    }

    /// <summary>
    /// Re-baselines to the file's current content. Call this immediately after
    /// the app loads or saves the file so the app's own write isn't flagged.
    /// </summary>
    public void MarkSynced() => _baseline = ConfigFileSnapshot.Compute(_filePath);

    private void OnFsEvent(object sender, FileSystemEventArgs e)
    {
        // Coalesce the burst of events an editor produces into one check.
        if (_disposed) return;
        _dispatcher.BeginInvoke(() =>
        {
            _debounce.Stop();
            _debounce.Start();
        });
    }

    private void OnDebounceTick(object? sender, EventArgs e)
    {
        _debounce.Stop();
        if (_disposed) return;

        if (ConfigFileSnapshot.HasDrifted(_baseline, _filePath))
            DriftDetected?.Invoke(this, EventArgs.Empty);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _debounce.Stop();
        _debounce.Tick -= OnDebounceTick;
        if (_watcher != null)
        {
            _watcher.EnableRaisingEvents = false;
            _watcher.Changed -= OnFsEvent;
            _watcher.Created -= OnFsEvent;
            _watcher.Deleted -= OnFsEvent;
            _watcher.Renamed -= OnFsEvent;
            _watcher.Dispose();
            _watcher = null;
        }
    }
}
