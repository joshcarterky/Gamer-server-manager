using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using GameServerManager.Core.Models;
using GameServerManager.GameProviders;
using GameServerManager.Services;

namespace GameServerManager.App.ViewModels;

// ── Display model for one console line ───────────────────────────────────────

public sealed class ConsoleLineItem
{
    public ConsoleLineItem(string text, ConsoleLineLevel level)
    {
        DisplayText = text;
        Level = level;
    }

    public string DisplayText { get; }
    public ConsoleLineLevel Level { get; }

    public string ForegroundHex => Level switch
    {
        ConsoleLineLevel.Error   => "#FF6B6B",
        ConsoleLineLevel.Warning => "#FFB800",
        ConsoleLineLevel.Rcon    => "#29B6F6",
        ConsoleLineLevel.System  => "#7090A8",
        _                        => "#B0C8DC"
    };
}

// ── Server selector item ─────────────────────────────────────────────────────

public sealed class ServerConsoleItem
{
    public ServerConsoleItem(ServerProfile profile, bool running)
    {
        Profile = profile;
        IsRunning = running;
        StatusDot = running ? "●" : "○";
        DisplayName = $"{profile.ProfileName}  ({profile.GameId})";
    }

    public ServerProfile Profile { get; }
    public bool IsRunning { get; }
    public string StatusDot { get; }
    public string DisplayName { get; }
    public override string ToString() => DisplayName;
}

// ── ViewModel ─────────────────────────────────────────────────────────────────

public sealed class ConsoleViewModel : BaseViewModel, IDisposable
{
    private const int MaxLines = 2000;

    private readonly ServersJsonService _serversService;
    private readonly ServerProcessService _processService;
    private readonly GameProviderRegistry _providers;
    private readonly ConsoleSessionService _sessionService;

    // Per-server line cache — preserved when switching servers
    private readonly Dictionary<string, List<ConsoleLineItem>> _cache = new();
    private readonly List<ConsoleLineItem> _allLines = new();

    // Pause buffer
    private bool _isPaused;
    private readonly List<ConsoleLineItem> _pauseBuffer = new();

    // Active session
    private ConsoleSession? _session;

    // Command history
    private readonly List<string> _history = new();
    private int _historyIndex = -1;

    // ── Observable collections ────────────────────────────────────────────────

    public ObservableCollection<ServerConsoleItem> Servers { get; } = new();
    public ObservableCollection<ConsoleLineItem> FilteredLines { get; } = new();

    // ── Properties ────────────────────────────────────────────────────────────

    private ServerConsoleItem? _selectedServer;
    public ServerConsoleItem? SelectedServer
    {
        get => _selectedServer;
        set
        {
            if (SetProperty(ref _selectedServer, value))
                _ = SwitchServerAsync(value);
        }
    }

    private string _commandText = string.Empty;
    public string CommandText
    {
        get => _commandText;
        set
        {
            if (SetProperty(ref _commandText, value))
                SendCommand.NotifyCanExecuteChanged();
        }
    }

    private string _filterText = string.Empty;
    public string FilterText
    {
        get => _filterText;
        set
        {
            if (SetProperty(ref _filterText, value))
                RebuildFilteredLines();
        }
    }

    private bool _isConnected;
    public bool IsConnected
    {
        get => _isConnected;
        private set { if (SetProperty(ref _isConnected, value)) NotifyCommandStates(); }
    }

    private bool _isRunning;
    public bool IsRunning
    {
        get => _isRunning;
        private set { if (SetProperty(ref _isRunning, value)) NotifyCommandStates(); }
    }

    private bool _isLoading;
    public bool IsLoading
    {
        get => _isLoading;
        private set => SetProperty(ref _isLoading, value);
    }

    private bool _autoScroll = true;
    public bool AutoScroll
    {
        get => _autoScroll;
        set => SetProperty(ref _autoScroll, value);
    }

    private string _statusText = "Select a server to connect.";
    public string StatusText
    {
        get => _statusText;
        private set => SetProperty(ref _statusText, value);
    }

    private bool _statusIsError;
    public bool StatusIsError
    {
        get => _statusIsError;
        private set => SetProperty(ref _statusIsError, value);
    }

    private string _connectionLabel = "Disconnected";
    public string ConnectionLabel
    {
        get => _connectionLabel;
        private set => SetProperty(ref _connectionLabel, value);
    }

    private string _pauseButtonText = "Pause";
    public string PauseButtonText
    {
        get => _pauseButtonText;
        private set => SetProperty(ref _pauseButtonText, value);
    }

    public bool CanSendCommand => IsConnected && IsRunning && !string.IsNullOrWhiteSpace(_commandText);
    public bool HasServer => _selectedServer != null;

