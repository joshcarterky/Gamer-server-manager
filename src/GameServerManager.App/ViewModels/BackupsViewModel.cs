using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using GameServerManager.Core.Models;
using GameServerManager.GameProviders;
using GameServerManager.Services;

namespace GameServerManager.App.ViewModels;

/// <summary>A server shown in the Backups page server picker.</summary>
public sealed class BackupServerOption
{
    public BackupServerOption(ServerProfile profile, string gameName)
    {
        Profile = profile;
        GameName = gameName;
    }

    public ServerProfile Profile { get; }
    public string GameName { get; }
    public string DisplayName => string.IsNullOrWhiteSpace(Profile.ProfileName) ? Profile.ServerName : Profile.ProfileName;
    public string Label => $"{DisplayName}  ·  {GameName}";
}

/// <summary>One backup archive row.</summary>
public sealed class BackupItemViewModel
{
    public BackupItemViewModel(BackupFileInfo info) => Info = info;

    public BackupFileInfo Info { get; }
    public string FileName => Info.FileName;
    public string CreatedText => Info.CreatedAt.ToString("g");
    public string SizeText => FormatBytes(Info.SizeBytes);

    private static string FormatBytes(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB", "TB"];
        double value = Math.Max(0, bytes);
        var unit = 0;
        while (value >= 1024 && unit < units.Length - 1)
        {
            value /= 1024;
            unit++;
        }

        return $"{value:0.##} {units[unit]}";
    }
}

/// <summary>
/// Backups page: pick a server, see its archives, and create / restore / delete /
/// open them. Restore is blocked while the server is running and always takes a
/// safety backup first (handled by <see cref="BackupService"/>).
/// </summary>
public sealed class BackupsViewModel : BaseViewModel
{
    private readonly AppDataPaths _paths = new();
    private readonly ServersJsonService _serversService;
    private readonly BackupService _backupService;
    private readonly ServerProcessService _processService;
    private readonly GameProviderRegistry _providers = GameProviderRegistry.CreateDefault();

    private BackupServerOption? _selectedServer;
    private string _status = "Select a server to view its backups.";
    private bool _isBusy;

    public ObservableCollection<BackupServerOption> Servers { get; } = new();
    public ObservableCollection<BackupItemViewModel> Backups { get; } = new();

    public RelayCommand RefreshCommand { get; }
    public RelayCommand CreateBackupCommand { get; }
    public RelayCommand RestoreCommand { get; }
    public RelayCommand DeleteCommand { get; }
    public RelayCommand OpenFolderCommand { get; }

    public BackupsViewModel()
    {
        _serversService = new ServersJsonService(_paths);
        _backupService = new BackupService(_paths);
        _processService = new ServerProcessService(_providers, _paths);

        RefreshCommand = new RelayCommand(async _ => await LoadAsync());
        CreateBackupCommand = new RelayCommand(async _ => await CreateBackupAsync(), () => _selectedServer != null && !_isBusy);
        RestoreCommand = new RelayCommand(async p => await RestoreAsync(p as BackupItemViewModel), () => !_isBusy);
        DeleteCommand = new RelayCommand(async p => await DeleteAsync(p as BackupItemViewModel), () => !_isBusy);
        OpenFolderCommand = new RelayCommand(_ => OpenFolder(), () => _selectedServer != null);

        _ = LoadAsync();
    }

    public BackupServerOption? SelectedServer
    {
        get => _selectedServer;
        set
        {
            if (SetProperty(ref _selectedServer, value))
            {
                RefreshBackups();
                NotifyCommands();
            }
        }
    }

