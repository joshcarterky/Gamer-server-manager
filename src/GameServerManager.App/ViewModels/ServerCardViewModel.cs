using System.IO;
using GameServerManager.Core.Models;
using GameServerManager.GameProviders;
using GameServerManager.Services;

namespace GameServerManager.App.ViewModels;

public class ServerCardViewModel : BaseViewModel
{
    private readonly ServerProcessService _processService;
    private ServerStatus _status;
    private int _cpuPercent;
    private int _ramMb;
    private int? _ramLimitMb;
    private TimeSpan _uptime = TimeSpan.Zero;
    private int _playerCount;
    private bool _isExpanded;
    private bool _isBusy;
    private string _busyText = string.Empty;

    public ServerCardViewModel(ServerProfile profile, IGameServerProvider provider, ServerProcessService processService)
    {
        Profile = profile;
        Provider = provider;
        _processService = processService;
        _status = processService.IsRunning(profile) ? ServerStatus.Running : profile.Status;
    }

    public ServerProfile Profile { get; }
    public IGameServerProvider Provider { get; }

    public string ProfileName => Profile.ProfileName;
    public string ServerName => Profile.ServerName;
    public string GameName => Provider.GameName;
    public string Tags => Profile.Settings.TryGetValue("tags", out var tags) ? tags : string.Empty;
    public bool HasTags => !string.IsNullOrWhiteSpace(Tags);

