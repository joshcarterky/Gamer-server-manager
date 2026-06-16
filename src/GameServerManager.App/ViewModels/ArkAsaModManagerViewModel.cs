using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.IO;
using GameServerManager.Core.Models;
using GameServerManager.Services;
using GameServerManager.Services.CurseForge;
using Microsoft.Win32;

namespace GameServerManager.App.ViewModels;

public enum CurseForgePreviewState { Hidden, Loading, NeedsId, Found, Failed }

public sealed class ArkAsaModManagerViewModel : BaseViewModel
{
    private string _addModInput = string.Empty;
    private bool _addAsEnabled = true;
    private bool _addAsClusterWide;
    private string _searchText = string.Empty;
    private string _activeMapModId = string.Empty;
    private string _message = string.Empty;
    private bool _hasUnsavedChanges;
    private ArkSurvivalAscendedServerProfile? _profile;
    private bool _isBrowserOpen;

    // CurseForge preview state
    private CurseForgePreviewState _previewState = CurseForgePreviewState.Hidden;
    private string _previewSlug = string.Empty;
    private string _previewUrl = string.Empty;
    private string _previewName = string.Empty;
    private string _previewProjectId = string.Empty;
    private string _previewAuthor = string.Empty;
    private string _previewSummary = string.Empty;
    private string _previewManualId = string.Empty;
    private string _previewError = string.Empty;
    private CancellationTokenSource? _previewCts;

    public ArkAsaModManagerViewModel()
    {
        AllMods = new ObservableCollection<ArkModEntryViewModel>();
        FilteredMods = new ObservableCollection<ArkModEntryViewModel>();
        ValidationItems = new ObservableCollection<ModValidationItemViewModel>();

        AllMods.CollectionChanged += OnAllModsChanged;

        Browser = new CurseForgeModBrowserViewModel();
        Browser.ModsConfirmed += OnBrowserModsConfirmed;

        AddModCommand = new RelayCommand(_ => AddMods());
        ImportFromClipboardCommand = new RelayCommand(_ => ImportFromClipboard());
        ExportToClipboardCommand = new RelayCommand(_ => ExportToClipboard());
        ExportToFileCommand = new RelayCommand(_ => ExportToFile());
        ValidateCommand = new RelayCommand(_ => Validate());
        ClearDisabledCommand = new RelayCommand(_ => ClearDisabled());
        ClearAllCommand = new RelayCommand(_ => ClearAll());
        ClearMapModCommand = new RelayCommand(_ => ActiveMapModId = string.Empty);
        SortByNameCommand = new RelayCommand(_ => SortByName());
        SortByDateCommand = new RelayCommand(_ => SortByDate());
        AddFromPreviewCommand = new RelayCommand(_ => AddFromPreview());
        DismissPreviewCommand = new RelayCommand(_ => ClearPreview());
        OpenBrowserCommand = new RelayCommand(_ => OpenBrowser());
        CloseBrowserCommand = new RelayCommand(_ => CloseBrowser());
    }

    // ── Stats ────────────────────────────────────────────────
    public int TotalMods => AllMods.Count;
    public int EnabledCount => AllMods.Count(m => m.Enabled);
    public int DisabledCount => AllMods.Count(m => !m.Enabled);
    public int DuplicateCount => AllMods.GroupBy(m => m.ModId, StringComparer.OrdinalIgnoreCase).Count(g => g.Count() > 1);
    public bool HasDuplicates => DuplicateCount > 0;
    public bool HasUnsavedChanges { get => _hasUnsavedChanges; private set => SetProperty(ref _hasUnsavedChanges, value); }
    public bool IsEmpty => AllMods.Count == 0;
    public bool IsNotEmpty => AllMods.Count > 0;

    // ── Add Mod ──────────────────────────────────────────────
    public string AddModInput
    {
        get => _addModInput;
        set
        {
            if (SetProperty(ref _addModInput, value))
            {
                var trimmed = value.Trim();
                if (CurseForgeUrlParser.IsCurseForgeUrl(trimmed))
                {
                    _previewCts?.Cancel();
                    _previewCts = new CancellationTokenSource();
                    _ = LookupCurseForgeUrlAsync(trimmed, _previewCts.Token);
                }
                else if (_previewState != CurseForgePreviewState.Hidden)
                {
                    ClearPreview();
                }
            }
        }
    }

