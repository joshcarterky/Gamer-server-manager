using System.Collections.ObjectModel;
using System.IO;
using GameServerManager.Core.Models;
using GameServerManager.GameProviders;
using GameServerManager.Services;
using GameServerManager.Services.ArkSurvivalAscended;

namespace GameServerManager.App.ViewModels;

public sealed class ArkAsaClusterViewModel : BaseViewModel, IDisposable
{
    private readonly AppDataPaths _paths = new();
    private readonly GameProviderRegistry _providers = GameProviderRegistry.CreateDefault();
    private readonly ArkAsaClusterManager _clusterManager = new();
    private readonly ArkAsaProfileMapper _mapper = new();
    private readonly ServersJsonService _serversJsonService;
    private readonly ServerProcessService _processService;
    private readonly ArkClusterModSyncService _modSync;
    private readonly ArkClusterLogger _logger;

    private string _clusterName = "Main Cluster";
    private string _clusterId = "asa-main-cluster";
    private string _clusterDirectoryOverride;
    private string _selectedMapName = "TheIsland_WP";
    private string _customMapName = string.Empty;
    private int _nextGamePort = 7777;
    private int _nextQueryPort = 27015;
    private int _nextRconPort = 27020;
    private int _maxPlayers = 70;
    private bool _sharedBackupEnabled = true;
    private bool _allowTributeDownloads = true;
    private bool _noTransferFromFiltering;
    private bool _preventDownloadSurvivors;
    private bool _preventDownloadItems;
    private bool _preventDownloadDinos;
    private bool _preventUploadSurvivors;
    private bool _preventUploadItems;
    private bool _preventUploadDinos;
    private string _clusterModIds = string.Empty;
    private string _message = "Create or load an ARK ASA cluster.";
    private bool _isBusy;

    public ArkAsaClusterViewModel()
    {
        _serversJsonService = new ServersJsonService(_paths);
        _processService = new ServerProcessService(_providers, _paths);
        _logger = new ArkClusterLogger(_paths);
        _modSync = new ArkClusterModSyncService(_serversJsonService, _logger);
        _clusterDirectoryOverride = Path.Combine(Environment.CurrentDirectory, "Servers", "ARK_ASA_Cluster");

        KnownMaps = new ObservableCollection<ArkAsaKnownMap>(ArkAsaClusterManager.KnownMaps);
        ClusterMaps = new ObservableCollection<ArkClusterMapCardViewModel>();
        ValidationIssues = new ObservableCollection<ArkClusterIssueViewModel>();
        ModConsistencyIssues = new ObservableCollection<string>();

        AddMapCommand = new RelayCommand(async _ => await AddMapAsync(), () => !_isBusy);
        ValidateCommand = new RelayCommand(async _ => await LoadAsync(), () => !_isBusy);
        SaveSharedRulesCommand = new RelayCommand(async _ => await SaveSharedRulesAsync(), () => !_isBusy);
        StartClusterCommand = new RelayCommand(async _ => await StartClusterAsync(), () => !_isBusy && CanStart);
        StopClusterCommand = new RelayCommand(async _ => await StopClusterAsync(), () => !_isBusy && MapCount > 0);
        RestartClusterCommand = new RelayCommand(async _ => await RestartClusterAsync(), () => !_isBusy && CanStart);
        BackupClusterCommand = new RelayCommand(async _ => await BackupClusterAsync(), () => !_isBusy && MapCount > 0);
        SyncModsToClusterCommand = new RelayCommand(async _ => await SyncModsAsync(), () => !_isBusy && MapCount > 0);
        CheckModConsistencyCommand = new RelayCommand(async _ => await CheckModConsistencyAsync(), () => !_isBusy && MapCount > 0);
        ApplyClusterCommand = new RelayCommand(async _ => await ApplyClusterAsync(), () => !_isBusy && CanStart);

        _ = LoadAsync();
    }

    public ObservableCollection<ArkAsaKnownMap> KnownMaps { get; }
    public ObservableCollection<ArkClusterMapCardViewModel> ClusterMaps { get; }
    public ObservableCollection<ArkClusterIssueViewModel> ValidationIssues { get; }
    public ObservableCollection<string> ModConsistencyIssues { get; }

