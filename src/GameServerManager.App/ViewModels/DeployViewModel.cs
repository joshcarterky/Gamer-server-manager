using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using GameServerManager.Core.Models;
using GameServerManager.GameProviders;
using GameServerManager.Services;

namespace GameServerManager.App.ViewModels;

// ── Server list item ──────────────────────────────────────────────────────────

public sealed class DeployServerItem
{
    public DeployServerItem(ServerProfile profile, IGameServerProvider? provider)
    {
        Profile = profile;
        Provider = provider;
        GameName = provider?.GameName ?? profile.GameId;
        SupportsSteamCmd = provider?.SupportedFeatures.HasFlag(GameServerFeatures.SteamCmdInstall) ?? false;

        if (!SupportsSteamCmd)
        {
            StatusLabel = "No SteamCMD";
            StatusColor = "#4E6070";
        }
        else if (string.IsNullOrWhiteSpace(profile.InstallPath))
        {
            StatusLabel = "No Path";
            StatusColor = "#FFB800";
        }
        else
        {
            var exe = string.IsNullOrWhiteSpace(profile.ExecutablePath)
                ? Path.Combine(profile.InstallPath,
                    provider!.ExecutableRelativePath.Replace('/', Path.DirectorySeparatorChar))
                : profile.ExecutablePath;
            IsInstalled = File.Exists(exe);
            StatusLabel = IsInstalled ? "Installed" : "Not Installed";
            StatusColor = IsInstalled ? "#38D983" : "#FF7070";
        }
    }

    public ServerProfile Profile { get; }
    public IGameServerProvider? Provider { get; }
    public string GameName { get; }
    public bool SupportsSteamCmd { get; }
    public bool IsInstalled { get; }
    public string StatusLabel { get; }
    public string StatusColor { get; }
    public string DisplayName => Profile.ProfileName;
}

// ── ViewModel ─────────────────────────────────────────────────────────────────

public sealed class DeployViewModel : BaseViewModel, IDisposable
{
    private const int MaxLogLines = 1000;

    private enum Panel { Empty, ServerInfo, NewServer, Installing, Result }

    private readonly AppDataPaths _paths;
    private readonly GameProviderRegistry _providers;
    private readonly ServersJsonService _serversService;
    private readonly ServerInstallService _installService;

    private CancellationTokenSource? _installCts;
    private Panel _panel = Panel.Empty;

    // ── Collections ───────────────────────────────────────────────────────────

    public ObservableCollection<DeployServerItem> Servers { get; } = new();
    public ObservableCollection<IGameServerProvider> AllProviders { get; } = new();
    public ObservableCollection<string> InstallLog { get; } = new();

    // ── Server list selection ─────────────────────────────────────────────────

    private DeployServerItem? _selectedServer;
    public DeployServerItem? SelectedServer
    {
        get => _selectedServer;
        set
        {
            if (SetProperty(ref _selectedServer, value) && value != null)
                ShowPanel(Panel.ServerInfo);
            else if (value == null)
                ShowPanel(Panel.Empty);
            NotifyCommandStates();
        }
    }

    // ── Panel visibility ──────────────────────────────────────────────────────

    public bool ShowEmpty => _panel == Panel.Empty;
    public bool ShowServerInfo => _panel == Panel.ServerInfo;
    public bool ShowNewServer => _panel == Panel.NewServer;
    public bool ShowInstalling => _panel == Panel.Installing;
    public bool ShowResult => _panel == Panel.Result;

    // ── Install state ─────────────────────────────────────────────────────────

    private string _installStage = string.Empty;
    public string InstallStage
    {
        get => _installStage;
        private set => SetProperty(ref _installStage, value);
    }

    private string _installStatus = string.Empty;
    public string InstallStatus
    {
        get => _installStatus;
        private set => SetProperty(ref _installStatus, value);
    }

    // ── Result state ──────────────────────────────────────────────────────────

    private bool _resultSuccess;
    public bool ResultSuccess
    {
        get => _resultSuccess;
        private set => SetProperty(ref _resultSuccess, value);
    }

