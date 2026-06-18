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
    private readonly ServerImportService _importService;
    private readonly ServerProfileValidator _profileValidator;
    private readonly ServerInstallService _installService;
    private readonly DispatcherTimer _refreshTimer;
    private bool _isMonitoringRefreshRunning;
    private string _searchText = string.Empty;
    private GameFilterOption? _filterProvider;
    private StatusFilterOption? _filterStatus;
    private string _message = string.Empty;
    private bool _isAddServerOpen;
    private bool _isConsoleOpen;
    private bool _isImportConfirmationOpen;
    private bool _isImportRunning;
    private bool _linkExistingFolder;
    private string _consoleTitle = "Console";
    private string _consoleText = string.Empty;
    private string _importSourceFolder = string.Empty;
    private string _importDestinationFolder = string.Empty;
    private string _importServerName = string.Empty;
    private string _importGameName = string.Empty;
    private string _importEstimatedSize = string.Empty;
    private string _importStatus = string.Empty;
    private string _importCurrentFile = string.Empty;
    private int _importProgress;
    private ServerCardViewModel? _consoleServer;
    private ServerProfile? _pendingImportProfile;
    private CancellationTokenSource? _importCancellation;
    // Install / Update panel state
    private bool _isInstallOpen;
    private bool _isInstallRunning;
    private bool _isInstallComplete;
    private bool _isInstallSuccess;
    private bool _installValidateFiles = true;
    private bool _installRestartAfter;
    private string _installStage = string.Empty;
    private string _installLogText = string.Empty;
    private readonly System.Text.StringBuilder _installLogBuilder = new();
    private string _installResultMessage = string.Empty;
    private string _installLogPath = string.Empty;
    private ServerCardViewModel? _installTarget;
    private CancellationTokenSource? _installCts;

    public ServersViewModel()
    {
        _providers = GameProviderRegistry.CreateDefault();
        var paths = new AppDataPaths();
        _serversJsonService = new ServersJsonService(paths);
        _processService = new ServerProcessService(_providers, paths);
        _monitorService = new ServerMonitorService(_processService, new ServerQueryService());
        _backupService = new BackupService(paths);
        _importDetector = new ServerImportDetector(_providers);
        _importService = new ServerImportService(paths);
        _profileValidator = new ServerProfileValidator();
        _installService = new ServerInstallService(paths);
        AddServer = new AddServerWizardViewModel(_providers.Providers, paths);

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
        ConfirmImportCommand = new RelayCommand(async _ => await ConfirmImportAsync());
        CancelImportCommand = new RelayCommand(_ => CancelImport());
        CancelImportCopyCommand = new RelayCommand(_ => _importCancellation?.Cancel());
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
        StartInstallCommand = new RelayCommand(async _ => await StartInstallAsync());
        CancelInstallCommand = new RelayCommand(_ => _installCts?.Cancel());
        CloseInstallCommand = new RelayCommand(_ => CloseInstall());
        OpenInstallLogCommand = new RelayCommand(_ => OpenInstallLog());
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
    public RelayCommand ConfirmImportCommand { get; }
    public RelayCommand CancelImportCommand { get; }
    public RelayCommand CancelImportCopyCommand { get; }
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
    public RelayCommand StartInstallCommand { get; }
    public RelayCommand CancelInstallCommand { get; }
    public RelayCommand CloseInstallCommand { get; }
    public RelayCommand OpenInstallLogCommand { get; }
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

    // ── Summary stats for header cards ────────────────────────────────────────
    public int TotalCount => Servers.Count;
    public int OnlineCount => Servers.Count(s => s.Status == ServerStatus.Running);
    public int TotalPlayerCount => Servers.Sum(s => s.PlayerCount);
    public string TotalPlayerDisplay => Servers.Any(s => s.Status == ServerStatus.Running)
        ? TotalPlayerCount.ToString()
        : "—";
    public int AttentionCount => Servers.Count(s => s.HasWarnings || s.Status == ServerStatus.Error);

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

    public bool IsImportConfirmationOpen
    {
        get => _isImportConfirmationOpen;
        set => SetProperty(ref _isImportConfirmationOpen, value);
    }

    public bool IsImportRunning
    {
        get => _isImportRunning;
        set
        {
            if (SetProperty(ref _isImportRunning, value))
            {
                OnPropertyChanged(nameof(CanConfirmImport));
            }
        }
    }

    public bool LinkExistingFolder
    {
        get => _linkExistingFolder;
        set
        {
            if (SetProperty(ref _linkExistingFolder, value))
            {
                OnPropertyChanged(nameof(ImportModeText));
                OnPropertyChanged(nameof(ImportDestinationDisplay));
            }
        }
    }

    public string ImportSourceFolder
    {
        get => _importSourceFolder;
        set => SetProperty(ref _importSourceFolder, value);
    }

    public string ImportDestinationFolder
    {
        get => _importDestinationFolder;
        set
        {
            if (SetProperty(ref _importDestinationFolder, value))
            {
                OnPropertyChanged(nameof(ImportDestinationDisplay));
            }
        }
    }

    public string ImportServerName
    {
        get => _importServerName;
        set
        {
            if (SetProperty(ref _importServerName, value))
            {
                ImportDestinationFolder = _importService.CreateDestinationPath(value);
            }
        }
    }

    public string ImportGameName
    {
        get => _importGameName;
        set => SetProperty(ref _importGameName, value);
    }

    public string ImportEstimatedSize
    {
        get => _importEstimatedSize;
        set => SetProperty(ref _importEstimatedSize, value);
    }

    public string ImportStatus
    {
        get => _importStatus;
        set => SetProperty(ref _importStatus, value);
    }

    public string ImportCurrentFile
    {
        get => _importCurrentFile;
        set => SetProperty(ref _importCurrentFile, value);
    }

    public int ImportProgress
    {
        get => _importProgress;
        set => SetProperty(ref _importProgress, value);
    }

    public string ImportModeText => LinkExistingFolder
        ? "Advanced: link existing folder without copying."
        : "Recommended: copy into the managed server folder.";

    public string ImportDestinationDisplay => LinkExistingFolder ? ImportSourceFolder : ImportDestinationFolder;
    public bool CanConfirmImport => !IsImportRunning;

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

    // ── Install / Update panel ─────────────────────────────────────────────────
    public bool IsInstallOpen
    {
        get => _isInstallOpen;
        private set => SetProperty(ref _isInstallOpen, value);
    }

    public bool IsInstallRunning
    {
        get => _isInstallRunning;
        private set => SetProperty(ref _isInstallRunning, value);
    }

    public bool IsInstallComplete
    {
        get => _isInstallComplete;
        private set => SetProperty(ref _isInstallComplete, value);
    }

    public bool IsInstallSuccess
    {
        get => _isInstallSuccess;
        private set => SetProperty(ref _isInstallSuccess, value);
    }

    public bool IsInstallConfirm => IsInstallOpen && !IsInstallRunning && !IsInstallComplete;

    public bool InstallValidateFiles
    {
        get => _installValidateFiles;
        set => SetProperty(ref _installValidateFiles, value);
    }

    public bool InstallRestartAfter
    {
        get => _installRestartAfter;
        set => SetProperty(ref _installRestartAfter, value);
    }

    public string InstallStage
    {
        get => _installStage;
        private set => SetProperty(ref _installStage, value);
    }

    public string InstallLogText
    {
        get => _installLogText;
        private set => SetProperty(ref _installLogText, value);
    }

    public string InstallResultMessage
    {
        get => _installResultMessage;
        private set => SetProperty(ref _installResultMessage, value);
    }

    public string InstallLogPath
    {
        get => _installLogPath;
        private set => SetProperty(ref _installLogPath, value);
    }

    public ServerCardViewModel? InstallTarget
    {
        get => _installTarget;
        private set => SetProperty(ref _installTarget, value);
    }

    public string InstallTargetName => _installTarget?.ServerName ?? string.Empty;
    public string InstallTargetGame => _installTarget?.GameName ?? string.Empty;
    public string InstallTargetPath => _installTarget?.Profile.InstallPath ?? string.Empty;

    public bool IsInstallFirstTime =>
        _installTarget != null &&
        !string.IsNullOrWhiteSpace(_installTarget.Profile.InstallPath) &&
        !File.Exists(Path.Combine(_installTarget.Profile.InstallPath, _installTarget.Provider.ExecutableRelativePath));

    public string InstallModeLabel => IsInstallFirstTime ? "Install" : "Update";

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
                    if (MemorySettingsPolicy.ApplyProfileMigration(profile, provider, out var migrationMessage))
                    {
                        await _serversJsonService.UpdateServerAsync(profile);
                        await LogMemoryMigrationAsync(migrationMessage);
                    }

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
            _pendingImportProfile = profile;
            ImportStatus = "Calculating folder size...";
            ImportProgress = 0;
            ImportCurrentFile = string.Empty;
            ImportSourceFolder = dialog.FolderName;
            ImportGameName = GetGameName(profile.GameId);
            ImportServerName = profile.ServerName;
            LinkExistingFolder = false;
            ImportEstimatedSize = FormatBytes(await _importService.CalculateFolderSizeAsync(dialog.FolderName));
            IsImportConfirmationOpen = true;
            Message = "Review the import options before continuing.";
        }
        catch (Exception ex)
        {
            Message = $"Import failed: {ex.Message}";
        }
    }

    private async Task ConfirmImportAsync()
    {
        if (_pendingImportProfile == null || IsImportRunning)
        {
            return;
        }

        IsImportRunning = true;
        ImportProgress = 0;
        _importCancellation = new CancellationTokenSource();

        try
        {
            ServerProfile profile;
            if (LinkExistingFolder)
            {
                var answer = MessageBox.Show(
                    "Linked servers remain in their original location. Moving or deleting the original folder may break this server.",
                    "Link Existing Folder",
                    MessageBoxButton.OKCancel,
                    MessageBoxImage.Warning);
                if (answer != MessageBoxResult.OK)
                {
                    Message = "Import cancelled.";
                    return;
                }

                profile = _pendingImportProfile;
                profile.ServerName = ImportServerName;
                profile.ProfileName = ImportServerName;
                MarkImported(profile, ImportSourceFolder, copied: false);
                await _importService.LogAsync($"Linked imported server. Source={ImportSourceFolder}");
            }
            else
            {
                ImportStatus = "Copying server files...";
                var destination = ImportDestinationFolder;
                if (Directory.Exists(destination))
                {
                    throw new IOException($"Destination folder already exists: {destination}");
                }

                var progress = new Progress<ServerImportProgress>(update =>
                {
                    ImportStatus = update.Status;
                    ImportCurrentFile = update.CurrentFile;
                    ImportProgress = update.Percent;
                    ImportEstimatedSize = $"{FormatBytes(update.CopiedBytes)} / {FormatBytes(update.TotalBytes)} at {FormatBytes((long)update.BytesPerSecond)}/s";
                });

                await _importService.CopyIntoManagedFolderAsync(ImportSourceFolder, destination, progress, _importCancellation.Token);
                profile = _importDetector.Detect(destination);
                profile.ServerName = ImportServerName;
                profile.ProfileName = ImportServerName;
                MarkImported(profile, ImportSourceFolder, copied: true);
            }

            await _serversJsonService.AddServerAsync(profile);
            IsImportConfirmationOpen = false;
            Message = LinkExistingFolder
                ? $"Linked {profile.ServerName} as {GetGameName(profile.GameId)}."
                : $"Imported {profile.ServerName} into the managed server folder.";
            _pendingImportProfile = null;
            await LoadAsync();
        }
        catch (OperationCanceledException)
        {
            ImportStatus = "Import canceled.";
            Message = "Import canceled.";
        }
        catch (Exception ex)
        {
            ImportStatus = "Import failed.";
            Message = $"Import failed: {ex.Message}";
        }
        finally
        {
            _importCancellation?.Dispose();
            _importCancellation = null;
            IsImportRunning = false;
        }
    }

    private void CancelImport()
    {
        if (IsImportRunning)
        {
            _importCancellation?.Cancel();
            return;
        }

        IsImportConfirmationOpen = false;
        _pendingImportProfile = null;
        Message = "Import cancelled.";
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
        if (server == null) return;

        Debug.WriteLine("[ServersPage] Install/Update button clicked");
        Debug.WriteLine($"[InstallUpdate] ServerId={server.Profile.Id}");
        Debug.WriteLine($"[InstallUpdate] ServerName={server.ServerName}");
        Debug.WriteLine($"[InstallUpdate] Game={server.Profile.GameId}");
        Debug.WriteLine($"[InstallUpdate] InstallPath={server.Profile.InstallPath}");

        if (_installService.IsOperationActive(server.Profile.Id))
        {
            Message = $"Install or update already in progress for {server.ServerName}.";
            Debug.WriteLine($"[InstallUpdate] Blocked — operation already active for {server.Profile.Id}");
            return;
        }

        InstallTarget = server;
        InstallValidateFiles = true;
        InstallRestartAfter = false;
        _installStage = string.Empty;
        _installLogBuilder.Clear();
        _installLogText = string.Empty;
        _installResultMessage = string.Empty;
        _installLogPath = string.Empty;
        SetInstallPanelState(open: true, running: false, complete: false, success: false);
    }

    private void SetInstallPanelState(bool open, bool running, bool complete, bool success)
    {
        IsInstallOpen = open;
        IsInstallRunning = running;
        IsInstallComplete = complete;
        IsInstallSuccess = success;
        OnPropertyChanged(nameof(IsInstallConfirm));
        OnPropertyChanged(nameof(IsInstallFirstTime));
        OnPropertyChanged(nameof(InstallModeLabel));
        OnPropertyChanged(nameof(InstallTargetName));
        OnPropertyChanged(nameof(InstallTargetGame));
        OnPropertyChanged(nameof(InstallTargetPath));
    }

    private async Task StartInstallAsync()
    {
        if (IsInstallRunning || _installTarget == null) return;

        var server = _installTarget;
        var mode = IsInstallFirstTime ? "Install" : "Update";
        Debug.WriteLine($"[InstallUpdate] Mode={mode}");
        Debug.WriteLine($"[InstallUpdate] Operation started");

        if (server.Status == ServerStatus.Running)
        {
            var answer = MessageBox.Show(
                $"'{server.ServerName}' is currently running. Stop it now before updating?",
                "Server Running",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);
            if (answer != MessageBoxResult.Yes)
            {
                Message = $"Update cancelled — {server.ServerName} is still running.";
                CloseInstall();
                return;
            }

            try
            {
                server.SetBusy("Stopping...");
                await _processService.StopServerAsync(server.Profile);
                await _serversJsonService.UpdateServerAsync(server.Profile);
            }
            catch (Exception ex)
            {
                Message = $"Could not stop {server.ServerName}: {ex.Message}";
                server.SetBusy(string.Empty);
                CloseInstall();
                return;
            }
            finally
            {
                server.SetBusy(string.Empty);
            }
        }

        SetInstallPanelState(open: true, running: true, complete: false, success: false);
        server.Profile.Status = ServerStatus.Updating;
        server.RefreshProfileFields();

        _installCts = new CancellationTokenSource();

        var progress = new Progress<ServerInstallProgress>(p =>
        {
            InstallStage = p.Stage;
            if (!string.IsNullOrWhiteSpace(p.StatusLine))
            {
                _installLogBuilder.AppendLine(p.StatusLine);
                InstallLogText = _installLogBuilder.ToString();
            }
        });

        try
        {
            var result = await _installService.InstallOrUpdateAsync(
                server.Profile,
                server.Provider,
                InstallValidateFiles,
                progress,
                _installCts.Token);

            Debug.WriteLine($"[InstallUpdate] Installer exited — Success={result.Success}, Cancelled={result.Cancelled}");
            Debug.WriteLine(result.Success ? "[InstallUpdate] Operation completed" : "[InstallUpdate] Operation failed");
            InstallResultMessage = result.Message;
            InstallLogPath = result.LogPath;
            SetInstallPanelState(open: true, running: false, complete: true, success: result.Success);

            server.Profile.Status = ServerStatus.Stopped;
            server.RefreshProfileFields();
            await _serversJsonService.UpdateServerAsync(server.Profile);
            await RefreshMonitoringAsync();

            Message = result.Success
                ? $"Install complete: {result.Message}"
                : $"Install failed: {result.Message}";

            if (result.Success && InstallRestartAfter)
            {
                CloseInstall();
                await StartAsync(server);
            }
        }
        catch (Exception ex)
        {
            InstallResultMessage = $"Unexpected error: {ex.Message}";
            InstallLogPath = string.Empty;
            SetInstallPanelState(open: true, running: false, complete: true, success: false);
            server.Profile.Status = ServerStatus.Stopped;
            server.RefreshProfileFields();
            Message = $"Install error: {ex.Message}";
        }
        finally
        {
            _installCts?.Dispose();
            _installCts = null;
        }
    }

    private void CloseInstall()
    {
        _installCts?.Cancel();
        InstallTarget = null;
        SetInstallPanelState(open: false, running: false, complete: false, success: false);
    }

    private void OpenInstallLog()
    {
        if (!string.IsNullOrEmpty(InstallLogPath) && File.Exists(InstallLogPath))
        {
            Process.Start(new ProcessStartInfo { FileName = InstallLogPath, UseShellExecute = true });
        }
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
            NotifyServerCollectionChanged();
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
        OnPropertyChanged(nameof(TotalCount));
        OnPropertyChanged(nameof(OnlineCount));
        OnPropertyChanged(nameof(TotalPlayerCount));
        OnPropertyChanged(nameof(TotalPlayerDisplay));
        OnPropertyChanged(nameof(AttentionCount));
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

    private static void MarkImported(ServerProfile profile, string originalSourcePath, bool copied)
    {
        profile.Notes = copied
            ? $"Imported from {originalSourcePath}"
            : $"Linked imported server from {originalSourcePath}";
        profile.Settings["tags"] = AddTag(profile.Settings.TryGetValue("tags", out var tags) ? tags : string.Empty, "imported");
        profile.Settings["imported"] = "true";
        profile.Settings["originalImportPath"] = originalSourcePath;
        profile.Settings["importMode"] = copied ? "copied" : "linked";
        profile.ModifiedAt = DateTime.UtcNow;
    }

    private static string AddTag(string tags, string tag)
    {
        return tags.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Any(value => value.Equals(tag, StringComparison.OrdinalIgnoreCase))
            ? tags
            : string.IsNullOrWhiteSpace(tags) ? tag : $"{tags}, {tag}";
    }

    private static string FormatBytes(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB", "TB"];
        double value = bytes;
        var unit = 0;
        while (value >= 1024 && unit < units.Length - 1)
        {
            value /= 1024;
            unit++;
        }

        return $"{value:0.##} {units[unit]}";
    }

    private async Task LogMemoryMigrationAsync(string message)
    {
        var logPath = Path.Combine(new AppDataPaths().LogsDirectory, "migration.log");
        Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);
        await File.AppendAllTextAsync(logPath, $"[{DateTimeOffset.Now:o}] {message}{Environment.NewLine}");
    }

    public void Dispose()
    {
        _refreshTimer.Stop();
        _processService.Dispose();
        _installService.Dispose();
        _installCts?.Cancel();
        _installCts?.Dispose();
    }
}
