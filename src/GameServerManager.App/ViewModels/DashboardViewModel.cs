using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Input;
using System.Windows.Threading;
using GameServerManager.Core.Models;
using GameServerManager.GameProviders;
using GameServerManager.Services;

namespace GameServerManager.App.ViewModels;

public class DashboardViewModel : BaseViewModel, IDisposable
{
    private readonly AppDataPaths _paths;
    private readonly GameProviderRegistry _providers;
    private readonly ServersJsonService _serversJsonService;
    private readonly ServerProcessService _processService;
    private readonly ServerMonitorService _monitorService;
    private readonly DispatcherTimer _refreshTimer;
    private readonly List<int> _cpuHistory = new();
    private readonly List<int> _memoryHistory = new();
    private readonly List<int> _playerHistory = new();
    private readonly List<int> _diskHistory = new();
    private readonly List<int> _networkHistory = new();
    private bool _isRefreshing;
    private string _message = "Loading dashboard analytics...";

    public DashboardViewModel()
    {
        _providers = GameProviderRegistry.CreateDefault();
        _paths = new AppDataPaths();
        _serversJsonService = new ServersJsonService(_paths);
        _processService = new ServerProcessService(_providers, _paths);
        _monitorService = new ServerMonitorService(_processService, new ServerQueryService());

        StartCommand = new RelayCommand(async parameter => await StartAsync(parameter as ServerCardViewModel));
        StopCommand = new RelayCommand(async parameter => await StopAsync(parameter as ServerCardViewModel));
        RestartCommand = new RelayCommand(async parameter => await RestartAsync(parameter as ServerCardViewModel));
        RefreshCommand = new RelayCommand(async _ => await LoadAsync());

        _refreshTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
        _refreshTimer.Tick += async (_, _) => await RefreshMonitoringAsync();
        _refreshTimer.Start();

        _ = LoadAsync();
    }

    public ObservableCollection<ServerCardViewModel> Servers { get; } = new();
    public ObservableCollection<DashboardActivityViewModel> RecentActivity { get; } = new();

    public ICommand StartCommand { get; }
    public ICommand StopCommand { get; }
    public ICommand RestartCommand { get; }
    public ICommand RefreshCommand { get; }

    public string Message
    {
        get => _message;
        set => SetProperty(ref _message, value);
    }

    public int ProfileCount => Servers.Count;
    public int ProfileProgressMaximum => Math.Max(1, ProfileCount);
    public int ProfileProgressValue => ProfileCount == 0 ? 0 : ProfileCount;
    public int OnlineCount => Servers.Count(server => server.Status == ServerStatus.Running);
    public int OfflineCount => Math.Max(0, ProfileCount - OnlineCount);
    public bool IsEmpty => ProfileCount == 0;
    public int ActivePlayers => Servers.Sum(server => server.PlayerCount);
    public int MaxPlayers => Servers.Sum(server => Math.Max(server.MaxPlayers, 0));
    public string PlayerCapacityText => MaxPlayers == 0 ? "0 / 0" : $"{ActivePlayers} / {MaxPlayers}";
    public int CpuUsagePercent => OnlineCount == 0 ? 0 : (int)Math.Round(Servers.Where(IsOnline).Average(server => server.CpuPercent));
    public int MemoryUsageMb => Servers.Sum(GetRamUsedMb);
    public int MemoryLimitMb => Servers.Sum(GetRamLimitMb);
    public int MemoryUsagePercent => MemoryLimitMb <= 0 ? 0 : Math.Clamp(MemoryUsageMb * 100 / MemoryLimitMb, 0, 100);
    public string MemoryUsageText => $"{FormatMb(MemoryUsageMb)} / {FormatMb(MemoryLimitMb)}";
    public long StorageUsageBytes => Servers.Sum(server => GetDirectorySize(GetStorageDirectory(server.Profile)));
    public string StorageUsageText => FormatBytes(StorageUsageBytes);
    public int StorageUsagePercent => Math.Clamp((int)Math.Round(StorageUsageBytes / 1024d / 1024d / 1024d), 0, 100);
    public int NetworkThroughputMbps => Servers.Sum(server => server.Status == ServerStatus.Running ? Math.Max(1, server.PlayerCount * 2) : 0);
    public int ActiveProcesses => OnlineCount;
    public string RunningTasksText => OnlineCount == 0 ? "0" : Servers.Where(IsOnline).Sum(server => server.CpuPercent).ToString();
    public int RunningTasksPercent => Math.Clamp(CpuUsagePercent, 0, 100);
    public string BackupStatusText => Servers.Count == 0
        ? "No servers"
        : $"{Servers.Count(server => server.Profile.LastBackupAt.HasValue)} / {Servers.Count}";
    public int BackupCoveragePercent => Servers.Count == 0
        ? 0
        : Math.Clamp(Servers.Count(server => server.Profile.LastBackupAt.HasValue) * 100 / Servers.Count, 0, 100);
    public int FavoriteCount => Servers.Count(server => server.IsFavorite);
    public int HealthScore => CalculateHealthScore();
    public int ResourceAllocationPercent => Math.Clamp(Math.Max(CpuUsagePercent, MemoryUsagePercent), 0, 100);
    public int ServiceAvailabilityPercent => ProfileCount == 0 ? 0 : Math.Clamp(OnlineCount * 100 / ProfileCount, 0, 100);
    public int NetworkConnectivityPercent => ProfileCount == 0 ? 0 : Math.Clamp(Servers.Count(server => HasQueryEndpoint(server.Profile)) * 100 / ProfileCount, 0, 100);
    public int StorageHealthPercent => Math.Clamp(100 - StorageUsagePercent, 0, 100);
    public string CpuChartPath => BuildChartPath(_cpuHistory, 260, 82);
    public string MemoryChartPath => BuildChartPath(_memoryHistory, 260, 82);
    public string PlayerChartPath => BuildChartPath(_playerHistory, 260, 82);
    public string StorageChartPath => BuildChartPath(_diskHistory, 260, 82);
    public string PlayerSparklinePath => BuildChartPath(_playerHistory, 116, 24);
    public string NetworkSparklinePath => BuildChartPath(_networkHistory, 116, 24);
    public string NetworkChartPath => BuildChartPath(_networkHistory, 260, 82);

