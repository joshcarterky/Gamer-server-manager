using System.Collections.ObjectModel;
using GameServerManager.Core.Models;
using GameServerManager.Services;
using GameServerManager.Services.CurseForge;

namespace GameServerManager.App.ViewModels;

// ── Result card shown in the search grid ────────────────────────────────────

public sealed class CurseForgeResultViewModel : BaseViewModel
{
    private bool _isSelected;

    public CurseForgeModInfo Mod { get; }

    public int ProjectId => Mod.ProjectId;
    public string Name => Mod.Name;
    public string Author => string.IsNullOrEmpty(Mod.Author) ? "Unknown" : Mod.Author;
    public string Summary => Mod.Summary;
    public string IconUrl => Mod.IconUrl;
    public string DownloadCountText => Mod.DownloadCount >= 1_000_000
        ? $"{Mod.DownloadCount / 1_000_000.0:F1}M downloads"
        : Mod.DownloadCount >= 1_000
            ? $"{Mod.DownloadCount / 1_000.0:F1}K downloads"
            : $"{Mod.DownloadCount} downloads";
    public string UpdatedText => Mod.DateModified.HasValue
        ? $"Updated {Mod.DateModified.Value:MMM d, yyyy}"
        : string.Empty;

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }

    public CurseForgeResultViewModel(CurseForgeModInfo mod) => Mod = mod;
}

// ── Item in the "selected mods" review cart ──────────────────────────────────

public sealed class BrowserSelectedModViewModel : BaseViewModel
{
    public CurseForgeResultViewModel Source { get; }

    public int ProjectId => Source.ProjectId;
    public string Name => Source.Name;
    public string Author => Source.Author;

    public RelayCommand RemoveCommand { get; }

    public BrowserSelectedModViewModel(CurseForgeResultViewModel source, Action<BrowserSelectedModViewModel> onRemove)
    {
        Source = source;
        RemoveCommand = new RelayCommand(_ => onRemove(this));
    }
}

// ── Main browser ViewModel ────────────────────────────────────────────────────

public enum BrowserState { Idle, Loading, Results, Error, NoApiKey }

public sealed class CurseForgeModBrowserViewModel : BaseViewModel
{
    private string _searchText = string.Empty;
    private BrowserState _state = BrowserState.Idle;
    private string _statusMessage = string.Empty;
    private int _currentPage = 0;
    private int _totalCount = 0;
    private const int PageSize = 20;
    private int _sortField = 2; // Popularity
    private CancellationTokenSource? _searchCts;

    private AppSettings? _appSettings;

    public CurseForgeModBrowserViewModel()
    {
        Results = new ObservableCollection<CurseForgeResultViewModel>();
        SelectedMods = new ObservableCollection<BrowserSelectedModViewModel>();

        SearchCommand = new RelayCommand(_ => _ = ExecuteSearchAsync(reset: true));
        LoadMoreCommand = new RelayCommand(_ => _ = ExecuteSearchAsync(reset: false));
        AddSelectedCommand = new RelayCommand(_ => AddSelectedToList());
        ClearSelectionCommand = new RelayCommand(_ => ClearSelection());
        ToggleResultCommand = new RelayCommand(p => { if (p is CurseForgeResultViewModel vm) ToggleResult(vm); });

        SortByPopularityCommand = new RelayCommand(_ => SetSort(2));
        SortByUpdatedCommand = new RelayCommand(_ => SetSort(3));
        SortByNameCommand = new RelayCommand(_ => SetSort(4));
        SortByDownloadsCommand = new RelayCommand(_ => SetSort(6));
    }

    // ── Search bar ───────────────────────────────────────────────────────────

    public string SearchText
    {
        get => _searchText;
        set => SetProperty(ref _searchText, value);
    }

    // ── State ────────────────────────────────────────────────────────────────

    public BrowserState State
    {
        get => _state;
        private set
        {
            if (SetProperty(ref _state, value))
            {
                OnPropertyChanged(nameof(IsIdle));
                OnPropertyChanged(nameof(IsLoading));
                OnPropertyChanged(nameof(HasResults));
                OnPropertyChanged(nameof(HasError));
                OnPropertyChanged(nameof(NeedsApiKey));
            }
        }
    }

    public bool IsIdle => _state == BrowserState.Idle;
    public bool IsLoading => _state == BrowserState.Loading;
    public bool HasResults => _state == BrowserState.Results;
    public bool HasError => _state == BrowserState.Error;
    public bool NeedsApiKey => _state == BrowserState.NoApiKey;

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    // ── Pagination ───────────────────────────────────────────────────────────

    public bool CanLoadMore => _state == BrowserState.Results && Results.Count < _totalCount;

    public string ResultCountText => _totalCount > 0
        ? $"Showing {Results.Count} of {_totalCount} results"
        : string.Empty;

    // ── Results + selection ──────────────────────────────────────────────────

    public ObservableCollection<CurseForgeResultViewModel> Results { get; }
    public ObservableCollection<BrowserSelectedModViewModel> SelectedMods { get; }

    public bool HasSelectedMods => SelectedMods.Count > 0;
    public string SelectedCountText => SelectedMods.Count == 1
        ? "1 mod selected"
        : $"{SelectedMods.Count} mods selected";

    // ── Sort ─────────────────────────────────────────────────────────────────

    public int SortField
    {
        get => _sortField;
        private set => SetProperty(ref _sortField, value);
    }

    public bool SortIsPopularity => _sortField == 2;
    public bool SortIsUpdated => _sortField == 3;
    public bool SortIsName => _sortField == 4;
    public bool SortIsDownloads => _sortField == 6;

    // ── Commands ─────────────────────────────────────────────────────────────