    public bool IsFavorite
    {
        get => Profile.IsFavorite;
        set
        {
            if (Profile.IsFavorite != value)
            {
                Profile.IsFavorite = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(FavoriteIcon));
                OnPropertyChanged(nameof(FavoriteColor));
            }
        }
    }

    public string FavoriteIcon => IsFavorite ? "★" : "☆";
    public string FavoriteColor => IsFavorite ? "#FFC928" : "#455A6A";

    // ── Endpoint ──────────────────────────────────────────────────────────────
    public string Endpoint => BuildEndpoint();
    public string IconText => Provider.GameName.Length == 0 ? "?" : Provider.GameName[..1].ToUpperInvariant();

    // ── Game identity ─────────────────────────────────────────────────────────
    public string GameInitials
    {
        get
        {
            var words = Provider.GameName.Split(new[] { ' ', ':' }, StringSplitOptions.RemoveEmptyEntries);
            return words.Length >= 2
                ? $"{words[0][0]}{words[1][0]}".ToUpperInvariant()
                : Provider.GameName.Length == 0
                    ? "?"
                    : Provider.GameName[..Math.Min(2, Provider.GameName.Length)].ToUpperInvariant();
        }
    }

    public string GameAccentColor => Provider.GameId switch
    {
        var id when id.Contains("ark") => "#E8622A",
        "palworld" => "#4DB6AC",
        var id when id.Contains("minecraft") => "#7CB342",
        "rust" => "#E57373",
        "valheim" => "#90CAF9",
        "conan_exiles" => "#CE9B60",
        "seven_days_to_die" => "#EF5350",
        "satisfactory" => "#FFA726",
        "factorio" => "#F57F17",
        _ => "#5C7B94"
    };

    public string GameTileBackground => Provider.GameId switch
    {
        var id when id.Contains("ark") => "#1E0D04",
        "palworld" => "#061814",
        var id when id.Contains("minecraft") => "#0C160A",
        "rust" => "#1E0808",
        "valheim" => "#081220",
        "conan_exiles" => "#160E04",
        "seven_days_to_die" => "#160606",
        "satisfactory" => "#160C04",
        "factorio" => "#140E04",
        _ => "#091420"
    };

    // ── Status ────────────────────────────────────────────────────────────────
    public string MapName => string.IsNullOrWhiteSpace(Profile.MapName) ? "—" : Profile.MapName;

    public bool IsExpanded
    {
        get => _isExpanded;
        set
        {
            if (SetProperty(ref _isExpanded, value))
            {
                OnPropertyChanged(nameof(ExpandText));
            }
        }
    }

    public string ExpandText => IsExpanded ? "Collapse" : "Details";

    public bool IsBusy
    {
        get => _isBusy;
        private set => SetProperty(ref _isBusy, value);
    }

    public string BusyText
    {
        get => _busyText;
        private set => SetProperty(ref _busyText, value);
    }

    // ── Players ───────────────────────────────────────────────────────────────
    public int PlayerCount
    {
        get => _playerCount;
        private set
        {
            if (SetProperty(ref _playerCount, value))
            {
                OnPropertyChanged(nameof(PlayersText));
                OnPropertyChanged(nameof(PlayersDisplayText));
            }
        }
    }

    public int MaxPlayers => Profile.MaxPlayers;
    public string PlayersText => $"{PlayerCount} / {MaxPlayers}";
    public string PlayersDisplayText => Status == ServerStatus.Running ? PlayersText : "—";

    // ── CPU ───────────────────────────────────────────────────────────────────
    public int CpuPercent
    {
        get => _cpuPercent;
        private set => SetProperty(ref _cpuPercent, value);
    }

    public string CpuDisplayText => Status == ServerStatus.Running ? $"{CpuPercent}%" : "—";

    // ── Memory ────────────────────────────────────────────────────────────────
    public int RamPercent => _ramLimitMb is > 0 ? Math.Clamp(_ramMb * 100 / _ramLimitMb.Value, 0, 100) : 0;
    public int RamMb => _ramMb;
    public int? RamLimitMb => _ramLimitMb;
    public bool HasRamLimit => _ramLimitMb is > 0;
    public string RamText => HasRamLimit ? $"{FormatMb(_ramMb)} / {FormatMb(_ramLimitMb!.Value)}" : FormatMb(_ramMb);

    public string RamDisplayText
    {
        get
        {
            if (Status != ServerStatus.Running)
            {
                return "—";
            }

            return _ramMb > 0 ? RamText : "Unavailable";
        }
    }

    // ── Uptime ────────────────────────────────────────────────────────────────
    public string Uptime => Status == ServerStatus.Running ? FormatUptime(_uptime) : "—";
    public string InstallPath => Profile.InstallPath;

    public bool IsRunning => Status == ServerStatus.Running;
    public bool IsPrimaryStart => Status != ServerStatus.Running;
    public string PrimaryActionLabel => Status == ServerStatus.Running ? ">_  Console" : "▶  Start";

    // ── Ports ─────────────────────────────────────────────────────────────────
    public string Ports => Profile.Ports.Count == 0
        ? "—"
        : string.Join(", ", Profile.Ports.Select(p => $"{p.Name}:{p.Port}/{p.Protocol}"));

    public string GamePortText => GetPortText("Game");
    public string QueryPortText => GetPortText("Query");
    public string RconPortText => GetPortText("RCON");

    // ── Timestamps ────────────────────────────────────────────────────────────
    public string LastStarted => Profile.LastStartedAt?.ToLocalTime().ToString("g") ?? "Never";
    public string LastStopped => Profile.LastStoppedAt?.ToLocalTime().ToString("g") ?? "Never";
    public string LastBackup => Profile.LastBackupAt?.ToLocalTime().ToString("g") ?? "Never";
    public string LastRestart => Profile.LastStartedAt?.ToLocalTime().ToString("g") ?? "Never";
    public string BackupStatus => Profile.AutoBackupEnabled ? $"Auto — {LastBackup}" : $"Manual — {LastBackup}";
    public string UpdateStatus => Profile.AutoUpdateEnabled ? "Auto update on" : "Manual update";
    public string DiskUsageText => BuildDiskUsageText();
    public int DiskUsagePercent => 0;
    public string NetworkActivityText => "Not available";

    // ── Status badge ──────────────────────────────────────────────────────────
    public ServerStatus Status
    {
        get => _status;
        private set
        {
            if (SetProperty(ref _status, value))
            {
                OnPropertyChanged(nameof(StatusText));
                OnPropertyChanged(nameof(StatusColor));
                OnPropertyChanged(nameof(StatusIcon));
                OnPropertyChanged(nameof(StatusBadgeForeground));
                OnPropertyChanged(nameof(StatusBadgeBackground));
                OnPropertyChanged(nameof(StatusBadgeBorder));
                OnPropertyChanged(nameof(CanStart));
                OnPropertyChanged(nameof(CanStop));
                OnPropertyChanged(nameof(IsRunning));
                OnPropertyChanged(nameof(IsPrimaryStart));
                OnPropertyChanged(nameof(PrimaryActionLabel));
                OnPropertyChanged(nameof(Uptime));
                OnPropertyChanged(nameof(CpuDisplayText));
                OnPropertyChanged(nameof(RamDisplayText));
                OnPropertyChanged(nameof(PlayersDisplayText));
                OnPropertyChanged(nameof(WarningBadges));
                OnPropertyChanged(nameof(HasWarnings));
            }
        }
    }

    public string StatusText => Status switch
    {
        ServerStatus.Running => "Online",
        ServerStatus.Starting => "Starting…",
        ServerStatus.Stopping => "Stopping…",
        ServerStatus.Restarting => "Restarting…",
        ServerStatus.Updating => "Updating",
        ServerStatus.Error => "Error",
        ServerStatus.Unknown => "Unknown",
        _ => "Offline"
    };

    public string StatusIcon => Status switch
    {
        ServerStatus.Running => "●",
        ServerStatus.Starting or ServerStatus.Restarting => "◎",
        ServerStatus.Stopping => "◌",
        ServerStatus.Updating => "↓",
        ServerStatus.Error => "⚠",
        _ => "○"
    };

    public string StatusBadgeForeground => Status switch
    {
        ServerStatus.Running => "#3DDA85",
        ServerStatus.Starting or ServerStatus.Restarting => "#5BB8FF",
        ServerStatus.Stopping => "#F7C34A",
        ServerStatus.Updating => "#A87EFF",
        ServerStatus.Error => "#FF6B6B",
        _ => "#7A95AA"
    };

    public string StatusBadgeBackground => Status switch
    {
        ServerStatus.Running => "#0A221A",
        ServerStatus.Starting or ServerStatus.Restarting => "#091B34",
        ServerStatus.Stopping => "#261C06",
        ServerStatus.Updating => "#180F30",
        ServerStatus.Error => "#260A0A",
        _ => "#111D2B"
    };

    public string StatusBadgeBorder => Status switch
    {
        ServerStatus.Running => "#185C38",
        ServerStatus.Starting or ServerStatus.Restarting => "#163E72",
        ServerStatus.Stopping => "#5A4214",
        ServerStatus.Updating => "#382870",
        ServerStatus.Error => "#5C1A1A",
        _ => "#213446"
    };

    // ── Legacy status color (kept for Dashboard compatibility) ────────────────
    public string StatusColor => Status switch
    {
        ServerStatus.Running => "#5CFF9A",
        ServerStatus.Starting or ServerStatus.Restarting or ServerStatus.Updating => "#4BB8FF",
        ServerStatus.Error => "#FF6969",
        _ => "#A8B7C8"
    };

    public bool CanStart => Status != ServerStatus.Running;
    public bool CanStop => Status == ServerStatus.Running;

    // ── Warnings ─────────────────────────────────────────────────────────────
    public IReadOnlyList<string> WarningBadges => BuildWarningBadges();
    public bool HasWarnings => WarningBadges.Count > 0;

    // ── ARK: Survival Ascended specifics ──────────────────────────────────────
    public bool IsArkAsa =>
        Profile.GameId.Equals(ArkSurvivalAscendedServerProfile.GameId, StringComparison.OrdinalIgnoreCase) ||
        Profile.GameId.Equals(ArkSurvivalAscendedServerProfile.LegacyGameId, StringComparison.OrdinalIgnoreCase);

    public bool HasCluster => IsArkAsa && GetClusterEnabled();

    public string ClusterBadge
    {
        get
        {
            if (!HasCluster)
            {
                return string.Empty;
            }

            var cid = GetClusterIdSetting();
            if (string.IsNullOrWhiteSpace(cid))
            {
                return "Cluster";
            }

            return cid.Length > 14 ? cid[..14] + "…" : cid;
        }
    }

    // ── Mutations ─────────────────────────────────────────────────────────────
    public void ToggleExpanded()
    {
        IsExpanded = !IsExpanded;
    }

    public void SetBusy(string text)
    {
        BusyText = text;
        IsBusy = !string.IsNullOrWhiteSpace(text);
    }

    public void RefreshStatus()
    {
        Status = _processService.IsRunning(Profile) ? ServerStatus.Running : Profile.Status;
        OnPropertyChanged(nameof(WarningBadges));
        OnPropertyChanged(nameof(HasWarnings));
    }

    public void ApplyMonitorSnapshot(ServerMonitorSnapshot snapshot)
    {
        Status = snapshot.Status;
        CpuPercent = snapshot.CpuPercent;
        _ramMb = snapshot.RamMb;
        _ramLimitMb = snapshot.RamLimitMb;
        _uptime = snapshot.Uptime;
        PlayerCount = snapshot.PlayerCount;
        Profile.LastStartedAt = snapshot.LastStartedAt;
        Profile.LastStoppedAt = snapshot.LastStoppedAt;

        OnPropertyChanged(nameof(RamPercent));
        OnPropertyChanged(nameof(RamMb));
        OnPropertyChanged(nameof(RamLimitMb));
        OnPropertyChanged(nameof(HasRamLimit));
        OnPropertyChanged(nameof(RamText));
        OnPropertyChanged(nameof(RamDisplayText));
        OnPropertyChanged(nameof(CpuDisplayText));
        OnPropertyChanged(nameof(Uptime));
        OnPropertyChanged(nameof(LastStarted));
        OnPropertyChanged(nameof(LastStopped));
        OnPropertyChanged(nameof(LastRestart));
        OnPropertyChanged(nameof(WarningBadges));
        OnPropertyChanged(nameof(HasWarnings));
    }

    public void RefreshProfileFields()
    {
        OnPropertyChanged(nameof(LastStarted));
        OnPropertyChanged(nameof(LastStopped));
        OnPropertyChanged(nameof(LastBackup));
        OnPropertyChanged(nameof(LastRestart));
        OnPropertyChanged(nameof(BackupStatus));
        OnPropertyChanged(nameof(UpdateStatus));
        OnPropertyChanged(nameof(DiskUsageText));
        OnPropertyChanged(nameof(WarningBadges));
        OnPropertyChanged(nameof(HasWarnings));
        OnPropertyChanged(nameof(HasCluster));
        OnPropertyChanged(nameof(ClusterBadge));
    }

    // ── Private helpers ───────────────────────────────────────────────────────
    private string GetClusterIdSetting()
    {
        foreach (var key in new[] { "Cluster.Id", "ClusterID" })
        {
            if (Profile.Settings.TryGetValue(key, out var val) && !string.IsNullOrWhiteSpace(val))
            {
                return val;
            }
        }

        return string.Empty;
    }

    private bool GetClusterEnabled()
    {
        if (Profile.Settings.TryGetValue("Cluster.Enabled", out var v1) && bool.TryParse(v1, out var b1))
        {
            return b1;
        }

        if (Profile.Settings.TryGetValue("ClusterEnabled", out var v2) && bool.TryParse(v2, out var b2))
        {
            return b2;
        }

        return !string.IsNullOrWhiteSpace(GetClusterIdSetting());
    }

    private string BuildEndpoint()
    {
        var gamePort = Profile.Ports.FirstOrDefault(p => p.Name.Equals("Game", StringComparison.OrdinalIgnoreCase))?.Port;
        var ipAddress = Profile.Settings.TryGetValue("ipAddress", out var savedIp) && !string.IsNullOrWhiteSpace(savedIp)
            ? savedIp
            : "localhost";

        return gamePort.HasValue ? $"{ipAddress}:{gamePort}" : ipAddress;
    }

    private string GetPortText(string name)
    {
        var port = Profile.Ports.FirstOrDefault(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        return port == null ? "Not set" : $"{port.Port}/{port.Protocol}";
    }

    private string BuildDiskUsageText()
    {
        if (string.IsNullOrWhiteSpace(Profile.InstallPath) || !Directory.Exists(Profile.InstallPath))
        {
            return "Install folder missing";
        }

        try
        {
            var root = new DirectoryInfo(Profile.InstallPath);
            var totalBytes = root.EnumerateFiles("*", SearchOption.AllDirectories)
                .Take(10000)
                .Sum(file => file.Length);

            return FormatBytes(totalBytes);
        }
        catch
        {
            return "Unavailable";
        }
    }

    private IReadOnlyList<string> BuildWarningBadges()
    {
        var warnings = new List<string>();
        if (string.IsNullOrWhiteSpace(Profile.InstallPath) || !Directory.Exists(Profile.InstallPath))
        {
            warnings.Add("Missing install folder");
        }

        if (string.IsNullOrWhiteSpace(Profile.ExecutablePath) || !File.Exists(Profile.ExecutablePath))
        {
            warnings.Add("Missing executable");
        }

        if (Profile.Ports.GroupBy(port => port.Port).Any(group => group.Count() > 1))
        {
            warnings.Add("Duplicate profile ports");
        }

        if (Status == ServerStatus.Error)
        {
            warnings.Add("Server error");
        }

        return warnings;
    }

    private static string FormatMb(int mb)
    {
        return mb >= 1024 ? $"{mb / 1024.0:0.0} GB" : $"{mb} MB";
    }

    private static string FormatUptime(TimeSpan uptime)
    {
        if (uptime.TotalDays >= 1)
        {
            return $"{(int)uptime.TotalDays}d {uptime.Hours}h {uptime.Minutes}m";
        }

        if (uptime.TotalHours >= 1)
        {
            return $"{(int)uptime.TotalHours}h {uptime.Minutes}m";
        }

        return $"{Math.Max(0, uptime.Minutes)}m";
    }

    private static string FormatBytes(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB", "TB"];
        var value = (double)Math.Max(0, bytes);
        var unit = 0;
        while (value >= 1024 && unit < units.Length - 1)
        {
            value /= 1024;
            unit++;
        }

        return $"{value:0.##} {units[unit]}";
    }
}