    private async Task LoadAsync()
    {
        try
        {
            Servers.Clear();
            var profiles = await _serversJsonService.LoadServersAsync();
            foreach (var profile in profiles)
            {
                NormalizeProfile(profile);
                if (_providers.TryGetProvider(profile.GameId, out var provider))
                {
                    Servers.Add(new ServerCardViewModel(profile, provider, _processService));
                }
            }

            Message = Servers.Count == 0
                ? $"No servers found in {_serversJsonService.ServersJsonPath}."
                : $"Loaded {Servers.Count} server(s) from {_serversJsonService.ServersJsonPath}.";
            AddActivity("Dashboard Refreshed", Message, "#4AA8FF");
            await RefreshMonitoringAsync();
        }
        catch (Exception ex)
        {
            Message = $"Dashboard failed to load: {ex.Message}";
            AddActivity("Dashboard Error", ex.Message, "#FF6969");
            NotifyAnalyticsChanged();
        }
    }

    private async Task RefreshMonitoringAsync()
    {
        if (_isRefreshing)
        {
            return;
        }

        _isRefreshing = true;
        try
        {
            var servers = Servers.ToList();
            var snapshots = await Task.WhenAll(servers.Select(server => _monitorService.GetSnapshotAsync(server.Profile)));
            for (var i = 0; i < servers.Count; i++)
            {
                var previousStatus = servers[i].Status;
                servers[i].ApplyMonitorSnapshot(snapshots[i]);
                if (previousStatus != servers[i].Status)
                {
                    AddActivity(
                        $"{servers[i].ServerName} {servers[i].StatusText}",
                        $"{servers[i].GameName} status changed from {previousStatus}.",
                        servers[i].Status == ServerStatus.Running ? "#2BC977" : "#FA6D72");
                }
            }

            AddSample(_cpuHistory, CpuUsagePercent);
            AddSample(_memoryHistory, MemoryUsagePercent);
            AddSample(_playerHistory, MaxPlayers == 0 ? 0 : Math.Clamp(ActivePlayers * 100 / MaxPlayers, 0, 100));
            AddSample(_diskHistory, StorageUsagePercent);
            AddSample(_networkHistory, Math.Clamp(NetworkThroughputMbps, 0, 100));
            NotifyAnalyticsChanged();
        }
        finally
        {
            _isRefreshing = false;
        }
    }

    private async Task StartAsync(ServerCardViewModel? server)
    {
        if (server == null)
        {
            return;
        }

        try
        {
            await _processService.StartServerAsync(server.Profile);
            await _serversJsonService.UpdateServerAsync(server.Profile);
            await RefreshMonitoringAsync();
            AddActivity("Server Started", server.ServerName, "#2BC977");
            Message = $"Started {server.ServerName}.";
        }
        catch (Exception ex)
        {
            Message = $"Start failed: {ex.Message}";
            AddActivity("Start Failed", $"{server.ServerName}: {ex.Message}", "#FF6969");
        }
    }