    public bool AddAsEnabled
    {
        get => _addAsEnabled;
        set => SetProperty(ref _addAsEnabled, value);
    }

    public bool AddAsClusterWide
    {
        get => _addAsClusterWide;
        set => SetProperty(ref _addAsClusterWide, value);
    }

    // ── Search ───────────────────────────────────────────────
    public string SearchText
    {
        get => _searchText;
        set { if (SetProperty(ref _searchText, value)) ApplyFilters(); }
    }

    // ── Map Mod ──────────────────────────────────────────────
    public string ActiveMapModId
    {
        get => _activeMapModId;
        set
        {
            if (SetProperty(ref _activeMapModId, value))
            {
                OnPropertyChanged(nameof(ActiveMapModName));
                OnPropertyChanged(nameof(HasActiveMapMod));
                MarkChanged();
            }
        }
    }

    public string ActiveMapModName
    {
        get
        {
            if (string.IsNullOrWhiteSpace(_activeMapModId)) return "(none)";
            var mod = AllMods.FirstOrDefault(m => m.ModId.Equals(_activeMapModId, StringComparison.OrdinalIgnoreCase));
            return mod != null && !string.IsNullOrWhiteSpace(mod.Name) && mod.Name != mod.ModId
                ? $"{mod.Name} ({_activeMapModId})"
                : _activeMapModId;
        }
    }

    public bool HasActiveMapMod => !string.IsNullOrWhiteSpace(_activeMapModId);

    // ── Status ───────────────────────────────────────────────
    public string Message
    {
        get => _message;
        set => SetProperty(ref _message, value);
    }

    // ── CurseForge Preview ───────────────────────────────────
    public bool IsPreviewVisible => _previewState != CurseForgePreviewState.Hidden;
    public bool IsPreviewLoading => _previewState == CurseForgePreviewState.Loading;
    public bool IsPreviewNeedsId => _previewState == CurseForgePreviewState.NeedsId;
    public bool IsPreviewFound => _previewState == CurseForgePreviewState.Found;
    public bool IsPreviewFailed => _previewState == CurseForgePreviewState.Failed;
    public string PreviewName => _previewName;
    public string PreviewProjectId => _previewProjectId;
    public string PreviewAuthor => _previewAuthor;
    public string PreviewAuthorText => string.IsNullOrWhiteSpace(_previewAuthor) ? string.Empty : $"by {_previewAuthor}";
    public string PreviewSummary => _previewSummary;
    public bool HasPreviewSummary => !string.IsNullOrWhiteSpace(_previewSummary);
    public string PreviewUrl => _previewUrl;
    public string PreviewError => _previewError;

    public string PreviewManualId
    {
        get => _previewManualId;
        set => SetProperty(ref _previewManualId, value);
    }

    // ── Browser ──────────────────────────────────────────────
    public CurseForgeModBrowserViewModel Browser { get; }

    public bool IsBrowserOpen
    {
        get => _isBrowserOpen;
        private set
        {
            if (SetProperty(ref _isBrowserOpen, value))
                OnPropertyChanged(nameof(IsBrowserClosed));
        }
    }

    public bool IsBrowserClosed => !_isBrowserOpen;

    // ── Collections ──────────────────────────────────────────
    public ObservableCollection<ArkModEntryViewModel> AllMods { get; }
    public ObservableCollection<ArkModEntryViewModel> FilteredMods { get; }
    public ObservableCollection<ModValidationItemViewModel> ValidationItems { get; }

    // ── Commands ─────────────────────────────────────────────
    public RelayCommand AddModCommand { get; }
    public RelayCommand ImportFromClipboardCommand { get; }
    public RelayCommand ExportToClipboardCommand { get; }
    public RelayCommand ExportToFileCommand { get; }
    public RelayCommand ValidateCommand { get; }
    public RelayCommand ClearDisabledCommand { get; }
    public RelayCommand ClearAllCommand { get; }
    public RelayCommand ClearMapModCommand { get; }
    public RelayCommand SortByNameCommand { get; }
    public RelayCommand SortByDateCommand { get; }
    public RelayCommand AddFromPreviewCommand { get; }
    public RelayCommand DismissPreviewCommand { get; }
    public RelayCommand OpenBrowserCommand { get; }
    public RelayCommand CloseBrowserCommand { get; }