    public RelayCommand AddMapCommand { get; }
    public RelayCommand ValidateCommand { get; }
    public RelayCommand SaveSharedRulesCommand { get; }
    public RelayCommand StartClusterCommand { get; }
    public RelayCommand StopClusterCommand { get; }
    public RelayCommand RestartClusterCommand { get; }
    public RelayCommand BackupClusterCommand { get; }
    public RelayCommand SyncModsToClusterCommand { get; }
    public RelayCommand CheckModConsistencyCommand { get; }
    public RelayCommand ApplyClusterCommand { get; }

    public string ClusterName { get => _clusterName; set => SetProperty(ref _clusterName, value); }
    public string ClusterId { get => _clusterId; set => SetProperty(ref _clusterId, value); }
    public string ClusterDirectoryOverride { get => _clusterDirectoryOverride; set => SetProperty(ref _clusterDirectoryOverride, value); }
    public string SelectedMapName { get => _selectedMapName; set => SetProperty(ref _selectedMapName, value); }
    public string CustomMapName { get => _customMapName; set => SetProperty(ref _customMapName, value); }
    public int NextGamePort { get => _nextGamePort; set => SetProperty(ref _nextGamePort, value); }
    public int NextQueryPort { get => _nextQueryPort; set => SetProperty(ref _nextQueryPort, value); }
    public int NextRconPort { get => _nextRconPort; set => SetProperty(ref _nextRconPort, value); }
    public int MaxPlayers { get => _maxPlayers; set => SetProperty(ref _maxPlayers, value); }
    public bool SharedBackupEnabled { get => _sharedBackupEnabled; set => SetProperty(ref _sharedBackupEnabled, value); }
    public bool AllowTributeDownloads { get => _allowTributeDownloads; set => SetProperty(ref _allowTributeDownloads, value); }
    public bool NoTransferFromFiltering { get => _noTransferFromFiltering; set => SetProperty(ref _noTransferFromFiltering, value); }
    public bool PreventDownloadSurvivors { get => _preventDownloadSurvivors; set => SetProperty(ref _preventDownloadSurvivors, value); }
    public bool PreventDownloadItems { get => _preventDownloadItems; set => SetProperty(ref _preventDownloadItems, value); }
    public bool PreventDownloadDinos { get => _preventDownloadDinos; set => SetProperty(ref _preventDownloadDinos, value); }
    public bool PreventUploadSurvivors { get => _preventUploadSurvivors; set => SetProperty(ref _preventUploadSurvivors, value); }
    public bool PreventUploadItems { get => _preventUploadItems; set => SetProperty(ref _preventUploadItems, value); }
    public bool PreventUploadDinos { get => _preventUploadDinos; set => SetProperty(ref _preventUploadDinos, value); }

    public string ClusterModIds
    {
        get => _clusterModIds;
        set => SetProperty(ref _clusterModIds, value);
    }