    private async Task StopAsync(ServerCardViewModel? server)
    {
        if (server == null)
        {
            return;
        }

        try
        {
            await _processService.StopServerAsync(server.Profile);
            await _serversJsonService.UpdateServerAsync(server.Profile);
            await RefreshMonitoringAsync();
            AddActivity("Server Stopped", server.ServerName, "#FA6D72");
            Message = $"Stopped {server.ServerName}.";
        }
        catch (Exception ex)
        {
            Message = $"Stop failed: {ex.Message}";
            AddActivity("Stop Failed", $"{server.ServerName}: {ex.Message}", "#FF6969");
        }
    }

    private async Task RestartAsync(ServerCardViewModel? server)
    {
        if (server == null)
        {
            return;
        }

        try
        {
            await _processService.RestartServerAsync(server.Profile);
            await _serversJsonService.UpdateServerAsync(server.Profile);
            await RefreshMonitoringAsync();
            AddActivity("Server Restarted", server.ServerName, "#4AA8FF");
            Message = $"Restarted {server.ServerName}.";
        }
        catch (Exception ex)
        {
            Message = $"Restart failed: {ex.Message}";
            AddActivity("Restart Failed", $"{server.ServerName}: {ex.Message}", "#FF6969");
        }
    }

    private void NotifyAnalyticsChanged()
    {
        OnPropertyChanged(nameof(ProfileCount));
        OnPropertyChanged(nameof(ProfileProgressMaximum));
        OnPropertyChanged(nameof(ProfileProgressValue));
        OnPropertyChanged(nameof(OnlineCount));
        OnPropertyChanged(nameof(OfflineCount));
        OnPropertyChanged(nameof(IsEmpty));
        OnPropertyChanged(nameof(ActivePlayers));
        OnPropertyChanged(nameof(MaxPlayers));
        OnPropertyChanged(nameof(PlayerCapacityText));
        OnPropertyChanged(nameof(CpuUsagePercent));
        OnPropertyChanged(nameof(MemoryUsageMb));
        OnPropertyChanged(nameof(MemoryLimitMb));
        OnPropertyChanged(nameof(MemoryUsagePercent));
        OnPropertyChanged(nameof(MemoryUsageText));
        OnPropertyChanged(nameof(StorageUsageBytes));
        OnPropertyChanged(nameof(StorageUsageText));
        OnPropertyChanged(nameof(StorageUsagePercent));
        OnPropertyChanged(nameof(NetworkThroughputMbps));
        OnPropertyChanged(nameof(ActiveProcesses));
        OnPropertyChanged(nameof(RunningTasksText));
        OnPropertyChanged(nameof(RunningTasksPercent));
        OnPropertyChanged(nameof(BackupStatusText));
        OnPropertyChanged(nameof(BackupCoveragePercent));
        OnPropertyChanged(nameof(FavoriteCount));
        OnPropertyChanged(nameof(HealthScore));
        OnPropertyChanged(nameof(ResourceAllocationPercent));
        OnPropertyChanged(nameof(ServiceAvailabilityPercent));
        OnPropertyChanged(nameof(NetworkConnectivityPercent));
        OnPropertyChanged(nameof(StorageHealthPercent));
        OnPropertyChanged(nameof(CpuChartPath));
        OnPropertyChanged(nameof(MemoryChartPath));
        OnPropertyChanged(nameof(PlayerChartPath));
        OnPropertyChanged(nameof(StorageChartPath));
        OnPropertyChanged(nameof(PlayerSparklinePath));
        OnPropertyChanged(nameof(NetworkSparklinePath));
        OnPropertyChanged(nameof(NetworkChartPath));
    }

    private void AddActivity(string title, string detail, string color)
    {
        RecentActivity.Insert(0, new DashboardActivityViewModel(title, detail, "now", color));
        while (RecentActivity.Count > 5)
        {
            RecentActivity.RemoveAt(RecentActivity.Count - 1);
        }
    }

    private static void AddSample(List<int> history, int value)
    {
        history.Add(Math.Clamp(value, 0, 100));
        if (history.Count > 24)
        {
            history.RemoveAt(0);
        }
    }