    // ── Load / Save ──────────────────────────────────────────
    public void LoadFrom(ArkSurvivalAscendedServerProfile profile, AppSettings? settings = null)
    {
        _profile = profile;
        _activeMapModId = profile.Mods.ActiveMapModId ?? string.Empty;

        if (settings != null)
            Browser.Configure(settings);

        AllMods.Clear();
        var order = 1;
        foreach (var entry in profile.Mods.EnabledMods)
        {
            AllMods.Add(CreateEntry(entry, order++));
        }

        HasUnsavedChanges = false;
        ApplyFilters();
        Validate();
        NotifyAll();
    }

    public void ApplyTo(ArkSurvivalAscendedServerProfile profile)
    {
        profile.Mods.ActiveMapModId = _activeMapModId;
        profile.Mods.EnabledMods = AllMods.Select(m => new ArkModEntry
        {
            Id = m.ModId,
            Name = m.Name,
            Enabled = m.Enabled,
            IsMapMod = m.IsMapMod,
            ClusterWide = m.IsClusterWide,
            LoadOrder = m.LoadOrder,
            Source = m.Source,
            DateAdded = m.DateAdded,
            Notes = m.Notes,
            RequiredRestart = m.RequiredRestart,
            CurseForgeSlug = m.CurseForgeSlug,
            CurseForgeUrl = m.CurseForgeUrl,
            Author = m.Author,
            Summary = m.Summary
        }).ToList();

        profile.Mods.ModIDs = AllMods.Where(m => m.Enabled).Select(m => m.ModId).ToList();

        // Only numeric IDs written to INI — never URLs or slugs
        var enabledIds = string.Join(',', AllMods.Where(m => m.Enabled).Select(m => m.ModId));
        if (string.IsNullOrEmpty(enabledIds))
            profile.GameUserSettings.ServerSettings.Remove("ActiveMods");
        else
            profile.GameUserSettings.ServerSettings["ActiveMods"] = enabledIds;

        if (string.IsNullOrWhiteSpace(_activeMapModId))
            profile.GameUserSettings.ServerSettings.Remove("ActiveMapMod");
        else
            profile.GameUserSettings.ServerSettings["ActiveMapMod"] = _activeMapModId;
    }

    // ── Called by child entries ───────────────────────────────
    internal void NotifyStats()
    {
        OnPropertyChanged(nameof(TotalMods));
        OnPropertyChanged(nameof(EnabledCount));
        OnPropertyChanged(nameof(DisabledCount));
        OnPropertyChanged(nameof(DuplicateCount));
        OnPropertyChanged(nameof(HasDuplicates));
        OnPropertyChanged(nameof(IsEmpty));
        OnPropertyChanged(nameof(IsNotEmpty));
        OnPropertyChanged(nameof(ActiveMapModName));
        OnPropertyChanged(nameof(HasActiveMapMod));
    }

    internal void MoveUp(ArkModEntryViewModel mod)
    {
        var index = AllMods.IndexOf(mod);
        if (index <= 0) return;
        AllMods.Move(index, index - 1);
        RenumberLoadOrder();
        MarkChanged();
    }

    internal void MoveDown(ArkModEntryViewModel mod)
    {
        var index = AllMods.IndexOf(mod);
        if (index < 0 || index >= AllMods.Count - 1) return;
        AllMods.Move(index, index + 1);
        RenumberLoadOrder();
        MarkChanged();
    }

    internal void RemoveMod(ArkModEntryViewModel mod)
    {
        AllMods.Remove(mod);
        if (_activeMapModId.Equals(mod.ModId, StringComparison.OrdinalIgnoreCase))
            ActiveMapModId = string.Empty;
        RenumberLoadOrder();
        MarkChanged();
        Validate();
    }

    internal void SetAsMapMod(string modId) => ActiveMapModId = modId;

