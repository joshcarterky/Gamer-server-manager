using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Threading;
using GameServerManager.App.Views;
using GameServerManager.Core.Models;
using GameServerManager.GameProviders;
using GameServerManager.Services;
using Microsoft.Win32;

namespace GameServerManager.App.ViewModels;

public sealed class GameFilterOption
{
    public GameFilterOption(string displayName, string? gameId = null)
    {
        DisplayName = displayName;
        GameId = gameId;
    }

    public string DisplayName { get; }
    public string? GameId { get; }

    public override string ToString()
    {
        return DisplayName;
    }
}

public sealed class StatusFilterOption
{
    public StatusFilterOption(string displayName, params ServerStatus[] statuses)
    {
        DisplayName = displayName;
        Statuses = statuses;
    }

    public string DisplayName { get; }
    public IReadOnlyList<ServerStatus> Statuses { get; }

    public override string ToString()
    {
        return DisplayName;
    }
}

public class ServersViewModel : BaseViewModel, IDisposable
{
    private readonly GameProviderRegistry _providers;
    private readonly ServersJsonService _serversJsonService;
    private readonly ServerProcessService _processService;
    private readonly ServerMonitorService _monitorService;
    private readonly BackupService _backupService;
    private readonly ServerImportDetector _importDetector;
    private readonly ServerProfileValidator _profileValidator;
    private readonly DispatcherTimer _refreshTimer;
    private bool _isMonitoringRefreshRunning;
    private string _searchText = string.Empty;
    private GameFilterOption? _filterProvider;
    private StatusFilterOption? _filterStatus;
    private string _message = string.Empty;
    private bool _isAddServerOpen;
    private bool _isConsoleOpen;
    private string _consoleTitle = "Console";
    private string _consoleText = string.Empty;
    private ServerCardViewModel? _consoleServer;

    public ServersViewModel()
    {
        _providers = GameProviderRegistry.CreateDefault();
        var paths = new AppDataPaths();
        _serversJsonService = new ServersJsonService(paths);
        _processService = new ServerProcessService(_providers, paths);
        _monitorService = new ServerMonitorService(_processService, new ServerQueryService());
        _backupService = new BackupService(paths);
        _importDetector = new ServerImportDetector(_providers);
        _profileValidator = new ServerProfileValidator();
        AddServer = new AddServerWizardViewModel(_providers.Providers);

        ProviderFilters = new ObservableCollection<GameFilterOption>(
            new[] { new GameFilterOption("All Games") }
                .Concat(_providers.Providers
                    .OrderBy(p => p.GameName)
                    .Select(p => new GameFilterOption(p.GameName, p.GameId))));
        StatusFilters = new ObservableCollection<StatusFilterOption>
        {
            new("All"),
            new("Online", ServerStatus.Running),
            new("Offline", ServerStatus.Stopped, ServerStatus.Unknown),
            new("Starting", ServerStatus.Starting),
            new("Stopping", ServerStatus.Stopping),
            new("Updating", ServerStatus.Updating),
            new("Error", ServerStatus.Error)
        };
        _filterProvider = ProviderFilters.FirstOrDefault();
        _filterStatus = StatusFilters.FirstOrDefault();

        RefreshCommand = new RelayCommand(async _ => await LoadAsync());
        ImportServerCommand = new RelayCommand(async _ => await ImportServerAsync());
        SaveNewServerCommand = new RelayCommand(async _ => await SaveNewServerAsync());
        ToggleAddServerCommand = new RelayCommand(_ => ToggleAddServer());
        NextAddServerStepCommand = new RelayCommand(_ => AddServer.NextStep());
        PreviousAddServerStepCommand = new RelayCommand(_ => AddServer.PreviousStep());
        StartCommand = new RelayCommand(async profile => await StartAsync(profile as ServerCardViewModel));
        StopCommand = new RelayCommand(async profile => await StopAsync(profile as ServerCardViewModel));
        RestartCommand = new RelayCommand(async profile => await RestartAsync(profile as ServerCardViewModel));
        ToggleCardCommand = new RelayCommand(profile => ToggleCard(profile as ServerCardViewModel));
        UpdateServerCommand = new RelayCommand(profile => UpdateServer(profile as ServerCardViewModel));
        OpenFilesCommand = new RelayCommand(profile => OpenFiles(profile as ServerCardViewModel));
        OpenSettingsCommand = new RelayCommand(profile => OpenSettings(profile as ServerCardViewModel));
        MoreOptionsCommand = new RelayCommand(profile => MoreOptions(profile as ServerCardViewModel));
        ToggleFavoriteCommand = new RelayCommand(async profile => await ToggleFavoriteAsync(profile as ServerCardViewModel));
        EditCommand = new RelayCommand(profile => Edit(profile as ServerCardViewModel));
        DeleteCommand = new RelayCommand(async profile => await DeleteAsync(profile as ServerCardViewModel));
        OpenFolderCommand = new RelayCommand(profile => OpenFolder(profile as ServerCardViewModel));
        ViewConsoleCommand = new RelayCommand(profile => ViewConsole(profile as ServerCardViewModel));
        BackupNowCommand = new RelayCommand(async profile => await BackupNowAsync(profile as ServerCardViewModel));
        CloseConsoleCommand = new RelayCommand(_ => IsConsoleOpen = false);
        RefreshConsoleCommand = new RelayCommand(_ => RefreshConsoleText());
        BrowseServerPathCommand = new RelayCommand(_ => BrowseFolder(path => AddServer.ServerPath = path, AddServer.ServerPath));
        BrowseInstallPathCommand = new RelayCommand(_ => BrowseFolder(path => AddServer.InstallPath = path, AddServer.InstallPath));
        BrowseExecutablePathCommand = new RelayCommand(_ => BrowseExecutable(path => AddServer.ExecutablePath = path, AddServer.ExecutablePath));
        BrowseSaveDirectoryCommand = new RelayCommand(_ => BrowseFolder(path => AddServer.SaveDirectory = path, AddServer.SaveDirectory));
        BrowseBackupDirectoryCommand = new RelayCommand(_ => BrowseFolder(path => AddServer.BackupDirectory = path, AddServer.BackupDirectory));

        _refreshTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
        _refreshTimer.Tick += async (_, _) => await RefreshMonitoringAsync();
        _refreshTimer.Start();

        _ = LoadAsync();
    }