    public RelayCommand SearchCommand { get; }
    public RelayCommand LoadMoreCommand { get; }
    public RelayCommand AddSelectedCommand { get; }
    public RelayCommand ClearSelectionCommand { get; }
    public RelayCommand ToggleResultCommand { get; }
    public RelayCommand SortByPopularityCommand { get; }
    public RelayCommand SortByUpdatedCommand { get; }
    public RelayCommand SortByNameCommand { get; }
    public RelayCommand SortByDownloadsCommand { get; }

    // ── Callback: raised when user confirms "Add to server" ──────────────────

    public event Action<IReadOnlyList<CurseForgeModInfo>>? ModsConfirmed;

    // ── Public API ────────────────────────────────────────────────────────────

    public void Configure(AppSettings settings)
    {
        _appSettings = settings;

        if (!settings.CurseForgeEnabled || string.IsNullOrWhiteSpace(settings.CurseForgeApiKey))
        {
            State = BrowserState.NoApiKey;
            StatusMessage = "CurseForge API key is not configured. Go to Settings → Integrations to add one.";
        }
        else
        {
            State = BrowserState.Idle;
            StatusMessage = string.Empty;
        }
    }

    public void ToggleResult(CurseForgeResultViewModel result)
    {
        if (result.IsSelected)
        {
            // Deselect
            result.IsSelected = false;
            var existing = SelectedMods.FirstOrDefault(s => s.ProjectId == result.ProjectId);
            if (existing != null) SelectedMods.Remove(existing);
        }
        else
        {
            // Check if already in the list
            if (SelectedMods.Any(s => s.ProjectId == result.ProjectId)) return;
            result.IsSelected = true;
            SelectedMods.Add(new BrowserSelectedModViewModel(result, RemoveFromSelection));
        }

        OnPropertyChanged(nameof(HasSelectedMods));
        OnPropertyChanged(nameof(SelectedCountText));
    }

    // ── Private helpers ──────────────────────────────────────────────────────

    private void SetSort(int field)
    {
        if (_sortField == field) return;
        _sortField = field;
        OnPropertyChanged(nameof(SortField));
        OnPropertyChanged(nameof(SortIsPopularity));
        OnPropertyChanged(nameof(SortIsUpdated));
        OnPropertyChanged(nameof(SortIsName));
        OnPropertyChanged(nameof(SortIsDownloads));
        // Only re-fetch if results are already showing; otherwise just update the sort preference
        if (_state == BrowserState.Results)
            _ = ExecuteSearchAsync(reset: true);
    }

    private async Task ExecuteSearchAsync(bool reset)
    {
        if (_appSettings is null) return;

        if (!_appSettings.CurseForgeEnabled || string.IsNullOrWhiteSpace(_appSettings.CurseForgeApiKey))
        {
            State = BrowserState.NoApiKey;
            StatusMessage = "CurseForge API key is not configured. Go to Settings → Integrations to add one.";
            return;
        }

        _searchCts?.Cancel();
        _searchCts = new CancellationTokenSource();
        var ct = _searchCts.Token;

        if (reset)
        {
            _currentPage = 0;
            Results.Clear();
            _totalCount = 0;
        }

        State = BrowserState.Loading;
        StatusMessage = string.Empty;

        try
        {
            using var service = new CurseForgeService(
                _appSettings.CurseForgeApiKey,
                _appSettings.CurseForgeGameId,
                _appSettings.CurseForgeTimeoutSeconds);

            var request = new CurseForgeSearchRequest
            {
                SearchFilter = _searchText.Trim(),
                SortField = _sortField,
                SortOrder = "desc",
                PageSize = PageSize,
                Index = _currentPage * PageSize
            };

            var result = await service.SearchAsync(request, ct);

            if (ct.IsCancellationRequested) return;

            if (!result.IsSuccess)
            {
                State = BrowserState.Error;
                StatusMessage = result.ErrorMessage;
                return;
            }

            _totalCount = result.TotalCount;
            _currentPage++;

            // Track which IDs are currently selected so cards stay highlighted across pages
            var selectedIds = SelectedMods.Select(s => s.ProjectId).ToHashSet();

            foreach (var mod in result.Mods)
            {
                var vm = new CurseForgeResultViewModel(mod);
                if (selectedIds.Contains(mod.ProjectId))
                    vm.IsSelected = true;
                Results.Add(vm);
            }

            State = BrowserState.Results;

            if (Results.Count == 0)
                StatusMessage = "No mods found. Try a different search term.";
            else
                StatusMessage = string.Empty;

            OnPropertyChanged(nameof(CanLoadMore));
            OnPropertyChanged(nameof(ResultCountText));
        }
        catch (OperationCanceledException)
        {
            // User cancelled or search replaced — silently ignore
        }
        catch (Exception ex)
        {
            if (!ct.IsCancellationRequested)
            {
                State = BrowserState.Error;
                StatusMessage = $"Unexpected error: {ex.Message}";
            }
        }
    }

    private void AddSelectedToList()
    {
        if (SelectedMods.Count == 0) return;
        var mods = SelectedMods.Select(s => s.Source.Mod).ToList();
        ModsConfirmed?.Invoke(mods);
        ClearSelection();
    }

    private void ClearSelection()
    {
        foreach (var item in SelectedMods)
            item.Source.IsSelected = false;
        SelectedMods.Clear();
        OnPropertyChanged(nameof(HasSelectedMods));
        OnPropertyChanged(nameof(SelectedCountText));
    }

    private void RemoveFromSelection(BrowserSelectedModViewModel item)
    {
        item.Source.IsSelected = false;
        SelectedMods.Remove(item);
        OnPropertyChanged(nameof(HasSelectedMods));
        OnPropertyChanged(nameof(SelectedCountText));
    }
}