    // ── Private helpers ───────────────────────────────────────

    private void OpenBrowser()
    {
        IsBrowserOpen = true;
    }

    private void CloseBrowser()
    {
        IsBrowserOpen = false;
    }

    private void OnBrowserModsConfirmed(IReadOnlyList<CurseForgeModInfo> mods)
    {
        int added = 0, skipped = 0;
        foreach (var mod in mods)
        {
            var id = mod.ProjectId.ToString();
            if (AllMods.Any(m => m.ModId.Equals(id, StringComparison.OrdinalIgnoreCase)))
            {
                skipped++;
                continue;
            }

            AllMods.Add(new ArkModEntryViewModel(this)
            {
                ModId = id,
                Name = mod.Name,
                Enabled = _addAsEnabled,
                IsClusterWide = _addAsClusterWide,
                LoadOrder = AllMods.Count + 1,
                Source = ModSource.CurseForge,
                CurseForgeSlug = mod.Slug,
                CurseForgeUrl = mod.WebsiteUrl,
                Author = mod.Author,
                Summary = mod.Summary,
                DateAdded = DateTime.Now
            });
            added++;
        }

        RenumberLoadOrder();
        MarkChanged();
        Validate();

        Message = added > 0
            ? $"Added {added} mod(s) from CurseForge browser{(skipped > 0 ? $", skipped {skipped} duplicate(s)" : "")}."
            : $"All {skipped} selected mod(s) already in list.";

        IsBrowserOpen = false;
    }

