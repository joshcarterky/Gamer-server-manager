using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Input;
using GameServerManager.Core.Models;
using GameServerManager.Services;
using Microsoft.Win32;

namespace GameServerManager.App.ViewModels;

// ── Tree node for left-panel folder tree ──────────────────────────────────────
public class FolderTreeNode : BaseViewModel
{
    private bool _isExpanded;
    private bool _isSelected;

    public string Name { get; }
    public string FullPath { get; }
    public ObservableCollection<FolderTreeNode> Children { get; } = new();
    public bool HasChildren => Children.Count > 0 || _placeholder != null;

    private FolderTreeNode? _placeholder;

    public FolderTreeNode(string name, string fullPath, bool addPlaceholder = false)
    {
        Name = name;
        FullPath = fullPath;
        if (addPlaceholder)
        {
            _placeholder = new FolderTreeNode("...", fullPath);
            Children.Add(_placeholder);
        }
    }

    public bool IsExpanded
    {
        get => _isExpanded;
        set => SetProperty(ref _isExpanded, value);
    }

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }

    public void ClearPlaceholder()
    {
        if (_placeholder != null)
        {
            Children.Remove(_placeholder);
            _placeholder = null;
        }
    }
}

// ── File entry view model for main list ───────────────────────────────────────
public class FileEntryViewModel : BaseViewModel
{
    private bool _isSelected;

    public string Name { get; }
    public string FullPath { get; }
    public FileEntryType Type { get; }
    public long SizeBytes { get; }
    public string SizeDisplay { get; }
    public DateTime Modified { get; }
    public string ModifiedDisplay { get; }
    public string TypeDisplay { get; }
    public bool IsDirectory => Type == FileEntryType.Directory;
    public bool IsReadOnly { get; }

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }

    public FileEntryViewModel(FileEntry entry)
    {
        Name = entry.Name;
        FullPath = entry.FullPath;
        Type = entry.Type;
        SizeBytes = entry.SizeBytes;
        Modified = entry.Modified;
        IsReadOnly = entry.IsReadOnly;
        ModifiedDisplay = entry.Modified.ToString("yyyy-MM-dd HH:mm");
        SizeDisplay = entry.Type == FileEntryType.Directory ? "" : FileManagerService.FormatSize(entry.SizeBytes);
        TypeDisplay = entry.Type == FileEntryType.Directory
            ? "Folder"
            : (Path.GetExtension(entry.Name).TrimStart('.').ToUpperInvariant() is { Length: > 0 } ext ? $"{ext} File" : "File");
    }
}

// ── Breadcrumb segment ────────────────────────────────────────────────────────
public class BreadcrumbItem
{
    public string Label { get; }
    public string FullPath { get; }
    public bool IsLast { get; set; }

    public BreadcrumbItem(string label, string fullPath)
    {
        Label = label;
        FullPath = fullPath;
    }
}

// ── Sort options ──────────────────────────────────────────────────────────────
public enum FileSortField { Name, Type, Size, Modified }
public enum FileSortDirection { Ascending, Descending }

// ── Main ViewModel ────────────────────────────────────────────────────────────
public class FileManagerViewModel : BaseViewModel
{
    private readonly FileManagerService _service = new();
    private readonly ServersJsonService _serversService = new();

    // History stacks
    private readonly Stack<string> _backStack = new();
    private readonly Stack<string> _forwardStack = new();

    // Clipboard
    private List<string> _clipboardPaths = new();
    private bool _clipboardIsCut;

    // ── Server selector ───────────────────────────────────────────────────────

    private ObservableCollection<ServerProfile> _servers = new();
    public ObservableCollection<ServerProfile> Servers
    {
        get => _servers;
        private set => SetProperty(ref _servers, value);
    }

    private ServerProfile? _selectedServer;
    public ServerProfile? SelectedServer
    {
        get => _selectedServer;
        set
        {
            if (SetProperty(ref _selectedServer, value))
                _ = OnServerSelectedAsync();
        }
    }

    private string _rootPath = string.Empty;
    public string RootPath
    {
        get => _rootPath;
        private set => SetProperty(ref _rootPath, value);
    }

    // ── Navigation ────────────────────────────────────────────────────────────

    private string _currentPath = string.Empty;
    public string CurrentPath
    {
        get => _currentPath;
        private set => SetProperty(ref _currentPath, value);
    }

    private ObservableCollection<BreadcrumbItem> _breadcrumbs = new();
    public ObservableCollection<BreadcrumbItem> Breadcrumbs
    {
        get => _breadcrumbs;
        private set => SetProperty(ref _breadcrumbs, value);
    }

    // ── Folder tree ───────────────────────────────────────────────────────────

    private ObservableCollection<FolderTreeNode> _treeNodes = new();
    public ObservableCollection<FolderTreeNode> TreeNodes
    {
        get => _treeNodes;
        private set => SetProperty(ref _treeNodes, value);
    }

    // ── File list ─────────────────────────────────────────────────────────────

    private ObservableCollection<FileEntryViewModel> _entries = new();
    public ObservableCollection<FileEntryViewModel> Entries
    {
        get => _entries;
        private set => SetProperty(ref _entries, value);
    }