    private int CalculateHealthScore()
    {
        if (ProfileCount == 0)
        {
            return 0;
        }

        var availability = ServiceAvailabilityPercent;
        var resourceHeadroom = 100 - Math.Max(CpuUsagePercent, MemoryUsagePercent);
        var backupCoverage = BackupCoveragePercent;
        return Math.Clamp((availability * 4 + resourceHeadroom * 3 + backupCoverage * 2 + StorageHealthPercent) / 10, 0, 100);
    }

    private static bool IsOnline(ServerCardViewModel server)
    {
        return server.Status == ServerStatus.Running;
    }

    private static int GetRamUsedMb(ServerCardViewModel server)
    {
        return server.RamMb;
    }

    private static int GetRamLimitMb(ServerCardViewModel server)
    {
        return server.RamLimitMb;
    }

    private static string GetStorageDirectory(ServerProfile profile)
    {
        if (Directory.Exists(profile.InstallPath))
        {
            return profile.InstallPath;
        }

        if (profile.Settings.TryGetValue("saveDirectory", out var saveDirectory) && Directory.Exists(saveDirectory))
        {
            return saveDirectory;
        }

        return string.Empty;
    }

    private static long GetDirectorySize(string directory)
    {
        if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
        {
            return 0;
        }

        try
        {
            return Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories)
                .Sum(file =>
                {
                    try
                    {
                        return new FileInfo(file).Length;
                    }
                    catch
                    {
                        return 0;
                    }
                });
        }
        catch
        {
            return 0;
        }
    }

    private static bool HasQueryEndpoint(ServerProfile profile)
    {
        return profile.Ports.Any(port =>
            port.Name.Contains("query", StringComparison.OrdinalIgnoreCase) ||
            port.Name.Contains("game", StringComparison.OrdinalIgnoreCase));
    }

    private static string FormatMb(int mb)
    {
        return mb >= 1024 ? $"{mb / 1024.0:0.0} GB" : $"{mb} MB";
    }

    private static string FormatBytes(long bytes)
    {
        var gb = bytes / 1024d / 1024d / 1024d;
        if (gb >= 1)
        {
            return $"{gb:0.0} GB";
        }

        var mb = bytes / 1024d / 1024d;
        return $"{mb:0.0} MB";
    }

    private static string BuildChartPath(IReadOnlyList<int> values, double width, double height)
    {
        if (values.Count == 0)
        {
            return $"M0,{height:0} L{width:0},{height:0}";
        }

        if (values.Count == 1)
        {
            var y = height - (values[0] / 100d * height);
            return $"M0,{y:0.#} L{width:0},{y:0.#}";
        }

        var step = width / (values.Count - 1);
        var points = values.Select((value, index) =>
        {
            var x = index * step;
            var y = height - (Math.Clamp(value, 0, 100) / 100d * height);
            return index == 0 ? $"M{x:0.#},{y:0.#}" : $"L{x:0.#},{y:0.#}";
        });
        return string.Join(" ", points);
    }

    private static void NormalizeProfile(ServerProfile profile)
    {
        if (string.IsNullOrWhiteSpace(profile.Id))
        {
            profile.Id = Guid.NewGuid().ToString();
        }

        if (string.IsNullOrWhiteSpace(profile.ProfileName))
        {
            profile.ProfileName = string.IsNullOrWhiteSpace(profile.ServerName) ? "Unnamed Server" : profile.ServerName;
        }

        if (string.IsNullOrWhiteSpace(profile.ServerName))
        {
            profile.ServerName = profile.ProfileName;
        }
    }

    public void Dispose()
    {
        _refreshTimer.Stop();
        _processService.Dispose();
    }
}

public sealed class DashboardActivityViewModel
{
    public DashboardActivityViewModel(string title, string detail, string timeText, string color)
    {
        Title = title;
        Detail = detail;
        TimeText = timeText;
        Color = color;
    }

    public string Title { get; }
    public string Detail { get; }
    public string TimeText { get; }
    public string Color { get; }
}

public class RelayCommand : ICommand
{
    private readonly Action<object?> _execute;
    private readonly Func<bool>? _canExecute;

    public event EventHandler? CanExecuteChanged;

    public RelayCommand(Action<object?> execute, Func<bool>? canExecute = null)
    {
        _execute = execute ?? throw new ArgumentNullException(nameof(execute));
        _canExecute = canExecute;
    }

    public bool CanExecute(object? parameter) => _canExecute == null || _canExecute();

    public void Execute(object? parameter) => _execute(parameter);

    public void NotifyCanExecuteChanged()
    {
        CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }
}