    private void AddMods()
    {
        var input = _addModInput.Trim();
        if (string.IsNullOrWhiteSpace(input))
        {
            Message = "Enter one or more mod IDs or a CurseForge URL to add.";
            return;
        }

        if (CurseForgeUrlParser.IsCurseForgeUrl(input))
        {
            Message = "CurseForge URL detected — use the preview panel to confirm, then click Add Mod.";
            return;
        }

        var ids = input
            .Split(new[] { ',', '\n', '\r', ' ', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        int added = 0, skipped = 0, invalid = 0;
        foreach (var id in ids)
        {
            if (CurseForgeUrlParser.IsCurseForgeUrl(id))
            {
                Message = "Paste CurseForge URLs one at a time — the preview panel will appear automatically.";
                continue;
            }

            if (!long.TryParse(id, out _))
            {
                Message = $"Invalid mod ID '{id}' — IDs must be numeric. Skipped.";
                invalid++;
                continue;
            }

            if (AllMods.Any(m => m.ModId.Equals(id, StringComparison.OrdinalIgnoreCase)))
            {
                skipped++;
                continue;
            }

            AllMods.Add(new ArkModEntryViewModel(this)
            {
                ModId = id,
                Name = id,
                Enabled = _addAsEnabled,
                IsClusterWide = _addAsClusterWide,
                LoadOrder = AllMods.Count + 1,
                Source = ModSource.ASAModId,
                DateAdded = DateTime.Now
            });
            added++;
        }

        AddModInput = string.Empty;
        RenumberLoadOrder();
        MarkChanged();
        Validate();

        if (invalid == 0)
            Message = added > 0
                ? $"Added {added} mod(s){(skipped > 0 ? $", skipped {skipped} duplicate(s)" : "")}."
                : $"All {skipped} mod ID(s) already in list.";
    }

    private async Task LookupCurseForgeUrlAsync(string url, CancellationToken ct)
    {
        var parsed = CurseForgeUrlParser.Parse(url);

        if (!parsed.IsValid)
        {
            _previewError = "Could not parse as a CurseForge URL.";
            SetAndNotifyPreview(CurseForgePreviewState.Failed);
            return;
        }

        if (!parsed.IsArkAsa)
        {
            _previewError = $"URL is for '{parsed.GameSlug}', not ARK: Survival Ascended.";
            SetAndNotifyPreview(CurseForgePreviewState.Failed);
            return;
        }

        _previewSlug = parsed.ModSlug;
        _previewUrl = parsed.CleanUrl;
        _previewName = CurseForgeUrlParser.FormatSlugAsName(parsed.ModSlug);

        AppSettings? settings = null;
        try { settings = await new AppSettingsService().LoadAsync(); }
        catch { }

        if (ct.IsCancellationRequested) return;

        bool hasApiKey = settings?.CurseForgeEnabled == true
            && !string.IsNullOrWhiteSpace(settings.CurseForgeApiKey);

        if (hasApiKey && settings != null)
        {
            SetAndNotifyPreview(CurseForgePreviewState.Loading);

            try
            {
                using var service = new CurseForgeService(
                    settings.CurseForgeApiKey, settings.CurseForgeGameId, settings.CurseForgeTimeoutSeconds);
                var result = await service.LookupBySlugAsync(parsed.ModSlug, ct);

                if (ct.IsCancellationRequested) return;

                if (result.Status == CurseForgeLookupStatus.Success && result.Mod != null)
                {
                    _previewName = result.Mod.Name;
                    _previewProjectId = result.Mod.ProjectId.ToString();
                    _previewAuthor = result.Mod.Author;
                    _previewSummary = result.Mod.Summary;
                    PreviewManualId = result.Mod.ProjectId.ToString();
                    SetAndNotifyPreview(CurseForgePreviewState.Found);
                }
                else
                {
                    _previewError = result.ErrorMessage;
                    SetAndNotifyPreview(CurseForgePreviewState.Failed);
                }
            }
            catch (OperationCanceledException) { return; }
            catch (Exception ex)
            {
                _previewError = $"Lookup error: {ex.Message}";
                SetAndNotifyPreview(CurseForgePreviewState.Failed);
            }
        }
        else
        {
            PreviewManualId = string.Empty;
            SetAndNotifyPreview(CurseForgePreviewState.NeedsId);
        }
    }

    private void SetAndNotifyPreview(CurseForgePreviewState state)
    {
        _previewState = state;
        NotifyPreview();
    }

    private void NotifyPreview()
    {
        OnPropertyChanged(nameof(IsPreviewVisible));
        OnPropertyChanged(nameof(IsPreviewLoading));
        OnPropertyChanged(nameof(IsPreviewNeedsId));
        OnPropertyChanged(nameof(IsPreviewFound));
        OnPropertyChanged(nameof(IsPreviewFailed));
        OnPropertyChanged(nameof(PreviewName));
        OnPropertyChanged(nameof(PreviewProjectId));
        OnPropertyChanged(nameof(PreviewAuthor));
        OnPropertyChanged(nameof(PreviewAuthorText));
        OnPropertyChanged(nameof(PreviewSummary));
        OnPropertyChanged(nameof(HasPreviewSummary));
        OnPropertyChanged(nameof(PreviewUrl));
        OnPropertyChanged(nameof(PreviewError));
    }

    private void ClearPreview()
    {
        _previewCts?.Cancel();
        _previewState = CurseForgePreviewState.Hidden;
        _previewSlug = string.Empty;
        _previewUrl = string.Empty;
        _previewName = string.Empty;
        _previewProjectId = string.Empty;
        _previewAuthor = string.Empty;
        _previewSummary = string.Empty;
        _previewError = string.Empty;
        PreviewManualId = string.Empty;
        NotifyPreview();
    }

    private void AddFromPreview()
    {
        var id = _previewState == CurseForgePreviewState.Found
            ? _previewProjectId
            : _previewManualId.Trim();

        if (string.IsNullOrWhiteSpace(id) || !long.TryParse(id, out _))
        {
            Message = "Enter the numeric Project ID from the mod's CurseForge page.";
            return;
        }

        if (AllMods.Any(m => m.ModId.Equals(id, StringComparison.OrdinalIgnoreCase)))
        {
            Message = $"Mod ID {id} is already in the list.";
            AddModInput = string.Empty;
            ClearPreview();
            return;
        }

        AllMods.Add(new ArkModEntryViewModel(this)
        {
            ModId = id,
            Name = string.IsNullOrWhiteSpace(_previewName) ? id : _previewName,
            Enabled = _addAsEnabled,
            IsClusterWide = _addAsClusterWide,
            LoadOrder = AllMods.Count + 1,
            Source = ModSource.CurseForge,
            CurseForgeSlug = _previewSlug,
            CurseForgeUrl = _previewUrl,
            Author = _previewAuthor,
            Summary = _previewSummary,
            DateAdded = DateTime.Now
        });

        var displayName = string.IsNullOrWhiteSpace(_previewName) ? id : _previewName;
        Message = $"Added '{displayName}' (ID: {id}) from CurseForge.";
        AddModInput = string.Empty;
        ClearPreview();
        RenumberLoadOrder();
        MarkChanged();
        Validate();
    }

    private void ImportFromClipboard()
    {
        var text = System.Windows.Clipboard.GetText().Trim();
        if (string.IsNullOrWhiteSpace(text)) { Message = "Clipboard is empty."; return; }

        // Handle "ActiveMods=1,2,3" or "-mods=1,2,3" formats
        if (text.Contains('='))
            text = text[(text.IndexOf('=') + 1)..];

        AddModInput = text.Trim();
        if (!CurseForgeUrlParser.IsCurseForgeUrl(AddModInput))
            AddMods();
    }

    private void ExportToClipboard()
    {
        var ids = string.Join(',', AllMods.Where(m => m.Enabled).Select(m => m.ModId));
        ClipboardService.SetText(ids);
        Message = $"Copied {AllMods.Count(m => m.Enabled)} enabled mod ID(s) to clipboard.";
    }

    private void ExportToFile()
    {
        var dialog = new SaveFileDialog
        {
            Title = "Export Mod List",
            Filter = "Text files (*.txt)|*.txt|All files (*.*)|*.*",
            DefaultExt = ".txt",
            FileName = "mods.txt"
        };

        if (dialog.ShowDialog() != true) return;

        var lines = new List<string>
        {
            "# ARK ASA Mod List",
            $"# Exported: {DateTime.Now:yyyy-MM-dd HH:mm}",
            $"# Total: {TotalMods}  Enabled: {EnabledCount}",
            $"# ActiveMapMod: {(HasActiveMapMod ? _activeMapModId : "(none)")}",
            string.Empty
        };
        lines.AddRange(AllMods.Select(m =>
            $"{m.ModId},{(m.Enabled ? "1" : "0")},{m.IsMapMod},{m.IsClusterWide},{m.Name},{m.Notes}"));

        try
        {
            File.WriteAllLines(dialog.FileName, lines);
            Message = $"Exported {AllMods.Count} mod(s) to {Path.GetFileName(dialog.FileName)}.";
        }
        catch (Exception ex)
        {
            Message = $"Export failed: {ex.Message}";
        }
    }

    private void ClearDisabled()
    {
        var toRemove = AllMods.Where(m => !m.Enabled).ToList();
        foreach (var mod in toRemove) AllMods.Remove(mod);
        RenumberLoadOrder();
        MarkChanged();
        Validate();
        Message = $"Removed {toRemove.Count} disabled mod(s).";
    }

    private void ClearAll()
    {
        AllMods.Clear();
        ActiveMapModId = string.Empty;
        MarkChanged();
        Validate();
        Message = "Cleared all mods.";
    }

    private void SortByName()
    {
        var sorted = AllMods.OrderBy(m => m.Name, StringComparer.OrdinalIgnoreCase).ToList();
        AllMods.Clear();
        foreach (var m in sorted) AllMods.Add(m);
        RenumberLoadOrder();
        MarkChanged();
        Message = "Sorted mods by name.";
    }

    private void SortByDate()
    {
        var sorted = AllMods.OrderBy(m => m.DateAdded ?? DateTime.MaxValue).ToList();
        AllMods.Clear();
        foreach (var m in sorted) AllMods.Add(m);
        RenumberLoadOrder();
        MarkChanged();
        Message = "Sorted mods by date added.";
    }

    private void Validate()
    {
        ValidationItems.Clear();

        var duplicates = AllMods
            .GroupBy(m => m.ModId, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1)
            .ToList();

        foreach (var dup in duplicates)
        {
            ValidationItems.Add(new ModValidationItemViewModel("#FF4444", "Error",
                $"Duplicate mod ID: {dup.Key} appears {dup.Count()} times."));
        }

        foreach (var mod in AllMods.Where(m => !string.IsNullOrWhiteSpace(m.ModId) && !long.TryParse(m.ModId, out _)))
        {
            ValidationItems.Add(new ModValidationItemViewModel("#FF4444", "Error",
                $"Invalid mod ID '{mod.ModId}' — must be numeric."));
        }

        if (AllMods.Any(m => string.IsNullOrWhiteSpace(m.ModId)))
            ValidationItems.Add(new ModValidationItemViewModel("#FF4444", "Error", "One or more mods have empty IDs."));

        if (_profile != null && string.IsNullOrWhiteSpace(_profile.Basic.InstallPath))
            ValidationItems.Add(new ModValidationItemViewModel("#F7B267", "Warning",
                "Install path is not set. Cannot check if mods are installed."));

        if (!string.IsNullOrWhiteSpace(_activeMapModId) &&
            !AllMods.Any(m => m.ModId.Equals(_activeMapModId, StringComparison.OrdinalIgnoreCase)))
        {
            ValidationItems.Add(new ModValidationItemViewModel("#F7B267", "Warning",
                $"Active map mod '{_activeMapModId}' is not in the mod list."));
        }

        if (!ValidationItems.Any())
        {
            if (AllMods.Any())
                ValidationItems.Add(new ModValidationItemViewModel("#4CAF50", "OK",
                    $"{EnabledCount} enabled mod(s) — no issues detected."));
            else
                ValidationItems.Add(new ModValidationItemViewModel("#7AA8C8", "Info",
                    "No mods configured. Add a mod ID or CurseForge URL above to get started."));
        }

        foreach (var mod in AllMods)
        {
            if (duplicates.Any(g => g.Key.Equals(mod.ModId, StringComparison.OrdinalIgnoreCase)))
                mod.ValidationStatus = ModValidationStatus.Duplicate;
            else if (string.IsNullOrWhiteSpace(mod.ModId))
                mod.ValidationStatus = ModValidationStatus.MissingId;
            else if (!long.TryParse(mod.ModId, out _))
                mod.ValidationStatus = ModValidationStatus.InvalidId;
            else
                mod.ValidationStatus = ModValidationStatus.Valid;
        }

        NotifyStats();
    }

    private void ApplyFilters()
    {
        FilteredMods.Clear();
        var source = string.IsNullOrWhiteSpace(_searchText)
            ? AllMods
            : (IEnumerable<ArkModEntryViewModel>)AllMods.Where(m =>
                m.ModId.Contains(_searchText, StringComparison.OrdinalIgnoreCase) ||
                m.Name.Contains(_searchText, StringComparison.OrdinalIgnoreCase) ||
                m.Notes.Contains(_searchText, StringComparison.OrdinalIgnoreCase));

        foreach (var mod in source) FilteredMods.Add(mod);
    }

    private void RenumberLoadOrder()
    {
        for (int i = 0; i < AllMods.Count; i++)
            AllMods[i].LoadOrder = i + 1;
    }

    private void MarkChanged()
    {
        HasUnsavedChanges = true;
        NotifyStats();
    }

    private void NotifyAll()
    {
        NotifyStats();
        OnPropertyChanged(nameof(AddModInput));
    }

    private void OnAllModsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        NotifyStats();
        ApplyFilters();
    }

    private ArkModEntryViewModel CreateEntry(ArkModEntry entry, int order) => new(this)
    {
        ModId = entry.Id,
        Name = string.IsNullOrWhiteSpace(entry.Name) ? entry.Id : entry.Name,
        Enabled = entry.Enabled,
        IsMapMod = entry.IsMapMod,
        IsClusterWide = entry.ClusterWide,
        LoadOrder = order,
        Source = entry.Source,
        DateAdded = entry.DateAdded,
        Notes = entry.Notes,
        RequiredRestart = entry.RequiredRestart,
        CurseForgeSlug = entry.CurseForgeSlug,
        CurseForgeUrl = entry.CurseForgeUrl,
        Author = entry.Author,
        Summary = entry.Summary
    };
}

// ── Per-mod ViewModel ────────────────────────────────────────
public sealed class ArkModEntryViewModel : BaseViewModel
{
    private readonly ArkAsaModManagerViewModel _parent;
    private string _modId = string.Empty;
    private string _name = string.Empty;
    private bool _enabled = true;
    private bool _isMapMod;
    private int _loadOrder;
    private ModValidationStatus _validationStatus = ModValidationStatus.Valid;
    private bool _isClusterWide;
    private string _notes = string.Empty;
    private bool _requiredRestart = true;