    public string Message
    {
        get => _message;
        set => SetProperty(ref _message, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetProperty(ref _isBusy, value))
                NotifyCommandsCanExecute();
        }
    }

    public int MapCount => ClusterMaps.Count;
    public int ErrorCount => ValidationIssues.Count(issue => issue.Severity == "Error");
    public int WarningCount => ValidationIssues.Count(issue => issue.Severity == "Warning");
    public int ModIssueCount => ModConsistencyIssues.Count;
    public bool HasModIssues => ModConsistencyIssues.Count > 0;
    public bool ModsAreConsistent => ModConsistencyIssues.Count == 0 && MapCount > 0;
    public bool CanStart => ErrorCount == 0 && MapCount > 0;
    public string ClusterLogPath => _logger.ClusterLogPath;
    public string ModsLogPath => _logger.ModsLogPath;

    private async Task LoadAsync()
    {
        IsBusy = true;
        try
        {
            var profiles = await _serversJsonService.LoadServersAsync();
            var dashboard = _clusterManager.BuildDashboard(profiles);

            ClusterMaps.Clear();
            foreach (var map in dashboard.Maps)
                ClusterMaps.Add(new ArkClusterMapCardViewModel(map.Profile, map.Ark, _processService));

            if (!string.IsNullOrWhiteSpace(dashboard.ClusterId))
                ClusterId = dashboard.ClusterId;

            if (!string.IsNullOrWhiteSpace(dashboard.ClusterDirectoryOverride))
                ClusterDirectoryOverride = dashboard.ClusterDirectoryOverride;

            ApplyNextPorts();
            ApplyValidation(dashboard.Validation);
            Message = MapCount == 0
                ? "No cluster maps yet. Add Island or another map to start."
                : $"Loaded {MapCount} cluster map(s).";

            if (MapCount > 1)
                await RefreshModConsistencyAsync();
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task AddMapAsync()
    {
        IsBusy = true;
        try
        {
            var selected = KnownMaps.FirstOrDefault(map => map.InternalName.Equals(SelectedMapName, StringComparison.OrdinalIgnoreCase))
                           ?? KnownMaps.First();
            var mapName = selected.InternalName.Equals("Custom", StringComparison.OrdinalIgnoreCase)
                ? CustomMapName.Trim()
                : selected.InternalName;
            if (string.IsNullOrWhiteSpace(mapName))
            {
                Message = "Enter a custom map name before adding it.";
                return;
            }

            var display = selected.InternalName.Equals("Custom", StringComparison.OrdinalIgnoreCase) ? mapName : selected.DisplayName;
            var request = new ArkAsaClusterMapRequest(
                ClusterName, ClusterId, ClusterDirectoryOverride,
                display, selected.InternalName, mapName,
                selected.DefaultAltSaveDirectoryName,
                Path.Combine(Environment.CurrentDirectory, "Servers", "ARK_Survival_Ascended", Sanitize(display)),
                NextGamePort, NextQueryPort, NextRconPort, MaxPlayers, SharedBackupEnabled,
                NoTransferFromFiltering, PreventDownloadSurvivors, PreventDownloadItems, PreventDownloadDinos,
                PreventUploadSurvivors, PreventUploadItems, PreventUploadDinos, AllowTributeDownloads);

            await _serversJsonService.AddServerAsync(_clusterManager.CreateClusterMapProfile(request));
            await _logger.LogClusterAsync($"Added map '{display}' to cluster '{ClusterName}'", $"ClusterID: {ClusterId}");
            Message = $"Added {display} to {ClusterName}.";
            await LoadAsync();
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task SaveSharedRulesAsync()
    {
        var profiles = (await _serversJsonService.LoadServersAsync()).ToList();
        foreach (var profile in profiles.Where(IsClusterProfile))
        {
            profile.Settings["ClusterID"] = ClusterId;
            profile.Settings["ClusterDirOverride"] = ClusterDirectoryOverride;
            profile.Settings["SharedClusterFolder"] = ClusterDirectoryOverride;
            profile.Settings["ClusterMapGroup"] = ClusterName;
            profile.Settings["NoTransferFromFiltering"] = NoTransferFromFiltering.ToString();
            profile.Settings["PreventDownloadSurvivors"] = PreventDownloadSurvivors.ToString();
            profile.Settings["PreventDownloadItems"] = PreventDownloadItems.ToString();
            profile.Settings["PreventDownloadDinos"] = PreventDownloadDinos.ToString();
            profile.Settings["PreventUploadSurvivors"] = PreventUploadSurvivors.ToString();
            profile.Settings["PreventUploadItems"] = PreventUploadItems.ToString();
            profile.Settings["PreventUploadDinos"] = PreventUploadDinos.ToString();
            profile.Settings["AllowTributeDownloads"] = AllowTributeDownloads.ToString();
            profile.Settings["SharedBackup"] = SharedBackupEnabled.ToString();
            profile.AutoBackupEnabled = SharedBackupEnabled;
            await _serversJsonService.UpdateServerAsync(profile);
        }

        await _logger.LogClusterAsync($"Saved shared rules for cluster '{ClusterName}'", $"ClusterID: {ClusterId}");
        Message = "Shared cluster rules applied to every map.";
        await LoadAsync();
    }

    private async Task SyncModsAsync()
    {
        IsBusy = true;
        try
        {
            var modIds = ParseModIds(_clusterModIds);
            if (modIds.Count == 0)
            {
                Message = "Enter at least one mod ID in the Cluster Mods field before syncing.";
                return;
            }

            var result = await _modSync.SyncModsToClusterAsync(ClusterId, modIds);
            Message = result.Message;

            if (result.HasChanges)
                await RefreshModConsistencyAsync();
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task CheckModConsistencyAsync()
    {
        IsBusy = true;
        try
        {
            await RefreshModConsistencyAsync();
            Message = ModsAreConsistent
                ? $"All {MapCount} maps have consistent mod lists."
                : $"Found {ModIssueCount} mod inconsistency issue(s).";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task ApplyClusterAsync()
    {
        IsBusy = true;
        try
        {
            await _logger.LogClusterAsync($"Cluster apply started for '{ClusterName}'", $"ClusterID: {ClusterId}");

            // Step 1: Validate
            Message = "Step 1/5: Validating cluster...";
            var profiles = (await _serversJsonService.LoadServersAsync()).ToList();
            var clusterCount = profiles.Count(IsClusterProfile);
            if (clusterCount == 0)
            {
                Message = "No cluster maps found. Add maps before applying.";
                return;
            }

            // Step 2: Save shared rules
            Message = "Step 2/5: Applying shared cluster rules...";
            foreach (var profile in profiles.Where(IsClusterProfile))
            {
                profile.Settings["ClusterID"] = ClusterId;
                profile.Settings["ClusterDirOverride"] = ClusterDirectoryOverride;
                profile.Settings["SharedClusterFolder"] = ClusterDirectoryOverride;
                profile.Settings["ClusterMapGroup"] = ClusterName;
                profile.Settings["NoTransferFromFiltering"] = NoTransferFromFiltering.ToString();
                profile.Settings["PreventDownloadSurvivors"] = PreventDownloadSurvivors.ToString();
                profile.Settings["PreventDownloadItems"] = PreventDownloadItems.ToString();
                profile.Settings["PreventDownloadDinos"] = PreventDownloadDinos.ToString();
                profile.Settings["PreventUploadSurvivors"] = PreventUploadSurvivors.ToString();
                profile.Settings["PreventUploadItems"] = PreventUploadItems.ToString();
                profile.Settings["PreventUploadDinos"] = PreventUploadDinos.ToString();
                profile.Settings["AllowTributeDownloads"] = AllowTributeDownloads.ToString();
                profile.Settings["SharedBackup"] = SharedBackupEnabled.ToString();
                profile.AutoBackupEnabled = SharedBackupEnabled;
                await _serversJsonService.UpdateServerAsync(profile);
            }
            await _logger.LogClusterAsync("Step 2: Shared rules saved.", $"Maps updated: {clusterCount}");

            // Step 3: Sync cluster-wide mods
            Message = "Step 3/5: Syncing cluster mods...";
            var modIds = ParseModIds(_clusterModIds);
            if (modIds.Count > 0)
            {
                var syncResult = await _modSync.SyncModsToClusterAsync(ClusterId, modIds);
                await _logger.LogClusterAsync("Step 3: Mod sync complete.", syncResult.Message);
            }
            else
            {
                await _logger.LogClusterAsync("Step 3: No cluster mods configured.");
            }

            // Step 4: Ensure cluster directory exists
            Message = "Step 4/5: Ensuring cluster directory...";
            if (!string.IsNullOrWhiteSpace(ClusterDirectoryOverride))
            {
                try
                {
                    Directory.CreateDirectory(ClusterDirectoryOverride);
                    await _logger.LogClusterAsync("Step 4: Cluster directory ready.", ClusterDirectoryOverride);
                }
                catch (Exception ex)
                {
                    await _logger.LogClusterAsync("Step 4: Could not create cluster directory.", ex.Message);
                }
            }

            // Step 5: Refresh & report
            Message = "Step 5/5: Refreshing cluster status...";
            await LoadAsync();

            Message = $"Cluster '{ClusterName}' applied — {MapCount} map(s) ready.";
            await _logger.LogClusterAsync($"Cluster apply complete for '{ClusterName}'.",
                $"{MapCount} maps, {ModIssueCount} mod issue(s).");
        }
        catch (Exception ex)
        {
            Message = $"Cluster apply failed: {ex.Message}";
            await _logger.LogClusterAsync("Cluster apply FAILED.", ex.ToString());
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task StartClusterAsync()
    {
        if (!await ValidateBeforeActionAsync()) return;

        IsBusy = true;
        try
        {
            foreach (var map in ClusterMaps)
            {
                await _processService.StartServerAsync(map.Profile);
                await _serversJsonService.UpdateServerAsync(map.Profile);
            }

            await _logger.LogClusterAsync($"Started cluster '{ClusterName}'.", $"{ClusterMaps.Count} map(s) started.");
            Message = $"Started {ClusterMaps.Count} cluster map(s).";
            await LoadAsync();
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task StopClusterAsync()
    {
        IsBusy = true;
        try
        {
            foreach (var map in ClusterMaps)
            {
                await _processService.StopServerAsync(map.Profile);
                await _serversJsonService.UpdateServerAsync(map.Profile);
            }

            await _logger.LogClusterAsync($"Stopped cluster '{ClusterName}'.", $"{ClusterMaps.Count} map(s) stopped.");
            Message = $"Stopped {ClusterMaps.Count} cluster map(s).";
            await LoadAsync();
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task RestartClusterAsync()
    {
        if (!await ValidateBeforeActionAsync()) return;

        IsBusy = true;
        try
        {
            foreach (var map in ClusterMaps)
            {
                await _processService.RestartServerAsync(map.Profile);
                await _serversJsonService.UpdateServerAsync(map.Profile);
            }

            await _logger.LogClusterAsync($"Restarted cluster '{ClusterName}'.", $"{ClusterMaps.Count} map(s) restarted.");
            Message = $"Restarted {ClusterMaps.Count} cluster map(s).";
            await LoadAsync();
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task BackupClusterAsync()
    {
        IsBusy = true;
        try
        {
            var backupService = new ArkAsaBackupService();
            foreach (var map in ClusterMaps)
            {
                var ark = _mapper.FromServerProfile(map.Profile);
                await backupService.CreateBackupAsync(ark, $"Cluster backup: {ClusterName}");
                map.Profile.LastBackupAt = DateTime.UtcNow;
                await _serversJsonService.UpdateServerAsync(map.Profile);
            }

            await _logger.LogClusterAsync($"Backed up cluster '{ClusterName}'.", $"{ClusterMaps.Count} map(s) backed up.");
            Message = $"Backed up {ClusterMaps.Count} cluster map(s).";
            await LoadAsync();
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task<bool> ValidateBeforeActionAsync()
    {
        await SaveSharedRulesAsync();
        if (!CanStart)
        {
            Message = "Fix cluster validation errors before starting.";
            return false;
        }

        return true;
    }

    private async Task RefreshModConsistencyAsync()
    {
        var report = await _modSync.CheckConsistencyAsync(ClusterId);
        ModConsistencyIssues.Clear();
        foreach (var issue in report.Issues)
            ModConsistencyIssues.Add(issue);

        OnPropertyChanged(nameof(ModIssueCount));
        OnPropertyChanged(nameof(HasModIssues));
        OnPropertyChanged(nameof(ModsAreConsistent));
    }

    private void ApplyValidation(ArkClusterValidationReport report)
    {
        ValidationIssues.Clear();
        foreach (var issue in report.Issues)
            ValidationIssues.Add(new ArkClusterIssueViewModel(issue));

        OnPropertyChanged(nameof(MapCount));
        OnPropertyChanged(nameof(ErrorCount));
        OnPropertyChanged(nameof(WarningCount));
        OnPropertyChanged(nameof(CanStart));
        NotifyCommandsCanExecute();
    }

    private void NotifyCommandsCanExecute()
    {
        StartClusterCommand.NotifyCanExecuteChanged();
        StopClusterCommand.NotifyCanExecuteChanged();
        RestartClusterCommand.NotifyCanExecuteChanged();
        BackupClusterCommand.NotifyCanExecuteChanged();
        SyncModsToClusterCommand.NotifyCanExecuteChanged();
        CheckModConsistencyCommand.NotifyCanExecuteChanged();
        ApplyClusterCommand.NotifyCanExecuteChanged();
        AddMapCommand.NotifyCanExecuteChanged();
        ValidateCommand.NotifyCanExecuteChanged();
        SaveSharedRulesCommand.NotifyCanExecuteChanged();
    }

    private void ApplyNextPorts()
    {
        var usedPorts = ClusterMaps.SelectMany(map => new[] { map.GamePort, map.QueryPort, map.RconPort }).ToHashSet();
        NextGamePort = NextFree(7777, usedPorts);
        NextQueryPort = NextFree(27015, usedPorts);
        NextRconPort = NextFree(27020, usedPorts);
    }

    private static IReadOnlyList<string> ParseModIds(string input)
    {
        return input.Split(new[] { ',', ' ', '\n', '\r', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(id => long.TryParse(id, out _))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static int NextFree(int start, HashSet<int> used)
    {
        var port = start;
        while (used.Contains(port)) port++;
        return port;
    }

    private static bool IsClusterProfile(ServerProfile profile)
    {
        return (profile.GameId.Equals(ArkSurvivalAscendedServerProfile.GameId, StringComparison.OrdinalIgnoreCase) ||
                profile.GameId.Equals(ArkSurvivalAscendedServerProfile.LegacyGameId, StringComparison.OrdinalIgnoreCase)) &&
               profile.Settings.TryGetValue("ClusterEnabled", out var enabled) &&
               bool.TryParse(enabled, out var parsed) && parsed;
    }

    private static string Sanitize(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        return new string(value.Select(ch => invalid.Contains(ch) || char.IsWhiteSpace(ch) ? '_' : ch).ToArray());
    }

    public void Dispose() => _processService.Dispose();
}

public sealed class ArkClusterMapCardViewModel : BaseViewModel
{
    private readonly ServerProcessService _processService;

    public ArkClusterMapCardViewModel(ServerProfile profile, ArkSurvivalAscendedServerProfile ark, ServerProcessService processService)
    {
        Profile = profile;
        Ark = ark;
        _processService = processService;
    }

    public ServerProfile Profile { get; }
    public ArkSurvivalAscendedServerProfile Ark { get; }
    public string DisplayName => Ark.Basic.ServerName;
    public string MapName => Ark.Basic.MapName;
    public string AltSaveDirectoryName => Ark.Basic.AltSaveDirectoryName;
    public int GamePort => Ark.Network.GamePort;
    public int QueryPort => Ark.Network.QueryPort;
    public int RconPort => Ark.Network.RCONPort;
    public string ClusterId => Ark.Cluster.ClusterID;
    public string ClusterDirectoryOverride => Ark.Cluster.ClusterDirectoryOverride;
    public bool IsRunning => _processService.IsRunning(Profile);
    public string StatusText => IsRunning ? "Running" : Profile.Status.ToString();
    public string StatusColor => IsRunning ? "#2ECC71" : "#A8B7C8";
    public string LastBackup => Profile.LastBackupAt?.ToLocalTime().ToString("g") ?? "Never";
}

public sealed class ArkClusterIssueViewModel
{
    public ArkClusterIssueViewModel(ArkClusterValidationIssue issue)
    {
        Severity = issue.Severity.ToString();
        Message = issue.Message;
        Color = issue.Severity == ArkClusterIssueSeverity.Error ? "#FF6969" : "#F7B267";
    }

    public string Severity { get; }
    public string Message { get; }
    public string Color { get; }
}
