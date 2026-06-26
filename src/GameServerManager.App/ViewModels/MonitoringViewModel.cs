using System.Collections.ObjectModel;
using System.Windows.Input;
using System.Windows.Threading;
using GameServerManager.Core.Models;
using GameServerManager.GameProviders;
using GameServerManager.Services;

namespace GameServerManager.App.ViewModels;

public class MonitoringViewModel : BaseViewModel, IDisposable
{
    private readonly ServersJsonService _serversJsonService;
    private readonly ServerProcessService _processService;
    private readonly ServerMonitorService _monitorService;
    private readonly DispatcherTimer _refreshTimer;
    private bool _isRefreshing;
    private string _lastUpdated = "—";

    public MonitoringViewModel()
    {
        var paths = new AppDataPaths();
        var registry = GameProviderRegistry.CreateDefault();
        _serversJsonService = new ServersJsonService(paths);
        _processService = new ServerProcessService(registry, paths);
        _monitorService = new ServerMonitorService(_processService, new ServerQueryService());

        RefreshCommand = new RelayCommand(async _ => await RefreshAsync());

        _refreshTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
        _refreshTimer.Tick += async (_, _) => await RefreshAsync();
        _refreshTimer.Start();

        _ = LoadAsync();
    }

    public ObservableCollection<ServerMonitorEntryViewModel> Entries { get; } = new();
    public ICommand RefreshCommand { get; }

    public string LastUpdated
    {
        get => _lastUpdated;
        set => SetProperty(ref _lastUpdated, value);
    }

    public bool HasNoServers => Entries.Count == 0;
    public int TotalRunning => Entries.Count(e => e.Status == ServerStatus.Running);
    public int TotalPlayers => Entries.Sum(e => e.PlayerCount);
    public string AverageCpu => Entries.Count == 0 ? "0%" : $"{(int)Entries.Average(e => e.CpuPercent)}%";
    public long TotalRamMb => Entries.Sum(e => (long)e.RamMb);
    public string TotalRamText => TotalRamMb >= 1024 ? $"{TotalRamMb / 1024.0:0.0} GB" : $"{TotalRamMb} MB";

    private async Task LoadAsync()
    {
        var profiles = await _serversJsonService.LoadServersAsync();
        var registry = GameProviderRegistry.CreateDefault();
        Entries.Clear();
        foreach (var profile in profiles)
        {
            if (registry.TryGetProvider(profile.GameId, out var provider))
                Entries.Add(new ServerMonitorEntryViewModel(profile, provider));
        }
        OnPropertyChanged(nameof(HasNoServers));
        await RefreshAsync();
    }

    private async Task RefreshAsync()
    {
        if (_isRefreshing) return;
        _isRefreshing = true;
        try
        {
            var entries = Entries.ToList();
            var snapshots = await Task.WhenAll(entries.Select(e => _monitorService.GetSnapshotAsync(e.Profile)));
            for (var i = 0; i < entries.Count; i++)
                entries[i].ApplySnapshot(snapshots[i]);

            LastUpdated = DateTime.Now.ToString("HH:mm:ss");
            OnPropertyChanged(nameof(TotalRunning));
            OnPropertyChanged(nameof(TotalPlayers));
            OnPropertyChanged(nameof(AverageCpu));
            OnPropertyChanged(nameof(TotalRamMb));
            OnPropertyChanged(nameof(TotalRamText));
        }
        finally
        {
            _isRefreshing = false;
        }
    }

    public void Dispose()
    {
        _refreshTimer.Stop();
        _processService.Dispose();
    }
}

public class ServerMonitorEntryViewModel : BaseViewModel
{
    private ServerStatus _status;
    private int _cpuPercent;
    private int _ramMb;
    private TimeSpan _uptime;
    private int _playerCount;

    public ServerMonitorEntryViewModel(ServerProfile profile, IGameServerProvider provider)
    {
        Profile = profile;
        GameName = provider.GameName;
        _status = profile.Status;
    }

    public ServerProfile Profile { get; }
    public string Name => Profile.ProfileName;
    public string GameName { get; }

    public ServerStatus Status
    {
        get => _status;
        private set
        {
            if (SetProperty(ref _status, value))
            {
                OnPropertyChanged(nameof(StatusText));
                OnPropertyChanged(nameof(StatusColor));
                OnPropertyChanged(nameof(StatusDot));
            }
        }
    }

    public string StatusText => _status switch
    {
        ServerStatus.Running => "Online",
        ServerStatus.Starting => "Starting",
        ServerStatus.Stopping => "Stopping",
        ServerStatus.Restarting => "Restarting",
        ServerStatus.Updating => "Updating",
        ServerStatus.Error => "Error",
        _ => "Offline"
    };

    public string StatusColor => _status switch
    {
        ServerStatus.Running => "#3DDA85",
        ServerStatus.Starting or ServerStatus.Restarting => "#5BB8FF",
        ServerStatus.Error => "#FF6B6B",
        _ => "#7A95AA"
    };

    public string StatusDot => _status == ServerStatus.Running ? "●" : "○";

    public int CpuPercent
    {
        get => _cpuPercent;
        private set
        {
            if (SetProperty(ref _cpuPercent, value))
                OnPropertyChanged(nameof(CpuText));
        }
    }

    public string CpuText => _status == ServerStatus.Running ? $"{_cpuPercent}%" : "—";

    public int RamMb
    {
        get => _ramMb;
        private set
        {
            if (SetProperty(ref _ramMb, value))
                OnPropertyChanged(nameof(RamText));
        }
    }

    public string RamText => _status == ServerStatus.Running
        ? (_ramMb >= 1024 ? $"{_ramMb / 1024.0:0.0} GB" : $"{_ramMb} MB")
        : "—";

    public int PlayerCount
    {
        get => _playerCount;
        private set
        {
            if (SetProperty(ref _playerCount, value))
                OnPropertyChanged(nameof(PlayersText));
        }
    }

    public string PlayersText => _status == ServerStatus.Running
        ? $"{_playerCount} / {Profile.MaxPlayers}"
        : "—";

    public string UptimeText => _status == ServerStatus.Running ? FormatUptime(_uptime) : "—";

    public string LastStarted => Profile.LastStartedAt?.ToLocalTime().ToString("g") ?? "Never";

    public void ApplySnapshot(ServerMonitorSnapshot snapshot)
    {
        Status = snapshot.Status;
        CpuPercent = snapshot.CpuPercent;
        RamMb = snapshot.RamMb;
        _uptime = snapshot.Uptime;
        PlayerCount = snapshot.PlayerCount;
        OnPropertyChanged(nameof(UptimeText));
        OnPropertyChanged(nameof(LastStarted));
    }

    private static string FormatUptime(TimeSpan uptime)
    {
        if (uptime.TotalDays >= 1)
            return $"{(int)uptime.TotalDays}d {uptime.Hours}h {uptime.Minutes}m";
        if (uptime.TotalHours >= 1)
            return $"{(int)uptime.TotalHours}h {uptime.Minutes}m";
        return $"{Math.Max(0, uptime.Minutes)}m";
    }
}