    public ArkModEntryViewModel(ArkAsaModManagerViewModel parent)
    {
        _parent = parent;
        MoveUpCommand = new RelayCommand(_ => _parent.MoveUp(this));
        MoveDownCommand = new RelayCommand(_ => _parent.MoveDown(this));
        RemoveCommand = new RelayCommand(_ => _parent.RemoveMod(this));
        CopyIdCommand = new RelayCommand(_ => ClipboardService.SetText(_modId));
        SetAsMapModCommand = new RelayCommand(_ => _parent.SetAsMapMod(_modId));
    }

    public string ModId
    {
        get => _modId;
        set { SetProperty(ref _modId, value); OnPropertyChanged(nameof(DisplayName)); }
    }

    public string Name
    {
        get => _name;
        set { SetProperty(ref _name, value); OnPropertyChanged(nameof(DisplayName)); }
    }

    public string DisplayName => !string.IsNullOrWhiteSpace(_name) && _name != _modId
        ? $"{_name}"
        : _modId;

    public bool Enabled
    {
        get => _enabled;
        set { SetProperty(ref _enabled, value); _parent.NotifyStats(); }
    }

    public bool IsMapMod
    {
        get => _isMapMod;
        set => SetProperty(ref _isMapMod, value);
    }

    public int LoadOrder
    {
        get => _loadOrder;
        set => SetProperty(ref _loadOrder, value);
    }