    private string _resultMessage = string.Empty;
    public string ResultMessage
    {
        get => _resultMessage;
        private set => SetProperty(ref _resultMessage, value);
    }

    private string? _resultLogPath;
    public string? ResultLogPath
    {
        get => _resultLogPath;
        private set { if (SetProperty(ref _resultLogPath, value)) OnPropertyChanged(nameof(HasLogPath)); }
    }

    public bool HasLogPath => !string.IsNullOrEmpty(_resultLogPath) && File.Exists(_resultLogPath);

    private string? _resultInstallPath;

    // ── New server form ───────────────────────────────────────────────────────

    private IGameServerProvider? _newProvider;
    public IGameServerProvider? NewProvider
    {
        get => _newProvider;
        set
        {
            if (SetProperty(ref _newProvider, value))
                RegenerateNewPaths();
        }
    }

    private string _newProfileName = string.Empty;
    public string NewProfileName
    {
        get => _newProfileName;
        set
        {
            if (SetProperty(ref _newProfileName, value))
                RegenerateNewPaths();
        }
    }

    private string _newServerName = string.Empty;
    public string NewServerName
    {
        get => _newServerName;
        set
        {
            if (SetProperty(ref _newServerName, value))
            {
                if (string.IsNullOrWhiteSpace(_newProfileName) || _newProfileName == _prevAutoName)
                {
                    _newProfileName = value;
                    OnPropertyChanged(nameof(NewProfileName));
                }
                _prevAutoName = value;
                RegenerateNewPaths();
            }
        }
    }

    private string _prevAutoName = string.Empty;

    private string _newInstallPath = string.Empty;
    public string NewInstallPath
    {
        get => _newInstallPath;
        set => SetProperty(ref _newInstallPath, value);
    }

    private int _newGamePort;
    public int NewGamePort
    {
        get => _newGamePort;
        set => SetProperty(ref _newGamePort, value);
    }

    private int _newQueryPort;
    public int NewQueryPort
    {
        get => _newQueryPort;
        set => SetProperty(ref _newQueryPort, value);
    }

    private int _newRconPort;
    public int NewRconPort
    {
        get => _newRconPort;
        set => SetProperty(ref _newRconPort, value);
    }

    private string _newAdminPassword = string.Empty;
    public string NewAdminPassword
    {
        get => _newAdminPassword;
        set => SetProperty(ref _newAdminPassword, value);
    }

    // ── Status bar ────────────────────────────────────────────────────────────

    private string _statusText = "Select a server to manage, or create a new profile.";
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

    // ── Commands ──────────────────────────────────────────────────────────────

    public RelayCommand RefreshCommand { get; }
    public RelayCommand AddNewCommand { get; }
    public RelayCommand CancelNewCommand { get; }
    public RelayCommand BrowseInstallPathCommand { get; }
    public RelayCommand CreateProfileCommand { get; }
    public RelayCommand CreateAndInstallCommand { get; }
    public RelayCommand InstallCommand { get; }
    public RelayCommand UpdateCommand { get; }
    public RelayCommand ValidateCommand { get; }
    public RelayCommand CancelInstallCommand { get; }
    public RelayCommand OpenLogCommand { get; }
    public RelayCommand OpenInstallFolderCommand { get; }
    public RelayCommand BackToServerCommand { get; }
    public RelayCommand DeleteProfileCommand { get; }

    // ── Constructor ───────────────────────────────────────────────────────────