    public ObservableCollection<ServerCardViewModel> Servers { get; } = new();
    public ObservableCollection<ServerCardViewModel> FilteredServers { get; } = new();
    public ObservableCollection<GameFilterOption> ProviderFilters { get; }
    public ObservableCollection<StatusFilterOption> StatusFilters { get; }
    public AddServerWizardViewModel AddServer { get; }

    public RelayCommand RefreshCommand { get; }
    public RelayCommand ImportServerCommand { get; }
    public RelayCommand SaveNewServerCommand { get; }
    public RelayCommand ToggleAddServerCommand { get; }
    public RelayCommand NextAddServerStepCommand { get; }
    public RelayCommand PreviousAddServerStepCommand { get; }
    public RelayCommand StartCommand { get; }
    public RelayCommand StopCommand { get; }
    public RelayCommand RestartCommand { get; }
    public RelayCommand ToggleCardCommand { get; }
    public RelayCommand UpdateServerCommand { get; }
    public RelayCommand OpenFilesCommand { get; }
    public RelayCommand OpenSettingsCommand { get; }
    public RelayCommand MoreOptionsCommand { get; }
    public RelayCommand ToggleFavoriteCommand { get; }
    public RelayCommand EditCommand { get; }
    public RelayCommand DeleteCommand { get; }
    public RelayCommand OpenFolderCommand { get; }
    public RelayCommand ViewConsoleCommand { get; }
    public RelayCommand BackupNowCommand { get; }
    public RelayCommand CloseConsoleCommand { get; }
    public RelayCommand RefreshConsoleCommand { get; }
    public RelayCommand BrowseServerPathCommand { get; }
    public RelayCommand BrowseInstallPathCommand { get; }
    public RelayCommand BrowseExecutablePathCommand { get; }
    public RelayCommand BrowseSaveDirectoryCommand { get; }
    public RelayCommand BrowseBackupDirectoryCommand { get; }

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetProperty(ref _searchText, value))
            {
                ApplyFilters();
            }
        }
    }

    public GameFilterOption? FilterProvider
    {
        get => _filterProvider;
        set
        {
            if (SetProperty(ref _filterProvider, value))
            {
                ApplyFilters();
            }
        }
    }

    public StatusFilterOption? FilterStatus
    {
        get => _filterStatus;
        set
        {
            if (SetProperty(ref _filterStatus, value))
            {
                ApplyFilters();
            }
        }
    }

    public string Message
    {
        get => _message;
        set => SetProperty(ref _message, value);
    }

    public bool IsEmpty => Servers.Count == 0;
    public bool HasServers => !IsEmpty;

    public bool IsAddServerOpen
    {
        get => _isAddServerOpen;
        set => SetProperty(ref _isAddServerOpen, value);
    }

    public bool IsConsoleOpen
    {
        get => _isConsoleOpen;
        set => SetProperty(ref _isConsoleOpen, value);
    }

    public string ConsoleTitle
    {
        get => _consoleTitle;
        set => SetProperty(ref _consoleTitle, value);
    }

    public string ConsoleText
    {
        get => _consoleText;
        set => SetProperty(ref _consoleText, value);
    }

    private async Task LoadAsync()
    {
        Servers.Clear();
        try
        {
            var profiles = await _serversJsonService.LoadServersAsync();
            foreach (var profile in profiles)
            {
                if (_providers.TryGetProvider(profile.GameId, out var provider))
                {
                    NormalizeProfile(profile);
                    Servers.Add(new ServerCardViewModel(profile, provider, _processService));
                }
            }

            ApplyFilters();
            NotifyServerCollectionChanged();
            Message = Servers.Count == 0
                ? $"No servers added yet. Add entries to {_serversJsonService.ServersJsonPath}."
                : $"Loaded {Servers.Count} server(s) from {_serversJsonService.ServersJsonPath}.";
        }
        catch (Exception ex)
        {
            ApplyFilters();
            NotifyServerCollectionChanged();
            Message = ex.Message;
        }
    }

    private async Task SaveNewServerAsync()
    {
        try
        {
            var profile = AddServer.CreateProfile();
            _profileValidator.Validate(profile);
            if (AddServer.IsEditMode)
            {
                await _serversJsonService.UpdateServerAsync(profile);
                Message = $"Saved changes to {profile.ServerName}.";
            }
            else
            {
                await _serversJsonService.AddServerAsync(profile);
                Message = $"Created {profile.ServerName}.";
            }

            IsAddServerOpen = false;
            AddServer.Reset();
            await LoadAsync();
        }
        catch (Exception ex)
        {
            Message = $"Create failed: {ex.Message}";
        }
    }

    private async Task ImportServerAsync()
    {
        try
        {
            var dialog = new OpenFolderDialog
            {
                Title = "Select existing server folder",
                Multiselect = false
            };

            if (dialog.ShowDialog() != true)
            {
                Message = "Import cancelled.";
                return;
            }

            var profile = _importDetector.Detect(dialog.FolderName);
            await _serversJsonService.AddServerAsync(profile);
            Message = $"Imported {profile.ServerName} as {GetGameName(profile.GameId)}.";
            await LoadAsync();
        }
        catch (Exception ex)
        {
            Message = $"Import failed: {ex.Message}";
        }
    }

    private void ToggleAddServer()
    {
        IsAddServerOpen = !IsAddServerOpen;
        if (IsAddServerOpen)
        {
            AddServer.Reset();
            Message = "Add Server wizard opened.";
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
            server.SetBusy("Starting...");
            server.Profile.Status = ServerStatus.Starting;
            await _processService.StartServerAsync(server.Profile);
            await _serversJsonService.UpdateServerAsync(server.Profile);
            await RefreshMonitoringAsync();
            server.RefreshProfileFields();
            Message = $"Started {server.ProfileName}.";
        }
        catch (Exception ex)
        {
            Message = $"Start failed: {ex.Message}";
        }
        finally
        {
            server.SetBusy(string.Empty);
        }
    }

    private async Task StopAsync(ServerCardViewModel? server)
    {
        if (server == null)
        {
            return;
        }

        if (MessageBox.Show(
                $"Stop '{server.ServerName}'?",
                "Stop Server",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning) != MessageBoxResult.Yes)
        {
            Message = "Stop cancelled.";
            return;
        }

        try
        {
            server.SetBusy("Stopping...");
            server.Profile.Status = ServerStatus.Stopping;
            await _processService.StopServerAsync(server.Profile);
            await _serversJsonService.UpdateServerAsync(server.Profile);
            await RefreshMonitoringAsync();
            Message = $"Stopped {server.ProfileName}.";
        }
        catch (Exception ex)
        {
            Message = $"Stop failed: {ex.Message}";
        }
        finally
        {
            server.SetBusy(string.Empty);
        }
    }

    private async Task RestartAsync(ServerCardViewModel? server)
    {
        if (server == null)
        {
            return;
        }

        if (MessageBox.Show(
                $"Restart '{server.ServerName}'?",
                "Restart Server",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning) != MessageBoxResult.Yes)
        {
            Message = "Restart cancelled.";
            return;
        }

        try
        {
            server.SetBusy("Restarting...");
            server.Profile.Status = ServerStatus.Restarting;
            await _processService.RestartServerAsync(server.Profile);
            await _serversJsonService.UpdateServerAsync(server.Profile);
            await RefreshMonitoringAsync();
            Message = $"Restarted {server.ProfileName}.";
        }
        catch (Exception ex)
        {
            Message = $"Restart failed: {ex.Message}";
        }
        finally
        {
            server.SetBusy(string.Empty);
        }
    }

    private void ToggleCard(ServerCardViewModel? server)
    {
        server?.ToggleExpanded();
    }

    private void UpdateServer(ServerCardViewModel? server)
    {
        if (server == null)
        {
            return;
        }

        MessageBox.Show(
            "Not implemented yet: the update manager will run provider-specific updates and show progress here.",
            "Update Manager",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
        Message = $"Update manager is not implemented yet for {server.ServerName}.";
    }

    private void OpenFiles(ServerCardViewModel? server)
    {
        OpenFolder(server);
        if (server != null)
        {
            Message = $"Opened files for {server.ServerName}.";
        }
    }

    private void OpenSettings(ServerCardViewModel? server)
    {
        if (server == null)
        {
            return;
        }

        if (server.Profile.GameId.Equals(ArkSurvivalAscendedServerProfile.GameId, StringComparison.OrdinalIgnoreCase) ||
            server.Profile.GameId.Equals(ArkSurvivalAscendedServerProfile.LegacyGameId, StringComparison.OrdinalIgnoreCase))
        {
            var shell = Application.Current.Windows.OfType<Shell>().FirstOrDefault();
            if (shell != null)
            {
                shell.OpenArkAsaSettings(server.Profile);
                Message = $"Opened ARK ASA settings for {server.ServerName}.";
                return;
            }
        }

        MessageBox.Show(
            "Not implemented yet: per-server settings will open the generated settings editor for this profile.",
            "Server Settings",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
        Message = $"Per-server settings are not implemented yet for {server.ServerName}.";
    }

    private void MoreOptions(ServerCardViewModel? server)
    {
        if (server == null)
        {
            return;
        }

        MessageBox.Show(
            "Not implemented yet: advanced server actions will appear in this menu.",
            "More Options",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
        Message = $"More options are not implemented yet for {server.ServerName}.";
    }

    private async Task ToggleFavoriteAsync(ServerCardViewModel? server)
    {
        if (server == null)
        {
            return;
        }

        server.IsFavorite = !server.IsFavorite;
        await _serversJsonService.UpdateServerAsync(server.Profile);
        ApplyFilters();
        Message = server.IsFavorite
            ? $"{server.ServerName} added to favorites."
            : $"{server.ServerName} removed from favorites.";
    }

    private async Task DeleteAsync(ServerCardViewModel? server)
    {
        if (server == null)
        {
            return;
        }

        var result = MessageBox.Show(
            $"Delete '{server.ServerName}' from servers.json?",
            "Delete Server",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result != MessageBoxResult.Yes)
        {
            Message = "Delete cancelled.";
            return;
        }

        await _serversJsonService.DeleteServerAsync(server.Profile.Id);
        Servers.Remove(server);
        ApplyFilters();
        NotifyServerCollectionChanged();
        Message = $"Deleted {server.ServerName}.";
    }

    private void Edit(ServerCardViewModel? server)
    {
        if (server == null)
        {
            return;
        }

        AddServer.LoadFromProfile(server.Profile, server.Provider);
        IsAddServerOpen = true;
        Message = $"Editing {server.ServerName}.";
    }

    private void OpenFolder(ServerCardViewModel? server)
    {
        if (server == null)
        {
            return;
        }

        Directory.CreateDirectory(server.Profile.InstallPath);
        Process.Start(new ProcessStartInfo
        {
            FileName = server.Profile.InstallPath,
            UseShellExecute = true
        });
    }

    private void ViewConsole(ServerCardViewModel? server)
    {
        if (server == null)
        {
            return;
        }

        _consoleServer = server;
        ConsoleTitle = $"{server.ServerName} Console";
        RefreshConsoleText();
        IsConsoleOpen = true;
    }

    private async Task BackupNowAsync(ServerCardViewModel? server)
    {
        if (server == null)
        {
            return;
        }

        if (MessageBox.Show(
                $"Create a backup for '{server.ServerName}' now?",
                "Backup Server",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question) != MessageBoxResult.Yes)
        {
            Message = "Backup cancelled.";
            return;
        }

        try
        {
            server.SetBusy("Backing up...");
            var backupPath = await _backupService.BackupServerAsync(server.Profile);
            server.Profile.LastBackupAt = DateTime.UtcNow;
            server.Profile.AutoBackupEnabled = true;
            await _serversJsonService.UpdateServerAsync(server.Profile);
            server.RefreshProfileFields();
            Message = $"Backup created: {backupPath}";
        }
        catch (Exception ex)
        {
            Message = $"Backup failed: {ex.Message}";
        }
        finally
        {
            server.SetBusy(string.Empty);
        }
    }

    private async Task RefreshMonitoringAsync()
    {
        if (_isMonitoringRefreshRunning)
        {
            return;
        }

        _isMonitoringRefreshRunning = true;
        try
        {
            var servers = Servers.ToList();
            var snapshots = await Task.WhenAll(servers.Select(server => _monitorService.GetSnapshotAsync(server.Profile)));
            for (var i = 0; i < servers.Count; i++)
            {
                servers[i].ApplyMonitorSnapshot(snapshots[i]);
            }

            ApplyFilters();
        }
        finally
        {
            _isMonitoringRefreshRunning = false;
        }
    }

    private void ApplyFilters()
    {
        var filtered = Servers.AsEnumerable();
        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            var search = SearchText.Trim();
            filtered = filtered.Where(s =>
                s.ServerName.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                s.GameName.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                s.Tags.Contains(search, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(FilterProvider?.GameId))
        {
            filtered = filtered.Where(s => s.Provider.GameId.Equals(FilterProvider.GameId, StringComparison.OrdinalIgnoreCase));
        }

        if (FilterStatus is { Statuses.Count: > 0 })
        {
            filtered = filtered.Where(s => FilterStatus.Statuses.Contains(s.Status));
        }

        FilteredServers.Clear();
        filtered = filtered
            .OrderByDescending(server => server.IsFavorite)
            .ThenBy(server => server.ServerName, StringComparer.OrdinalIgnoreCase);

        foreach (var server in filtered)
        {
            FilteredServers.Add(server);
        }

        OnPropertyChanged(nameof(FilteredServers));
    }

    private void NotifyServerCollectionChanged()
    {
        OnPropertyChanged(nameof(IsEmpty));
        OnPropertyChanged(nameof(HasServers));
        OnPropertyChanged(nameof(FilteredServers));
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

    private static string SafeFileName(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        return new string(value.Select(ch => invalid.Contains(ch) ? '_' : ch).ToArray());
    }

    private void RefreshConsoleText()
    {
        if (_consoleServer == null)
        {
            ConsoleText = "No server selected.";
            return;
        }

        var logPath = _processService.GetConsoleLogPath(_consoleServer.Profile);
        ConsoleText = File.Exists(logPath)
            ? ReadTail(logPath, 400)
            : $"No console log exists yet for {_consoleServer.ServerName}.{Environment.NewLine}{logPath}";
        Message = $"Console log: {logPath}";
    }

    private static string ReadTail(string path, int maxLines)
    {
        var lines = File.ReadLines(path).TakeLast(maxLines);
        return string.Join(Environment.NewLine, lines);
    }

    private static void BrowseFolder(Action<string> apply, string currentPath)
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Select folder",
            Multiselect = false
        };

        if (!string.IsNullOrWhiteSpace(currentPath) && Directory.Exists(currentPath))
        {
            dialog.InitialDirectory = currentPath;
        }

        if (dialog.ShowDialog() == true)
        {
            apply(dialog.FolderName);
        }
    }

    private static void BrowseExecutable(Action<string> apply, string currentPath)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Select server executable",
            Filter = "Executable files (*.exe;*.bat;*.cmd;*.jar)|*.exe;*.bat;*.cmd;*.jar|All files (*.*)|*.*"
        };

        if (!string.IsNullOrWhiteSpace(currentPath))
        {
            var directory = Path.GetDirectoryName(currentPath);
            if (!string.IsNullOrWhiteSpace(directory) && Directory.Exists(directory))
            {
                dialog.InitialDirectory = directory;
            }
        }

        if (dialog.ShowDialog() == true)
        {
            apply(dialog.FileName);
        }
    }

    private string GetGameName(string gameId)
    {
        return _providers.TryGetProvider(gameId, out var provider) ? provider.GameName : gameId;
    }

    public void Dispose()
    {
        _refreshTimer.Stop();
        _processService.Dispose();
    }
}