    public ModValidationStatus ValidationStatus
    {
        get => _validationStatus;
        set
        {
            SetProperty(ref _validationStatus, value);
            OnPropertyChanged(nameof(StatusBadgeText));
            OnPropertyChanged(nameof(StatusBadgeColor));
            OnPropertyChanged(nameof(HasWarning));
        }
    }

    public bool IsClusterWide
    {
        get => _isClusterWide;
        set => SetProperty(ref _isClusterWide, value);
    }

    public string Notes
    {
        get => _notes;
        set => SetProperty(ref _notes, value);
    }

    public bool RequiredRestart
    {
        get => _requiredRestart;
        set => SetProperty(ref _requiredRestart, value);
    }

    public ModSource Source { get; set; } = ModSource.Unknown;
    public DateTime? DateAdded { get; set; }

    // CurseForge metadata — stored in profile JSON, never written to INI
    public string CurseForgeSlug { get; set; } = string.Empty;
    public string CurseForgeUrl { get; set; } = string.Empty;
    public string Author { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;

    public string StatusBadgeText => _validationStatus switch
    {
        ModValidationStatus.Duplicate => "Duplicate",
        ModValidationStatus.InvalidId => "Invalid ID",
        ModValidationStatus.MissingId => "Missing ID",
        ModValidationStatus.MapModConflict => "Map Conflict",
        _ => _enabled ? "Enabled" : "Disabled"
    };

    public string StatusBadgeColor => _validationStatus switch
    {
        ModValidationStatus.Duplicate => "#CC3333",
        ModValidationStatus.InvalidId => "#CC3333",
        ModValidationStatus.MissingId => "#CC3333",
        ModValidationStatus.MapModConflict => "#CC7700",
        _ => _enabled ? "#1E6B47" : "#3A4A5A"
    };

    public bool HasWarning => _validationStatus != ModValidationStatus.Valid;

    public RelayCommand MoveUpCommand { get; }
    public RelayCommand MoveDownCommand { get; }
    public RelayCommand RemoveCommand { get; }
    public RelayCommand CopyIdCommand { get; }
    public RelayCommand SetAsMapModCommand { get; }
}

// ── Validation item ──────────────────────────────────────────
public sealed class ModValidationItemViewModel
{
    public ModValidationItemViewModel(string color, string severity, string message)
    {
        Color = color;
        Severity = severity;
        Message = message;
    }

    public string Color { get; }
    public string Severity { get; }
    public string Message { get; }
}