    // ── Commands ──────────────────────────────────────────────────────────────

    public RelayCommand ConnectCommand { get; }
    public RelayCommand DisconnectCommand { get; }
    public RelayCommand ReconnectCommand { get; }
    public RelayCommand SendCommand { get; }
    public RelayCommand ClearCommand { get; }
    public RelayCommand PauseResumeCommand { get; }
    public RelayCommand HistoryUpCommand { get; }
    public RelayCommand HistoryDownCommand { get; }
    public RelayCommand ExportCommand { get; }
    public RelayCommand OpenLogFolderCommand { get; }
    public RelayCommand RefreshServersCommand { get; }

    // ── Constructor ───────────────────────────────────────────────────────────

    public ConsoleViewModel(ServerProfile? initialProfile = null)
    {
        var paths = new AppDataPaths();
        _providers = GameProviderRegistry.CreateDefault();
        _processService = new ServerProcessService(_providers, paths);
        _sessionService = new ConsoleSessionService(_processService, paths);
        _serversService = new ServersJsonService(paths);

        ConnectCommand       = new RelayCommand(_ => _ = ConnectAsync(),          () => _selectedServer != null && !IsConnected);
        DisconnectCommand    = new RelayCommand(_ => Disconnect(),                 () => IsConnected);
        ReconnectCommand     = new RelayCommand(_ => _ = ReconnectAsync(),         () => _selectedServer != null);
        SendCommand          = new RelayCommand(_ => _ = SendCommandAsync(),       () => CanSendCommand);
        ClearCommand         = new RelayCommand(_ => ClearLines());
        PauseResumeCommand   = new RelayCommand(_ => TogglePause(),               () => IsConnected);
        HistoryUpCommand     = new RelayCommand(_ => NavigateHistory(-1));
        HistoryDownCommand   = new RelayCommand(_ => NavigateHistory(1));
        ExportCommand        = new RelayCommand(_ => ExportLines(),               () => FilteredLines.Count > 0);
        OpenLogFolderCommand = new RelayCommand(_ => OpenLogFolder(),             () => _selectedServer != null);
        RefreshServersCommand = new RelayCommand(_ => _ = ReloadServersAsync());

        _ = LoadInitialAsync(initialProfile);
    }

    // ── Server loading ────────────────────────────────────────────────────────