    public string Status
    {
        get => _status;
        private set => SetProperty(ref _status, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set { if (SetProperty(ref _isBusy, value)) NotifyCommands(); }
    }

    public bool HasNoBackups => Backups.Count == 0;

    private async Task LoadAsync()
    {
        try
        {
            var selectedId = _selectedServer?.Profile.Id;
            var profiles = await _serversService.LoadServersAsync();

            Servers.Clear();
            foreach (var profile in profiles.OrderBy(p => p.ProfileName))
            {
                var gameName = _providers.TryGetProvider(profile.GameId, out var provider) ? provider.GameName : profile.GameId;
                Servers.Add(new BackupServerOption(profile, gameName));
            }

            _selectedServer = selectedId != null
                ? Servers.FirstOrDefault(s => s.Profile.Id == selectedId)
                : Servers.FirstOrDefault();
            OnPropertyChanged(nameof(SelectedServer));
            RefreshBackups();
            NotifyCommands();

            Status = Servers.Count == 0
                ? "No servers yet. Create one in Deploy, then back it up here."
                : $"{Servers.Count} server{(Servers.Count == 1 ? "" : "s")} available.";
        }
        catch (Exception ex)
        {
            Status = $"Failed to load servers: {ex.Message}";
        }
    }

    private void RefreshBackups()
    {
        Backups.Clear();
        if (_selectedServer != null)
        {
            try
            {
                foreach (var backup in _backupService.ListBackups(_selectedServer.Profile))
                    Backups.Add(new BackupItemViewModel(backup));
            }
            catch (Exception ex)
            {
                Status = $"Failed to list backups: {ex.Message}";
            }
        }

        OnPropertyChanged(nameof(HasNoBackups));
    }

    private async Task CreateBackupAsync()
    {
        if (_selectedServer == null) return;

        try
        {
            IsBusy = true;
            Status = $"Backing up {_selectedServer.DisplayName}…";
            var path = await _backupService.BackupServerAsync(_selectedServer.Profile);
            _selectedServer.Profile.LastBackupAt = DateTime.UtcNow;
            await _serversService.UpdateServerAsync(_selectedServer.Profile);
            RefreshBackups();
            Status = $"Backup created: {Path.GetFileName(path)}";
        }
        catch (Exception ex)
        {
            Status = $"Backup failed: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task RestoreAsync(BackupItemViewModel? item)
    {
        if (item == null || _selectedServer == null) return;

        if (_processService.IsRunning(_selectedServer.Profile))
        {
            MessageBox.Show(
                "Stop the server before restoring a backup — restoring over a running server can corrupt its files.",
                "Server is running", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var confirm = MessageBox.Show(
            $"Restore '{item.FileName}' over '{_selectedServer.DisplayName}'?\n\n" +
            "The current files are backed up first, then replaced with the contents of this backup.",
            "Restore Backup", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (confirm != MessageBoxResult.Yes)
        {
            Status = "Restore cancelled.";
            return;
        }

        try
        {
            IsBusy = true;
            Status = $"Restoring {item.FileName} — taking a safety backup first…";
            var result = await _backupService.RestoreServerAsync(_selectedServer.Profile, item.Info.FullPath);
            RefreshBackups();
            Status = result.Message;
            if (!result.Success)
                MessageBox.Show(result.Message, "Restore", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        catch (Exception ex)
        {
            Status = $"Restore failed: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task DeleteAsync(BackupItemViewModel? item)
    {
        if (item == null) return;

        var confirm = MessageBox.Show(
            $"Permanently delete backup '{item.FileName}'? This cannot be undone.",
            "Delete Backup", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (confirm != MessageBoxResult.Yes) return;

        try
        {
            File.Delete(item.Info.FullPath);
            RefreshBackups();
            Status = $"Deleted {item.FileName}.";
        }
        catch (Exception ex)
        {
            Status = $"Delete failed: {ex.Message}";
        }

        await Task.CompletedTask;
    }

    private void OpenFolder()
    {
        if (_selectedServer == null) return;

        try
        {
            var folder = _backupService.GetServerBackupsFolder(_selectedServer.Profile);
            Directory.CreateDirectory(folder);
            Process.Start(new ProcessStartInfo { FileName = folder, UseShellExecute = true });
        }
        catch (Exception ex)
        {
            Status = $"Could not open the backups folder: {ex.Message}";
        }
    }

    private void NotifyCommands()
    {
        CreateBackupCommand.NotifyCanExecuteChanged();
        RestoreCommand.NotifyCanExecuteChanged();
        DeleteCommand.NotifyCanExecuteChanged();
        OpenFolderCommand.NotifyCanExecuteChanged();
    }
}