    public DeployViewModel()
    {
        _paths = new AppDataPaths();
        _providers = GameProviderRegistry.CreateDefault();
        _serversService = new ServersJsonService(_paths);
        _installService = new ServerInstallService(_paths);

        foreach (var p in _providers.Providers.OrderBy(p => p.GameName))
            AllProviders.Add(p);

        RefreshCommand         = new RelayCommand(_ => _ = LoadServersAsync());
        AddNewCommand          = new RelayCommand(_ => BeginNewProfile());
        CancelNewCommand       = new RelayCommand(_ => CancelNew());
        BrowseInstallPathCommand = new RelayCommand(_ => BrowseInstallPath());
        CreateProfileCommand   = new RelayCommand(_ => _ = CreateProfileAsync(thenInstall: false), () => CanCreate());
        CreateAndInstallCommand = new RelayCommand(_ => _ = CreateProfileAsync(thenInstall: true), () => CanCreate());
        InstallCommand         = new RelayCommand(_ => _ = RunInstallAsync(validate: false), () => CanInstallSelected());
        UpdateCommand          = new RelayCommand(_ => _ = RunInstallAsync(validate: false), () => CanInstallSelected());
        ValidateCommand        = new RelayCommand(_ => _ = RunInstallAsync(validate: true),  () => CanInstallSelected());
        CancelInstallCommand   = new RelayCommand(_ => _installCts?.Cancel(),                () => _panel == Panel.Installing);
        OpenLogCommand         = new RelayCommand(_ => OpenLog(),       () => HasLogPath);
        OpenInstallFolderCommand = new RelayCommand(_ => OpenInstallFolder(), () => !string.IsNullOrEmpty(_resultInstallPath));
        BackToServerCommand    = new RelayCommand(_ => GoBackFromResult());
        DeleteProfileCommand   = new RelayCommand(_ => _ = DeleteProfileAsync(), () => _selectedServer != null && _panel == Panel.ServerInfo);

        _ = LoadServersAsync();
    }

    // ── Server list loading ───────────────────────────────────────────────────

    private async Task LoadServersAsync()
    {
        try
        {
            var profiles = await _serversService.LoadServersAsync();
            var selectedId = _selectedServer?.Profile.Id;

            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                Servers.Clear();
                foreach (var p in profiles)
                {
                    _providers.TryGetProvider(p.GameId, out var provider);
                    Servers.Add(new DeployServerItem(p, provider));
                }

                // Restore selection
                var match = selectedId != null
                    ? Servers.FirstOrDefault(s => s.Profile.Id == selectedId)
                    : null;
                _selectedServer = match;
                OnPropertyChanged(nameof(SelectedServer));
                if (match == null) ShowPanel(Panel.Empty);
            });