    private async Task LoadInitialAsync(ServerProfile? initial)
    {
        IsLoading = true;
        try
        {
            var profiles = await _serversService.LoadServersAsync();
            var items = profiles.Select(p => new ServerConsoleItem(p, _processService.IsRunning(p))).ToList();

            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                Servers.Clear();
                foreach (var item in items) Servers.Add(item);
                OnPropertyChanged(nameof(HasServer));

                if (initial != null)
                {
                    var match = Servers.FirstOrDefault(s => s.Profile.Id == initial.Id);
                    if (match != null)
                    {
                        _selectedServer = match;
                        OnPropertyChanged(nameof(SelectedServer));
                        _ = ConnectAsync();
                    }
                }
            });
        }
        catch (Exception ex)
        {
            SetStatus($"Failed to load servers: {ex.Message}", isError: true);
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task ReloadServersAsync()
    {
        try
        {
            var profiles = await _serversService.LoadServersAsync();
            var selectedId = _selectedServer?.Profile.Id;
            var items = profiles.Select(p => new ServerConsoleItem(p, _processService.IsRunning(p))).ToList();
            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                Servers.Clear();
                foreach (var item in items) Servers.Add(item);
                OnPropertyChanged(nameof(HasServer));
                if (selectedId != null)
                {
                    var match = Servers.FirstOrDefault(s => s.Profile.Id == selectedId);
                    if (match != null)
                    {
                        _selectedServer = match;
                        OnPropertyChanged(nameof(SelectedServer));
                    }
                }
            });
        }
        catch { }
    }

    // ── Session management ────────────────────────────────────────────────────

    private async Task SwitchServerAsync(ServerConsoleItem? target)
    {
        // Cache current lines
        if (_session != null)
            _cache[_session.ProfileId] = new List<ConsoleLineItem>(_allLines);

        Disconnect();
        _allLines.Clear();
        await Application.Current.Dispatcher.InvokeAsync(() =>
        {
            FilteredLines.Clear();
            OnPropertyChanged(nameof(HasServer));
        });

        if (target == null) return;

        // Restore cached lines
        if (_cache.TryGetValue(target.Profile.Id, out var cached))
        {
            foreach (var line in cached) _allLines.Add(line);
            await Application.Current.Dispatcher.InvokeAsync(RebuildFilteredLines);
        }

        await ConnectAsync();
    }

    private async Task ConnectAsync()
    {
        if (_selectedServer == null) return;

        SetStatus("Connecting…", isError: false);
        IsConnected = false;

        try
        {
            AddSystemLine($"Connecting to {_selectedServer.Profile.ProfileName}…");

            var session = _sessionService.CreateSession(_selectedServer.Profile);
            _session = session;
            session.LineReceived += OnLineReceived;

            var initial = await Task.Run(() => session.GetInitialLines(500));
            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                foreach (var entry in initial)
                    AppendLine(MakeLineItem(entry));
            });

            session.StartWatching();

            bool running = _processService.IsRunning(_selectedServer.Profile);
            IsConnected = true;
            IsRunning = running;
            ConnectionLabel = running ? "Live" : "Offline";
            SetStatus(running
                ? $"Connected — {_selectedServer.Profile.ProfileName} is running"
                : $"Log view — {_selectedServer.Profile.ProfileName} is not running",
                isError: false);

            AddSystemLine(running
                ? "Server is running. Commands are available via stdin."
                : "Server is offline. Showing log history.");
        }
        catch (Exception ex)
        {
            SetStatus($"Connection failed: {ex.Message}", isError: true);
            AddSystemLine($"[ERR] {ex.Message}");
        }

        NotifyCommandStates();
    }

    private void Disconnect()
    {
        if (_session != null)
        {
            _session.LineReceived -= OnLineReceived;
            _session.StopWatching();
            _session.Dispose();
            _session = null;
        }

        IsConnected = false;
        IsRunning = false;
        ConnectionLabel = "Disconnected";
        _isPaused = false;
        PauseButtonText = "Pause";
        _pauseBuffer.Clear();
        SetStatus("Disconnected.", isError: false);
        NotifyCommandStates();
    }

    private async Task ReconnectAsync()
    {
        // Cache before disconnecting
        if (_session != null)
            _cache[_session.ProfileId] = new List<ConsoleLineItem>(_allLines);

        Disconnect();
        await ConnectAsync();
    }

    // ── Line ingestion ────────────────────────────────────────────────────────

    private void OnLineReceived(ConsoleLineEntry entry)
    {
        var item = MakeLineItem(entry);
        if (_isPaused)
        {
            _pauseBuffer.Add(item);
            return;
        }
        Application.Current.Dispatcher.InvokeAsync(() => AppendLine(item));
    }

    private void AppendLine(ConsoleLineItem item)
    {
        _allLines.Add(item);
        while (_allLines.Count > MaxLines) _allLines.RemoveAt(0);

        if (PassesFilter(item))
        {
            FilteredLines.Add(item);
            while (FilteredLines.Count > MaxLines) FilteredLines.RemoveAt(0);
        }
    }

    private void AddSystemLine(string message)
    {
        var item = new ConsoleLineItem($"[{DateTime.Now:HH:mm:ss}] {message}", ConsoleLineLevel.System);
        if (Application.Current?.Dispatcher.CheckAccess() == true)
            AppendLine(item);
        else
            Application.Current?.Dispatcher.InvokeAsync(() => AppendLine(item));
    }

    private static ConsoleLineItem MakeLineItem(ConsoleLineEntry e) =>
        new(e.Text, e.Level);

    private bool PassesFilter(ConsoleLineItem item) =>
        string.IsNullOrEmpty(_filterText) ||
        item.DisplayText.Contains(_filterText, StringComparison.OrdinalIgnoreCase);

    private void RebuildFilteredLines()
    {
        FilteredLines.Clear();
        foreach (var line in _allLines)
            if (PassesFilter(line))
                FilteredLines.Add(line);
    }

    // ── Command sending ───────────────────────────────────────────────────────

    private async Task SendCommandAsync()
    {
        var cmd = _commandText.Trim();
        if (string.IsNullOrWhiteSpace(cmd) || _session == null) return;

        if (IsDangerous(cmd))
        {
            var ans = MessageBox.Show(
                $"This command may be destructive:\n\n    {cmd}\n\nSend it anyway?",
                "Confirm Command", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (ans != MessageBoxResult.Yes) return;
        }

        // Add to history (dedupe consecutive)
        if (_history.Count == 0 || _history[^1] != cmd)
            _history.Add(cmd);
        _historyIndex = _history.Count;
        CommandText = string.Empty;

        // Try RCON first for games that support it, stdin as fallback
        var profile = _selectedServer!.Profile;
        if (_providers.TryGetProvider(profile.GameId, out var provider) &&
            provider.SupportedFeatures.HasFlag(GameServerFeatures.Rcon))
        {
            var rconPort = profile.Ports.FirstOrDefault(p =>
                p.Name.Equals("RCON", StringComparison.OrdinalIgnoreCase));
            if (rconPort != null)
            {
                var password = profile.AdminPassword ?? string.Empty;
                AddSystemLine($"[RCON] → {cmd}");
                var (ok, err, resp) = await _session.SendViaRconAsync(
                    "127.0.0.1", rconPort.Port, password, cmd);
                if (ok)
                {
                    if (!string.IsNullOrWhiteSpace(resp))
                        AppendLine(new ConsoleLineItem($"[RCON] ← {resp}", ConsoleLineLevel.Rcon));
                    SetStatus($"RCON: {cmd}", isError: false);
                    return;
                }
                // RCON failed — fall through to stdin
                AddSystemLine($"RCON unavailable ({err}), trying stdin…");
            }
        }

        // Stdin path
        AppendLine(new ConsoleLineItem($"> {cmd}", ConsoleLineLevel.Rcon));
        var (stdinOk, stdinErr) = await _session.SendViaStdinAsync(cmd);
        if (!stdinOk)
        {
            AddSystemLine($"[ERR] stdin send failed: {stdinErr}");
            SetStatus($"Send failed: {stdinErr}", isError: true);
        }
        else
        {
            SetStatus($"Sent: {cmd}", isError: false);
        }
    }

    private static readonly string[] _dangerousWords =
        ["shutdown", "restart", "stop", "wipe", "destroywilddinos", "kickall",
         "doexit", "exit", "quit", "forcedestroywilddinos", "deleteworld", "ban"];

    private static bool IsDangerous(string cmd)
    {
        var lower = cmd.ToLowerInvariant();
        return _dangerousWords.Any(w => lower.Contains(w));
    }

    // ── History navigation ────────────────────────────────────────────────────

    private void NavigateHistory(int direction)
    {
        if (_history.Count == 0) return;
        _historyIndex = Math.Clamp(_historyIndex + direction, 0, _history.Count);
        CommandText = _historyIndex < _history.Count ? _history[_historyIndex] : string.Empty;
    }

    // ── Pause / clear / export / log folder ──────────────────────────────────

    private void TogglePause()
    {
        _isPaused = !_isPaused;
        PauseButtonText = _isPaused ? "Resume" : "Pause";
        if (!_isPaused)
        {
            foreach (var item in _pauseBuffer) AppendLine(item);
            _pauseBuffer.Clear();
        }
        SetStatus(_isPaused ? "Output paused." : "Output resumed.", isError: false);
    }

    private void ClearLines()
    {
        _allLines.Clear();
        FilteredLines.Clear();
        _pauseBuffer.Clear();
        SetStatus("Console cleared.", isError: false);
    }

    private void ExportLines()
    {
        var dlg = new Microsoft.Win32.SaveFileDialog
        {
            Title = "Export Console",
            Filter = "Text files (*.txt)|*.txt|All files (*.*)|*.*",
            FileName = $"console_{_selectedServer?.Profile.ProfileName ?? "export"}_{DateTime.Now:yyyyMMdd_HHmmss}.txt"
        };
        if (dlg.ShowDialog() != true) return;
        try
        {
            File.WriteAllLines(dlg.FileName, FilteredLines.Select(l => l.DisplayText));
            SetStatus($"Exported {FilteredLines.Count} lines.", isError: false);
        }
        catch (Exception ex)
        {
            SetStatus($"Export failed: {ex.Message}", isError: true);
        }
    }

    private void OpenLogFolder()
    {
        if (_selectedServer == null) return;
        var logPath = _processService.GetConsoleLogPath(_selectedServer.Profile);
        var dir = Path.GetDirectoryName(logPath);
        if (dir != null && Directory.Exists(dir))
            System.Diagnostics.Process.Start("explorer.exe", dir);
        else
            SetStatus("Log folder not found.", isError: true);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private void SetStatus(string message, bool isError)
    {
        StatusText = message;
        StatusIsError = isError;
    }

    private void NotifyCommandStates()
    {
        ConnectCommand.NotifyCanExecuteChanged();
        DisconnectCommand.NotifyCanExecuteChanged();
        ReconnectCommand.NotifyCanExecuteChanged();
        SendCommand.NotifyCanExecuteChanged();
        PauseResumeCommand.NotifyCanExecuteChanged();
        ExportCommand.NotifyCanExecuteChanged();
        OpenLogFolderCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(CanSendCommand));
        OnPropertyChanged(nameof(HasServer));
    }

    public void Dispose()
    {
        Disconnect();
        _processService.Dispose();
        _sessionService.Dispose();
    }
}