    private string _searchText = string.Empty;
    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetProperty(ref _searchText, value))
                ApplyFilter();
        }
    }

    private FileSortField _sortField = FileSortField.Name;
    public FileSortField SortField
    {
        get => _sortField;
        set
        {
            if (_sortField == value)
                SortDirection = SortDirection == FileSortDirection.Ascending
                    ? FileSortDirection.Descending
                    : FileSortDirection.Ascending;
            else
            {
                SetProperty(ref _sortField, value);
                SortDirection = FileSortDirection.Ascending;
            }
            ApplyFilter();
        }
    }

    private FileSortDirection _sortDirection = FileSortDirection.Ascending;
    public FileSortDirection SortDirection
    {
        get => _sortDirection;
        set => SetProperty(ref _sortDirection, value);
    }

    // ── State ─────────────────────────────────────────────────────────────────

    private bool _isLoading;
    public bool IsLoading
    {
        get => _isLoading;
        private set
        {
            SetProperty(ref _isLoading, value);
            NotifyAllCommands();
        }
    }

    private string _statusMessage = string.Empty;
    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    private bool _statusIsError;
    public bool StatusIsError
    {
        get => _statusIsError;
        private set => SetProperty(ref _statusIsError, value);
    }

    private bool _hasServer;
    public bool HasServer
    {
        get => _hasServer;
        private set
        {
            SetProperty(ref _hasServer, value);
            NotifyAllCommands();
        }
    }

    private bool _isEmpty;
    public bool IsEmpty
    {
        get => _isEmpty;
        private set => SetProperty(ref _isEmpty, value);
    }

    private bool _isNoServerSelected = true;
    public bool IsNoServerSelected
    {
        get => _isNoServerSelected;
        private set => SetProperty(ref _isNoServerSelected, value);
    }

    // ── Editor panel ──────────────────────────────────────────────────────────

    private bool _editorVisible;
    public bool EditorVisible
    {
        get => _editorVisible;
        private set
        {
            SetProperty(ref _editorVisible, value);
            OnPropertyChanged(nameof(EditorPanelHeight));
            NotifyAllCommands();
        }
    }

    // Returns a GridLength so RowDefinition.Height can bind to it directly.
    public System.Windows.GridLength EditorPanelHeight =>
        _editorVisible
            ? new System.Windows.GridLength(2, System.Windows.GridUnitType.Star)
            : new System.Windows.GridLength(0);

    private string _editorFilePath = string.Empty;
    public string EditorFilePath
    {
        get => _editorFilePath;
        private set
        {
            SetProperty(ref _editorFilePath, value);
            OnPropertyChanged(nameof(EditorFileName));
        }
    }

    public string EditorFileName => Path.GetFileName(_editorFilePath);

    private string _editorContent = string.Empty;
    public string EditorContent
    {
        get => _editorContent;
        set
        {
            if (SetProperty(ref _editorContent, value))
                IsEditorModified = true;
        }
    }

    // tracks original content for modification detection
    private string _editorOriginalContent = string.Empty;

    private bool _isEditorModified;
    public bool IsEditorModified
    {
        get => _isEditorModified;
        private set
        {
            SetProperty(ref _isEditorModified, value);
            NotifyAllCommands();
        }
    }

    // ── Multi-select ──────────────────────────────────────────────────────────

    private List<FileEntryViewModel> _selectedEntries = new();
    public IReadOnlyList<FileEntryViewModel> SelectedEntries => _selectedEntries;

    public void SetSelectedEntries(IEnumerable<FileEntryViewModel> selected)
    {
        _selectedEntries = selected.ToList();
        NotifyAllCommands();
    }

    // ── Rename inline ─────────────────────────────────────────────────────────

    private bool _isRenaming;
    public bool IsRenaming
    {
        get => _isRenaming;
        private set => SetProperty(ref _isRenaming, value);
    }

    private string _renameText = string.Empty;
    public string RenameText
    {
        get => _renameText;
        set => SetProperty(ref _renameText, value);
    }

    private FileEntryViewModel? _renamingEntry;

    // ── Progress ──────────────────────────────────────────────────────────────

    private bool _progressVisible;
    public bool ProgressVisible
    {
        get => _progressVisible;
        private set => SetProperty(ref _progressVisible, value);
    }

    private double _progressValue;
    public double ProgressValue
    {
        get => _progressValue;
        set => SetProperty(ref _progressValue, value);
    }

    private string _progressLabel = string.Empty;
    public string ProgressLabel
    {
        get => _progressLabel;
        set => SetProperty(ref _progressLabel, value);
    }

    // ── All raw entries (pre-filter) ──────────────────────────────────────────
    private List<FileEntryViewModel> _allEntries = new();

    // ── Commands ──────────────────────────────────────────────────────────────

    public ICommand NavigateBackCommand { get; }
    public ICommand NavigateForwardCommand { get; }
    public ICommand NavigateUpCommand { get; }
    public ICommand NavigateHomeCommand { get; }
    public ICommand RefreshCommand { get; }
    public ICommand NavigateToCommand { get; }
    public ICommand OpenEntryCommand { get; }
    public ICommand CreateFolderCommand { get; }
    public ICommand CreateFileCommand { get; }
    public ICommand DeleteCommand { get; }
    public ICommand RenameCommand { get; }
    public ICommand CommitRenameCommand { get; }
    public ICommand CancelRenameCommand { get; }
    public ICommand CopyCommand { get; }
    public ICommand CutCommand { get; }
    public ICommand PasteCommand { get; }
    public ICommand UploadCommand { get; }
    public ICommand DownloadCommand { get; }
    public ICommand OpenInExplorerCommand { get; }
    public ICommand SortByNameCommand { get; }
    public ICommand SortByTypeCommand { get; }
    public ICommand SortBySizeCommand { get; }
    public ICommand SortByModifiedCommand { get; }

    // Editor commands
    public ICommand OpenEditorCommand { get; }
    public ICommand CloseEditorCommand { get; }
    public ICommand SaveFileCommand { get; }
    public ICommand SaveFileAsCommand { get; }
    public ICommand ReloadFileCommand { get; }

    public FileManagerViewModel(ServerProfile? initialProfile = null)
    {
        NavigateBackCommand = new RelayCommand(_ => _ = NavigateBackAsync(), () => _backStack.Count > 0 && HasServer && !IsLoading);
        NavigateForwardCommand = new RelayCommand(_ => _ = NavigateForwardAsync(), () => _forwardStack.Count > 0 && HasServer && !IsLoading);
        NavigateUpCommand = new RelayCommand(_ => _ = NavigateUpAsync(), () => CanGoUp() && HasServer && !IsLoading);
        NavigateHomeCommand = new RelayCommand(_ => _ = NavigateHomeAsync(), () => HasServer && !IsLoading);
        RefreshCommand = new RelayCommand(_ => _ = RefreshAsync(), () => HasServer && !IsLoading);
        NavigateToCommand = new RelayCommand(p => _ = NavigateToPathAsync(p as string ?? string.Empty));
        OpenEntryCommand = new RelayCommand(p => _ = OpenEntryAsync(p as FileEntryViewModel));

        CreateFolderCommand = new RelayCommand(_ => _ = CreateFolderAsync(), () => HasServer && !IsLoading);
        CreateFileCommand = new RelayCommand(_ => _ = CreateTextFileAsync(), () => HasServer && !IsLoading);
        DeleteCommand = new RelayCommand(_ => _ = DeleteSelectedAsync(), () => HasServer && !IsLoading && _selectedEntries.Any());
        RenameCommand = new RelayCommand(_ => StartRename(), () => HasServer && !IsLoading && _selectedEntries.Count == 1);
        CommitRenameCommand = new RelayCommand(_ => _ = CommitRenameAsync());
        CancelRenameCommand = new RelayCommand(_ => CancelRename());

        CopyCommand = new RelayCommand(_ => CopySelected(), () => HasServer && _selectedEntries.Any());
        CutCommand = new RelayCommand(_ => CutSelected(), () => HasServer && _selectedEntries.Any());
        PasteCommand = new RelayCommand(_ => _ = PasteAsync(), () => HasServer && !IsLoading && _clipboardPaths.Any());
        UploadCommand = new RelayCommand(_ => _ = UploadFilesAsync(), () => HasServer && !IsLoading);
        DownloadCommand = new RelayCommand(_ => _ = DownloadSelectedAsync(), () => HasServer && !IsLoading && _selectedEntries.Any(e => !e.IsDirectory));
        OpenInExplorerCommand = new RelayCommand(_ => OpenCurrentInExplorer(), () => HasServer);

        SortByNameCommand = new RelayCommand(_ => SortField = FileSortField.Name);
        SortByTypeCommand = new RelayCommand(_ => SortField = FileSortField.Type);
        SortBySizeCommand = new RelayCommand(_ => SortField = FileSortField.Size);
        SortByModifiedCommand = new RelayCommand(_ => SortField = FileSortField.Modified);

        OpenEditorCommand = new RelayCommand(p => _ = OpenEditorAsync(p as FileEntryViewModel ?? _selectedEntries.FirstOrDefault(e => !e.IsDirectory)),
            () => HasServer && (_selectedEntries.Count == 1 && !_selectedEntries[0].IsDirectory));
        CloseEditorCommand = new RelayCommand(_ => CloseEditor());
        SaveFileCommand = new RelayCommand(_ => _ = SaveEditorAsync(), () => EditorVisible && IsEditorModified);
        SaveFileAsCommand = new RelayCommand(_ => _ = SaveEditorAsAsync(), () => EditorVisible);
        ReloadFileCommand = new RelayCommand(_ => _ = ReloadEditorAsync(), () => EditorVisible);

        _ = LoadServersAsync(initialProfile);
    }

    // ── Initialisation ────────────────────────────────────────────────────────

    private async Task LoadServersAsync(ServerProfile? initialProfile)
    {
        try
        {
            var profiles = await _serversService.LoadServersAsync();
            Application.Current.Dispatcher.Invoke(() =>
            {
                Servers.Clear();
                foreach (var p in profiles)
                    Servers.Add(p);

                if (initialProfile != null)
                    SelectedServer = Servers.FirstOrDefault(s => s.Id == initialProfile.Id) ?? Servers.FirstOrDefault();
                else
                    SelectedServer = Servers.FirstOrDefault();
            });
        }
        catch (Exception ex)
        {
            SetStatus($"Failed to load server list: {ex.Message}", isError: true);
        }
    }

    private async Task OnServerSelectedAsync()
    {
        CloseEditor();
        _backStack.Clear();
        _forwardStack.Clear();
        _clipboardPaths.Clear();

        if (_selectedServer is null || string.IsNullOrWhiteSpace(_selectedServer.InstallPath))
        {
            HasServer = false;
            IsNoServerSelected = true;
            TreeNodes.Clear();
            Entries.Clear();
            _allEntries.Clear();
            Breadcrumbs.Clear();
            RootPath = string.Empty;
            CurrentPath = string.Empty;
            IsEmpty = false;
            SetStatus("Select a server to browse its files.", false);
            return;
        }

        var installPath = _selectedServer.InstallPath;
        if (!Directory.Exists(installPath))
        {
            HasServer = false;
            IsNoServerSelected = false;
            IsEmpty = true;
            SetStatus($"Install directory not found: {installPath}", isError: true);
            return;
        }

        RootPath = installPath;
        HasServer = true;
        IsNoServerSelected = false;
        await LoadFolderAsync(installPath, pushHistory: false);
        await LoadTreeAsync();
    }

    // ── Tree loading ──────────────────────────────────────────────────────────

    private async Task LoadTreeAsync()
    {
        var root = new FolderTreeNode(Path.GetFileName(RootPath) is { Length: > 0 } n ? n : RootPath, RootPath, addPlaceholder: false);
        root.IsExpanded = true;

        var (subs, _) = await _service.GetSubDirectoriesAsync(RootPath, RootPath);
        foreach (var sub in subs)
            root.Children.Add(new FolderTreeNode(sub.Name, sub.FullPath, addPlaceholder: Directory.EnumerateDirectories(sub.FullPath).Any()));

        Application.Current.Dispatcher.Invoke(() =>
        {
            TreeNodes.Clear();
            TreeNodes.Add(root);
        });
    }

    public async Task ExpandTreeNodeAsync(FolderTreeNode node)
    {
        node.ClearPlaceholder();
        var (subs, _) = await _service.GetSubDirectoriesAsync(node.FullPath, RootPath);
        Application.Current.Dispatcher.Invoke(() =>
        {
            node.Children.Clear();
            foreach (var sub in subs)
                node.Children.Add(new FolderTreeNode(sub.Name, sub.FullPath,
                    addPlaceholder: TryHasSubDirectories(sub.FullPath)));
        });
    }

    private bool TryHasSubDirectories(string path)
    {
        try { return Directory.EnumerateDirectories(path).Any(); }
        catch { return false; }
    }

    // ── Navigation ────────────────────────────────────────────────────────────

    public async Task NavigateToAsync(string path, bool pushHistory = true)
    {
        if (!_service.IsPathSafe(path, RootPath))
        {
            SetStatus("Access denied: path is outside the permitted directory.", isError: true);
            return;
        }
        await LoadFolderAsync(path, pushHistory);
    }

    private async Task NavigateToPathAsync(string path)
    {
        if (!string.IsNullOrWhiteSpace(path))
            await NavigateToAsync(path);
    }

    public async Task OpenEntryAsync(FileEntryViewModel? entry)
    {
        if (entry is null) return;

        if (entry.IsDirectory)
            await NavigateToAsync(entry.FullPath);
        else
            await OpenEditorAsync(entry);
    }

    private async Task NavigateBackAsync()
    {
        if (_backStack.Count == 0) return;
        _forwardStack.Push(CurrentPath);
        var path = _backStack.Pop();
        await LoadFolderAsync(path, pushHistory: false);
        NotifyAllCommands();
    }

    private async Task NavigateForwardAsync()
    {
        if (_forwardStack.Count == 0) return;
        _backStack.Push(CurrentPath);
        var path = _forwardStack.Pop();
        await LoadFolderAsync(path, pushHistory: false);
        NotifyAllCommands();
    }

    private async Task NavigateUpAsync()
    {
        if (!CanGoUp()) return;
        var parent = Path.GetDirectoryName(CurrentPath);
        if (parent is not null)
            await NavigateToAsync(parent);
    }

    private async Task NavigateHomeAsync()
    {
        if (!string.IsNullOrWhiteSpace(RootPath))
            await NavigateToAsync(RootPath);
    }

    private async Task RefreshAsync() => await LoadFolderAsync(CurrentPath, pushHistory: false);

    private bool CanGoUp()
    {
        if (string.IsNullOrWhiteSpace(CurrentPath) || string.IsNullOrWhiteSpace(RootPath))
            return false;
        var normalCurrent = Path.GetFullPath(CurrentPath);
        var normalRoot = Path.GetFullPath(RootPath);
        return !string.Equals(normalCurrent, normalRoot, StringComparison.OrdinalIgnoreCase);
    }

    private async Task LoadFolderAsync(string path, bool pushHistory)
    {
        if (IsLoading) return;

        IsLoading = true;
        SetStatus("Loading...", false);

        if (pushHistory && !string.IsNullOrWhiteSpace(CurrentPath))
            _backStack.Push(CurrentPath);

        if (pushHistory)
            _forwardStack.Clear();

        var (entries, error) = await _service.ListDirectoryAsync(path, RootPath);

        Application.Current.Dispatcher.Invoke(() =>
        {
            CurrentPath = path;
            _allEntries = entries.Select(e => new FileEntryViewModel(e)).ToList();
            ApplyFilter();
            BuildBreadcrumbs(path);
            IsEmpty = _allEntries.Count == 0;
            IsLoading = false;
            NotifyAllCommands();

            if (error is not null)
                SetStatus(error, isError: true);
            else
                SetStatus($"{_allEntries.Count} item{(_allEntries.Count == 1 ? "" : "s")}", false);
        });
    }

    // ── Filter / Sort ─────────────────────────────────────────────────────────

    private void ApplyFilter()
    {
        var filtered = string.IsNullOrWhiteSpace(SearchText)
            ? _allEntries
            : _allEntries.Where(e => e.Name.Contains(SearchText, StringComparison.OrdinalIgnoreCase)).ToList();

        IEnumerable<FileEntryViewModel> sorted = _sortField switch
        {
            FileSortField.Type => _sortDirection == FileSortDirection.Ascending
                ? filtered.OrderBy(e => e.IsDirectory ? 0 : 1).ThenBy(e => e.TypeDisplay).ThenBy(e => e.Name)
                : filtered.OrderByDescending(e => e.IsDirectory ? 0 : 1).ThenByDescending(e => e.TypeDisplay).ThenBy(e => e.Name),
            FileSortField.Size => _sortDirection == FileSortDirection.Ascending
                ? filtered.OrderBy(e => e.IsDirectory ? 0 : 1).ThenBy(e => e.SizeBytes)
                : filtered.OrderBy(e => e.IsDirectory ? 0 : 1).ThenByDescending(e => e.SizeBytes),
            FileSortField.Modified => _sortDirection == FileSortDirection.Ascending
                ? filtered.OrderBy(e => e.IsDirectory ? 0 : 1).ThenBy(e => e.Modified)
                : filtered.OrderBy(e => e.IsDirectory ? 0 : 1).ThenByDescending(e => e.Modified),
            _ => _sortDirection == FileSortDirection.Ascending
                ? filtered.OrderBy(e => e.IsDirectory ? 0 : 1).ThenBy(e => e.Name, StringComparer.OrdinalIgnoreCase)
                : filtered.OrderBy(e => e.IsDirectory ? 0 : 1).ThenByDescending(e => e.Name, StringComparer.OrdinalIgnoreCase)
        };

        var list = sorted.ToList();
        Entries.Clear();
        foreach (var e in list)
            Entries.Add(e);

        IsEmpty = list.Count == 0;
    }

    // ── Breadcrumbs ───────────────────────────────────────────────────────────

    private void BuildBreadcrumbs(string path)
    {
        Breadcrumbs.Clear();
        if (string.IsNullOrWhiteSpace(RootPath)) return;

        var normalRoot = Path.GetFullPath(RootPath);
        var normalPath = Path.GetFullPath(path);

        var segments = new List<BreadcrumbItem>();

        // Server root
        var rootLabel = _selectedServer?.ServerName is { Length: > 0 } sn ? sn : Path.GetFileName(normalRoot);
        segments.Add(new BreadcrumbItem(rootLabel, normalRoot));

        if (!string.Equals(normalPath, normalRoot, StringComparison.OrdinalIgnoreCase))
        {
            var relative = Path.GetRelativePath(normalRoot, normalPath);
            var parts = relative.Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries);
            var accumulated = normalRoot;
            foreach (var part in parts)
            {
                accumulated = Path.Combine(accumulated, part);
                segments.Add(new BreadcrumbItem(part, accumulated));
            }
        }

        for (int i = 0; i < segments.Count; i++)
            segments[i].IsLast = i == segments.Count - 1;

        foreach (var seg in segments)
            Breadcrumbs.Add(seg);
    }

    // ── File operations ───────────────────────────────────────────────────────

    private async Task CreateFolderAsync()
    {
        var name = PromptInput("Create Folder", "Folder name:", "New Folder");
        if (name is null) return;

        var result = await _service.CreateFolderAsync(CurrentPath, name, RootPath);
        if (result.Success)
        {
            SetStatus($"Folder '{name}' created.", false);
            await RefreshAsync();
        }
        else
        {
            SetStatus(result.Error ?? "Failed to create folder.", isError: true);
        }
    }

    private async Task CreateTextFileAsync()
    {
        var name = PromptInput("Create File", "File name:", "NewFile.txt");
        if (name is null) return;

        var result = await _service.CreateTextFileAsync(CurrentPath, name, RootPath);
        if (result.Success)
        {
            SetStatus($"File '{name}' created.", false);
            await RefreshAsync();
        }
        else
        {
            SetStatus(result.Error ?? "Failed to create file.", isError: true);
        }
    }

    private async Task DeleteSelectedAsync()
    {
        if (!_selectedEntries.Any()) return;

        var names = _selectedEntries.Count == 1
            ? $"'{_selectedEntries[0].Name}'"
            : $"{_selectedEntries.Count} items";

        var confirm = MessageBox.Show(
            $"Permanently delete {names}?\n\nThis cannot be undone.",
            "Confirm Delete",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (confirm != MessageBoxResult.Yes) return;

        var paths = _selectedEntries.Select(e => e.FullPath).ToList();
        var result = await _service.DeleteAsync(paths, RootPath);

        if (result.Success)
        {
            SetStatus($"Deleted {names}.", false);
            _selectedEntries.Clear();
            await RefreshAsync();
        }
        else
        {
            SetStatus(result.Error ?? "Delete failed.", isError: true);
        }
    }

    private void StartRename()
    {
        if (_selectedEntries.Count != 1) return;
        _renamingEntry = _selectedEntries[0];
        RenameText = _renamingEntry.Name;
        IsRenaming = true;
    }

    public async Task CommitRenameAsync()
    {
        if (_renamingEntry is null || string.IsNullOrWhiteSpace(RenameText)) { CancelRename(); return; }

        var newName = RenameText.Trim();
        if (string.Equals(newName, _renamingEntry.Name, StringComparison.OrdinalIgnoreCase))
        {
            CancelRename();
            return;
        }

        var result = await _service.RenameAsync(_renamingEntry.FullPath, newName, RootPath);
        IsRenaming = false;
        _renamingEntry = null;

        if (result.Success)
        {
            SetStatus($"Renamed to '{newName}'.", false);
            await RefreshAsync();
        }
        else
        {
            SetStatus(result.Error ?? "Rename failed.", isError: true);
        }
    }

    public void CancelRename()
    {
        IsRenaming = false;
        RenameText = string.Empty;
        _renamingEntry = null;
    }

    private void CopySelected()
    {
        _clipboardPaths = _selectedEntries.Select(e => e.FullPath).ToList();
        _clipboardIsCut = false;
        SetStatus($"Copied {_clipboardPaths.Count} item(s) to clipboard.", false);
        NotifyAllCommands();
    }

    private void CutSelected()
    {
        _clipboardPaths = _selectedEntries.Select(e => e.FullPath).ToList();
        _clipboardIsCut = true;
        SetStatus($"Cut {_clipboardPaths.Count} item(s) to clipboard.", false);
        NotifyAllCommands();
    }

    private async Task PasteAsync()
    {
        if (!_clipboardPaths.Any()) return;

        var progress = new Progress<(int done, int total, string current)>(p =>
        {
            ProgressLabel = p.total > 0 ? $"{(_clipboardIsCut ? "Moving" : "Copying")} {p.done}/{p.total}: {p.current}" : string.Empty;
            ProgressValue = p.total > 0 ? (double)p.done / p.total * 100 : 0;
        });

        ProgressVisible = true;

        FileManagerResult result;
        if (_clipboardIsCut)
        {
            result = await _service.MoveAsync(_clipboardPaths, CurrentPath, RootPath, progress);
            if (result.Success) _clipboardPaths.Clear();
        }
        else
        {
            result = await _service.CopyAsync(_clipboardPaths, CurrentPath, RootPath, progress);
        }

        ProgressVisible = false;
        ProgressLabel = string.Empty;

        if (result.Success)
        {
            SetStatus("Paste complete.", false);
            await RefreshAsync();
        }
        else
        {
            SetStatus(result.Error ?? "Paste failed.", isError: true);
        }
        NotifyAllCommands();
    }

    private async Task UploadFilesAsync()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Upload Files",
            Multiselect = true,
            Filter = "All files (*.*)|*.*"
        };

        if (dialog.ShowDialog() != true) return;

        ProgressVisible = true;
        var files = dialog.FileNames;
        int done = 0;

        foreach (var file in files)
        {
            ProgressLabel = $"Uploading {done + 1}/{files.Length}: {Path.GetFileName(file)}";
            ProgressValue = (double)done / files.Length * 100;

            var result = await _service.UploadFileAsync(file, CurrentPath, RootPath);
            if (!result.Success)
            {
                SetStatus($"Upload failed for '{Path.GetFileName(file)}': {result.Error}", isError: true);
                ProgressVisible = false;
                return;
            }
            done++;
        }

        ProgressVisible = false;
        ProgressLabel = string.Empty;
        SetStatus($"Uploaded {done} file(s).", false);
        await RefreshAsync();
    }

    private async Task DownloadSelectedAsync()
    {
        var fileEntries = _selectedEntries.Where(e => !e.IsDirectory).ToList();
        if (!fileEntries.Any()) return;

        if (fileEntries.Count == 1)
        {
            var entry = fileEntries[0];
            var dialog = new SaveFileDialog
            {
                Title = "Download File",
                FileName = entry.Name,
                Filter = "All files (*.*)|*.*"
            };
            if (dialog.ShowDialog() != true) return;

            ProgressVisible = true;
            ProgressLabel = $"Downloading {entry.Name}...";

            var progress = new Progress<double>(v => ProgressValue = v * 100);
            var result = await _service.DownloadFileAsync(entry.FullPath, dialog.FileName, RootPath, progress);

            ProgressVisible = false;
            SetStatus(result.Success ? $"Downloaded '{entry.Name}'." : (result.Error ?? "Download failed."), !result.Success);
        }
        else
        {
            var dialog = new OpenFolderDialog { Title = "Select download destination", Multiselect = false };
            if (dialog.ShowDialog() != true) return;

            ProgressVisible = true;
            int done = 0;

            foreach (var entry in fileEntries)
            {
                ProgressLabel = $"Downloading {done + 1}/{fileEntries.Count}: {entry.Name}";
                ProgressValue = (double)done / fileEntries.Count * 100;

                var destPath = Path.Combine(dialog.FolderName, entry.Name);
                var result = await _service.DownloadFileAsync(entry.FullPath, destPath, RootPath);
                if (!result.Success)
                {
                    SetStatus($"Download failed for '{entry.Name}': {result.Error}", isError: true);
                    ProgressVisible = false;
                    return;
                }
                done++;
            }

            ProgressVisible = false;
            SetStatus($"Downloaded {done} file(s).", false);
        }
    }

    private void OpenCurrentInExplorer()
    {
        var path = string.IsNullOrWhiteSpace(CurrentPath) ? RootPath : CurrentPath;
        if (!string.IsNullOrWhiteSpace(path))
        {
            try { System.Diagnostics.Process.Start("explorer.exe", path); }
            catch (Exception ex) { SetStatus($"Could not open Explorer: {ex.Message}", isError: true); }
        }
    }

    // ── Editor ────────────────────────────────────────────────────────────────

    private async Task OpenEditorAsync(FileEntryViewModel? entry)
    {
        if (entry is null || entry.IsDirectory) return;

        if (!_service.IsEditable(entry.Name))
        {
            SetStatus($"'{entry.Name}' is not a recognized text/config file. Only text files can be opened in the editor.", isError: true);
            return;
        }

        if (EditorVisible && IsEditorModified)
        {
            var save = MessageBox.Show(
                $"'{EditorFileName}' has unsaved changes.\nSave before opening another file?",
                "Unsaved Changes",
                MessageBoxButton.YesNoCancel,
                MessageBoxImage.Warning);

            if (save == MessageBoxResult.Cancel) return;
            if (save == MessageBoxResult.Yes) await SaveEditorAsync();
        }

        var (content, error) = await _service.ReadTextFileAsync(entry.FullPath, RootPath);
        if (error is not null)
        {
            SetStatus($"Cannot open file: {error}", isError: true);
            return;
        }

        EditorFilePath = entry.FullPath;
        _editorOriginalContent = content ?? string.Empty;
        // Set content without triggering IsEditorModified
        _editorContent = _editorOriginalContent;
        OnPropertyChanged(nameof(EditorContent));
        IsEditorModified = false;
        EditorVisible = true;
        SetStatus($"Opened '{entry.Name}'.", false);
    }

    private void CloseEditor()
    {
        if (EditorVisible && IsEditorModified)
        {
            var save = MessageBox.Show(
                $"'{EditorFileName}' has unsaved changes.\nSave before closing?",
                "Unsaved Changes",
                MessageBoxButton.YesNoCancel,
                MessageBoxImage.Warning);

            if (save == MessageBoxResult.Cancel) return;
            if (save == MessageBoxResult.Yes) _ = SaveEditorAsync();
        }

        EditorVisible = false;
        EditorFilePath = string.Empty;
        _editorContent = string.Empty;
        OnPropertyChanged(nameof(EditorContent));
        _editorOriginalContent = string.Empty;
        IsEditorModified = false;
    }

    private async Task SaveEditorAsync()
    {
        if (!EditorVisible || string.IsNullOrWhiteSpace(EditorFilePath)) return;

        var result = await _service.WriteTextFileAsync(EditorFilePath, EditorContent, RootPath);
        if (result.Success)
        {
            _editorOriginalContent = EditorContent;
            IsEditorModified = false;
            SetStatus($"Saved '{EditorFileName}'.", false);
            await RefreshAsync();
        }
        else
        {
            SetStatus(result.Error ?? "Save failed.", isError: true);
        }
    }

    private async Task SaveEditorAsAsync()
    {
        var dialog = new SaveFileDialog
        {
            Title = "Save As",
            FileName = EditorFileName,
            InitialDirectory = CurrentPath,
            Filter = "All files (*.*)|*.*"
        };

        if (dialog.ShowDialog() != true) return;

        // Check destination is inside root
        if (!_service.IsPathSafe(dialog.FileName, RootPath))
        {
            SetStatus("Cannot save outside the server directory.", isError: true);
            return;
        }

        var result = await _service.WriteTextFileAsync(dialog.FileName, EditorContent, RootPath);
        if (result.Success)
        {
            EditorFilePath = dialog.FileName;
            _editorOriginalContent = EditorContent;
            IsEditorModified = false;
            SetStatus($"Saved as '{EditorFileName}'.", false);
            await RefreshAsync();
        }
        else
        {
            SetStatus(result.Error ?? "Save failed.", isError: true);
        }
    }

    private async Task ReloadEditorAsync()
    {
        if (!EditorVisible || string.IsNullOrWhiteSpace(EditorFilePath)) return;

        if (IsEditorModified)
        {
            var confirm = MessageBox.Show(
                $"Reload '{EditorFileName}' and discard unsaved changes?",
                "Confirm Reload",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);
            if (confirm != MessageBoxResult.Yes) return;
        }

        var (content, error) = await _service.ReadTextFileAsync(EditorFilePath, RootPath);
        if (error is not null)
        {
            SetStatus($"Reload failed: {error}", isError: true);
            return;
        }

        _editorOriginalContent = content ?? string.Empty;
        _editorContent = _editorOriginalContent;
        OnPropertyChanged(nameof(EditorContent));
        IsEditorModified = false;
        SetStatus($"Reloaded '{EditorFileName}'.", false);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private void SetStatus(string message, bool isError)
    {
        StatusMessage = message;
        StatusIsError = isError;
    }

    private static string? PromptInput(string title, string label, string defaultValue = "")
    {
        var dialog = new Window
        {
            Title = title,
            Width = 380,
            Height = 160,
            WindowStartupLocation = WindowStartupLocation.CenterScreen,
            ResizeMode = ResizeMode.NoResize,
            Background = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromRgb(14, 24, 36)),
            WindowStyle = WindowStyle.ToolWindow
        };

        var stack = new System.Windows.Controls.StackPanel { Margin = new Thickness(20) };
        stack.Children.Add(new System.Windows.Controls.TextBlock
        {
            Text = label,
            Foreground = System.Windows.Media.Brushes.White,
            Margin = new Thickness(0, 0, 0, 8)
        });

        var textBox = new System.Windows.Controls.TextBox
        {
            Text = defaultValue,
            Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(12, 23, 38)),
            Foreground = System.Windows.Media.Brushes.White,
            BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(30, 51, 71)),
            Padding = new Thickness(8),
            CaretBrush = System.Windows.Media.Brushes.White,
            SelectionStart = 0,
            SelectionLength = defaultValue.Length
        };
        stack.Children.Add(textBox);

        var btnPanel = new System.Windows.Controls.StackPanel
        {
            Orientation = System.Windows.Controls.Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 14, 0, 0)
        };

        string? result = null;
        var ok = new System.Windows.Controls.Button
        {
            Content = "OK",
            Width = 70,
            Height = 30,
            Margin = new Thickness(0, 0, 8, 0),
            IsDefault = true,
            Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(30, 98, 232)),
            Foreground = System.Windows.Media.Brushes.White,
            BorderThickness = new Thickness(0)
        };
        ok.Click += (_, _) => { result = textBox.Text; dialog.DialogResult = true; };

        var cancel = new System.Windows.Controls.Button
        {
            Content = "Cancel",
            Width = 70,
            Height = 30,
            IsCancel = true,
            Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(21, 36, 54)),
            Foreground = System.Windows.Media.Brushes.White,
            BorderThickness = new Thickness(1),
            BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(37, 62, 86))
        };
        cancel.Click += (_, _) => { dialog.DialogResult = false; };

        btnPanel.Children.Add(ok);
        btnPanel.Children.Add(cancel);
        stack.Children.Add(btnPanel);
        dialog.Content = stack;

        textBox.Focus();
        return dialog.ShowDialog() == true ? result?.Trim() : null;
    }

    private void NotifyAllCommands()
    {
        ((RelayCommand)NavigateBackCommand).NotifyCanExecuteChanged();
        ((RelayCommand)NavigateForwardCommand).NotifyCanExecuteChanged();
        ((RelayCommand)NavigateUpCommand).NotifyCanExecuteChanged();
        ((RelayCommand)NavigateHomeCommand).NotifyCanExecuteChanged();
        ((RelayCommand)RefreshCommand).NotifyCanExecuteChanged();
        ((RelayCommand)CreateFolderCommand).NotifyCanExecuteChanged();
        ((RelayCommand)CreateFileCommand).NotifyCanExecuteChanged();
        ((RelayCommand)DeleteCommand).NotifyCanExecuteChanged();
        ((RelayCommand)RenameCommand).NotifyCanExecuteChanged();
        ((RelayCommand)CopyCommand).NotifyCanExecuteChanged();
        ((RelayCommand)CutCommand).NotifyCanExecuteChanged();
        ((RelayCommand)PasteCommand).NotifyCanExecuteChanged();
        ((RelayCommand)UploadCommand).NotifyCanExecuteChanged();
        ((RelayCommand)DownloadCommand).NotifyCanExecuteChanged();
        ((RelayCommand)OpenInExplorerCommand).NotifyCanExecuteChanged();
        ((RelayCommand)OpenEditorCommand).NotifyCanExecuteChanged();
        ((RelayCommand)SaveFileCommand).NotifyCanExecuteChanged();
        ((RelayCommand)SaveFileAsCommand).NotifyCanExecuteChanged();
        ((RelayCommand)ReloadFileCommand).NotifyCanExecuteChanged();
    }
}