            SetStatus($"Loaded {Servers.Count} server profile{(Servers.Count == 1 ? "" : "s")}.", false);
        }
        catch (Exception ex)
        {
            SetStatus($"Failed to load servers: {ex.Message}", true);
        }
    }

    // ── New profile flow ──────────────────────────────────────────────────────

    private void BeginNewProfile()
    {
        _newProvider = AllProviders.FirstOrDefault();
        _newProfileName = string.Empty;
        _newServerName = string.Empty;
        _newAdminPassword = string.Empty;
        _prevAutoName = string.Empty;
        OnPropertyChanged(nameof(NewProvider));
        OnPropertyChanged(nameof(NewProfileName));
        OnPropertyChanged(nameof(NewServerName));
        OnPropertyChanged(nameof(NewAdminPassword));
        RegenerateNewPaths();
        ShowPanel(Panel.NewServer);
        _selectedServer = null;
        OnPropertyChanged(nameof(SelectedServer));
    }

    private void CancelNew()
    {
        if (_selectedServer != null)
            ShowPanel(Panel.ServerInfo);
        else
            ShowPanel(Panel.Empty);
    }

    private void RegenerateNewPaths()
    {
        if (_newProvider == null) return;
        var slug = AppDataPaths.ToSlug(string.IsNullOrWhiteSpace(_newServerName) ? "new-server" : _newServerName);
        NewInstallPath = _paths.GetServerInstallDirectory(_newProvider.GameId, slug);
        NewGamePort  = _newProvider.DefaultPorts.FirstOrDefault(p => p.Name.Equals("Game",  StringComparison.OrdinalIgnoreCase))?.Port ?? 0;
        NewQueryPort = _newProvider.DefaultPorts.FirstOrDefault(p => p.Name.Equals("Query", StringComparison.OrdinalIgnoreCase))?.Port ?? 0;
        NewRconPort  = _newProvider.DefaultPorts.FirstOrDefault(p => p.Name.Equals("RCON",  StringComparison.OrdinalIgnoreCase))?.Port ?? 0;
        CreateProfileCommand.NotifyCanExecuteChanged();
        CreateAndInstallCommand.NotifyCanExecuteChanged();
    }

    private void BrowseInstallPath()
    {
        var dlg = new Microsoft.Win32.OpenFolderDialog
        {
            Title = "Select Install Directory",
            InitialDirectory = Directory.Exists(_newInstallPath)
                ? _newInstallPath
                : _paths.ServersDirectory
        };
        if (dlg.ShowDialog() == true)
            NewInstallPath = dlg.FolderName;
    }

    private bool CanCreate() =>
        _newProvider != null &&
        !string.IsNullOrWhiteSpace(_newServerName) &&
        !string.IsNullOrWhiteSpace(_newInstallPath);

    private async Task CreateProfileAsync(bool thenInstall)
    {
        if (_newProvider == null || string.IsNullOrWhiteSpace(_newServerName)) return;

        var profileName = string.IsNullOrWhiteSpace(_newProfileName) ? _newServerName : _newProfileName;
        var slug = AppDataPaths.ToSlug(profileName);

        var ports = new List<ServerPort>();
        if (_newGamePort  > 0) ports.Add(new ServerPort { Name = "Game",  Port = _newGamePort,  DefaultPort = _newGamePort,  Protocol = PortProtocol.UDP });
        if (_newQueryPort > 0) ports.Add(new ServerPort { Name = "Query", Port = _newQueryPort, DefaultPort = _newQueryPort, Protocol = PortProtocol.UDP });
        if (_newRconPort  > 0) ports.Add(new ServerPort { Name = "RCON",  Port = _newRconPort,  DefaultPort = _newRconPort,  Protocol = PortProtocol.TCP });

        var profile = new ServerProfile
        {
            Id           = Guid.NewGuid().ToString(),
            GameId       = _newProvider.GameId,
            ProfileName  = profileName,
            ServerName   = _newServerName,
            InstallPath  = _newInstallPath,
            ExecutablePath = Path.Combine(_newInstallPath, _newProvider.ExecutableRelativePath.Replace('/', Path.DirectorySeparatorChar)),
            AdminPassword = _newAdminPassword,
            MaxPlayers   = _newProvider.DefaultPorts.Any() ? 10 : 0,
            Status       = ServerStatus.Stopped,
            CreatedAt    = DateTime.UtcNow,
            ModifiedAt   = DateTime.UtcNow,
            Ports        = ports,
            Settings     =
            {
                ["backupDirectory"] = _paths.GetServerBackupDirectory(_newProvider.GameId, slug)
            }
        };

        foreach (var def in _newProvider.SettingsDefinitions)
            if (!string.IsNullOrWhiteSpace(def.SettingKey) && def.DefaultValue != null)
                profile.Settings.TryAdd(def.SettingKey, def.DefaultValue);

        try
        {
            await _serversService.AddServerAsync(profile);
            SetStatus($"Profile '{profileName}' created.", false);
            await LoadServersAsync();

            var created = Servers.FirstOrDefault(s => s.Profile.Id == profile.Id);
            _selectedServer = created;
            OnPropertyChanged(nameof(SelectedServer));

            if (thenInstall && created != null)
                await RunInstallAsync(validate: false);
            else
                ShowPanel(Panel.ServerInfo);
        }
        catch (Exception ex)
        {
            SetStatus($"Failed to save profile: {ex.Message}", true);
        }
    }

    // ── Install flow ──────────────────────────────────────────────────────────

    private bool CanInstallSelected()
    {
        if (_selectedServer == null) return false;
        return _selectedServer.SupportsSteamCmd &&
               !string.IsNullOrWhiteSpace(_selectedServer.Profile.InstallPath) &&
               _panel is Panel.ServerInfo or Panel.Result;
    }

    private async Task RunInstallAsync(bool validate)
    {
        if (_selectedServer == null) return;
        if (_selectedServer.Provider == null)
        {
            SetStatus("No provider found for this game.", true);
            return;
        }

        _installCts = new CancellationTokenSource();
        InstallLog.Clear();
        InstallStage = "Preparing";
        InstallStatus = "Starting…";
        ShowPanel(Panel.Installing);
        NotifyCommandStates();
        SetStatus($"Installing {_selectedServer.Profile.ProfileName}…", false);

        var profile = _selectedServer.Profile;
        var provider = _selectedServer.Provider;

        var progress = new Progress<ServerInstallProgress>(p =>
        {
            Application.Current.Dispatcher.InvokeAsync(() =>
            {
                InstallStage = p.Stage;
                InstallStatus = p.StatusLine;
                if (!string.IsNullOrWhiteSpace(p.StatusLine))
                {
                    InstallLog.Add(p.StatusLine);
                    while (InstallLog.Count > MaxLogLines) InstallLog.RemoveAt(0);
                }
            });
        });

        var result = await _installService.InstallOrUpdateAsync(
            profile, provider, validate, progress, _installCts.Token);

        _resultInstallPath = profile.InstallPath;
        ResultSuccess  = result.Success;
        ResultMessage  = result.Cancelled ? "Operation cancelled." : result.Message;
        ResultLogPath  = result.LogPath;

        ShowPanel(Panel.Result);
        NotifyCommandStates();
        SetStatus(result.Success ? $"✓ {result.Message}" : $"✕ {result.Message}", !result.Success);

        // Refresh list to update install status badge
        await LoadServersAsync();
    }

    // ── Result actions ────────────────────────────────────────────────────────

    private void GoBackFromResult()
    {
        if (_selectedServer != null)
            ShowPanel(Panel.ServerInfo);
        else
            ShowPanel(Panel.Empty);
    }

    private void OpenLog()
    {
        if (string.IsNullOrEmpty(_resultLogPath) || !File.Exists(_resultLogPath)) return;
        try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(_resultLogPath) { UseShellExecute = true }); }
        catch { }
    }

    private void OpenInstallFolder()
    {
        var path = _resultInstallPath ?? _selectedServer?.Profile.InstallPath;
        if (string.IsNullOrEmpty(path)) return;
        var dir = Directory.Exists(path) ? path : Path.GetDirectoryName(path);
        if (dir != null && Directory.Exists(dir))
            try { System.Diagnostics.Process.Start("explorer.exe", dir); } catch { }
    }

    // ── Delete profile ────────────────────────────────────────────────────────

    private async Task DeleteProfileAsync()
    {
        if (_selectedServer == null) return;
        var name = _selectedServer.Profile.ProfileName;
        var ans = MessageBox.Show(
            $"Delete the profile '{name}'?\n\nThis removes the saved configuration only. Server files on disk are not deleted.",
            "Delete Profile", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (ans != MessageBoxResult.Yes) return;

        try
        {
            await _serversService.DeleteServerAsync(_selectedServer.Profile.Id);
            _selectedServer = null;
            OnPropertyChanged(nameof(SelectedServer));
            ShowPanel(Panel.Empty);
            await LoadServersAsync();
            SetStatus($"Profile '{name}' deleted.", false);
        }
        catch (Exception ex)
        {
            SetStatus($"Delete failed: {ex.Message}", true);
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private void ShowPanel(Panel p)
    {
        _panel = p;
        OnPropertyChanged(nameof(ShowEmpty));
        OnPropertyChanged(nameof(ShowServerInfo));
        OnPropertyChanged(nameof(ShowNewServer));
        OnPropertyChanged(nameof(ShowInstalling));
        OnPropertyChanged(nameof(ShowResult));
    }

    private void SetStatus(string message, bool isError)
    {
        StatusText = message;
        StatusIsError = isError;
    }

    private void NotifyCommandStates()
    {
        InstallCommand.NotifyCanExecuteChanged();
        UpdateCommand.NotifyCanExecuteChanged();
        ValidateCommand.NotifyCanExecuteChanged();
        CancelInstallCommand.NotifyCanExecuteChanged();
        OpenLogCommand.NotifyCanExecuteChanged();
        OpenInstallFolderCommand.NotifyCanExecuteChanged();
        DeleteProfileCommand.NotifyCanExecuteChanged();
        CreateProfileCommand.NotifyCanExecuteChanged();
        CreateAndInstallCommand.NotifyCanExecuteChanged();
    }

    public void Dispose()
    {
        _installCts?.Cancel();
        _installCts?.Dispose();
        _installService.Dispose();
    }
}
