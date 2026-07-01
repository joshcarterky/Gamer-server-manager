using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using GameServerManager.Core.Models;
using GameServerManager.GameProviders;
using GameServerManager.Services;
using GameServerManager.Services.SevenDaysToDie;
using Microsoft.Win32;

namespace GameServerManager.App.ViewModels;

// ─── Category summary ─────────────────────────────────────────────────────────

public sealed class DtdCategoryViewModel : BaseViewModel
{
    private int _errorCount;
    private int _warningCount;

    public string Name { get; init; } = string.Empty;

    public int ErrorCount
    {
        get => _errorCount;
        set { _errorCount = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasErrors)); OnPropertyChanged(nameof(HasIssues)); }
    }

    public int WarningCount
    {
        get => _warningCount;
        set { _warningCount = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasWarnings)); OnPropertyChanged(nameof(HasIssues)); }
    }

    public bool HasErrors => _errorCount > 0;
    public bool HasWarnings => _warningCount > 0;
    public bool HasIssues => _errorCount > 0 || _warningCount > 0;
}

// ─── Individual setting item ──────────────────────────────────────────────────

public sealed class DtdSettingItemViewModel : BaseViewModel
{
    private string _value;
    private string _savedValue;
    private string? _validationError;
    private string? _validationWarning;

    public DtdSettingItemViewModel(ServerSettingDefinition def, string currentValue)
    {
        Key = def.SettingKey;
        DisplayName = def.DisplayName;
        Description = def.Description ?? string.Empty;
        HelpText = def.HelpText ?? string.Empty;
        Unit = def.Unit ?? string.Empty;
        RecommendedValue = def.RecommendedValue ?? string.Empty;
        Placeholder = def.Placeholder ?? string.Empty;
        Category = def.Category ?? string.Empty;
        DefaultValue = def.DefaultValue ?? string.Empty;
        IsRequired = def.IsRequired;
        IsAdvanced = def.IsAdvanced;
        RequiresRestart = def.RequiresRestart;

        IsText = def.ControlType == SettingControlType.TextBox;
        IsPassword = def.ControlType == SettingControlType.PasswordField;
        IsToggle = def.ControlType == SettingControlType.Toggle;
        IsDropdown = def.ControlType == SettingControlType.Dropdown;
        IsNumber = def.ControlType == SettingControlType.NumberBox;
        IsFolderPicker = def.ControlType is SettingControlType.FolderPicker or SettingControlType.FilePicker;
        IsSandboxCode = def.SettingKey.Equals("SandboxCode", StringComparison.OrdinalIgnoreCase);

        ParsedOptions = def.Options?.Select(o =>
        {
            var idx = o.IndexOf(':');
            return idx >= 0 ? new OptionItem(o[..idx], o[(idx + 1)..]) : new OptionItem(o, o);
        }).ToList() ?? [];

        MinValue = def.MinValue;
        MaxValue = def.MaxValue;

        _value = currentValue;
        _savedValue = currentValue;

        BrowseCommand = new RelayCommand(_ =>
        {
            var dialog = new OpenFolderDialog { Title = $"Select folder for {DisplayName}" };
            if (dialog.ShowDialog() == true)
                Value = dialog.FolderName;
        });

        ResetToDefaultCommand = new RelayCommand(_ => Value = DefaultValue,
            () => Value != DefaultValue);
    }

    public string Key { get; }
    public string DisplayName { get; }
    public string Description { get; }
    public string HelpText { get; }
    public string Unit { get; }
    public string RecommendedValue { get; }
    public string Placeholder { get; }
    public string Category { get; }
    public string DefaultValue { get; }
    public bool IsRequired { get; }
    public bool IsAdvanced { get; }
    public bool RequiresRestart { get; }
    public bool IsText { get; }
    public bool IsPassword { get; }
    public bool IsToggle { get; }
    public bool IsDropdown { get; }
    public bool IsNumber { get; }
    public bool IsFolderPicker { get; }
    public bool IsSandboxCode { get; }
    public IReadOnlyList<OptionItem> ParsedOptions { get; }
    public int? MinValue { get; }
    public int? MaxValue { get; }
    public RelayCommand BrowseCommand { get; }
    public RelayCommand ResetToDefaultCommand { get; }

    public bool HasDescription => !string.IsNullOrEmpty(Description);
    public bool HasHelpText => !string.IsNullOrEmpty(HelpText);
    public bool HasUnit => !string.IsNullOrEmpty(Unit);
    public bool HasRecommendedValue => !string.IsNullOrEmpty(RecommendedValue);

    public bool IsModified => !string.Equals(_value, _savedValue, StringComparison.Ordinal);

    public string Value
    {
        get => _value;
        set
        {
            if (_value == value) return;
            _value = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsModified));
            OnPropertyChanged(nameof(BooleanValue));
            ValueChanged?.Invoke(this, EventArgs.Empty);
            ResetToDefaultCommand.NotifyCanExecuteChanged();
        }
    }

    public bool BooleanValue
    {
        get => _value.Equals("True", StringComparison.OrdinalIgnoreCase);
        set => Value = value ? "True" : "False";
    }

    public string? ValidationError
    {
        get => _validationError;
        set { _validationError = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasError)); }
    }

    public string? ValidationWarning
    {
        get => _validationWarning;
        set { _validationWarning = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasWarning)); }
    }

    public bool HasError => !string.IsNullOrEmpty(_validationError);
    public bool HasWarning => !string.IsNullOrEmpty(_validationWarning);

    public event EventHandler? ValueChanged;

    public void CopySandboxCode()
    {
        if (!string.IsNullOrEmpty(_value))
            Clipboard.SetText(_value);
    }

    public void PasteSandboxCode()
    {
        var text = Clipboard.GetText();
        if (!string.IsNullOrEmpty(text))
            Value = text.Trim();
    }

    public void CommitSave()
    {
        _savedValue = _value;
        OnPropertyChanged(nameof(IsModified));
    }

    public void Revert() => Value = _savedValue;
}

// ─── Crossplay requirement item ───────────────────────────────────────────────

public sealed record CrossplayCheck(string Name, bool Passed, string? FixHint);

// ─── Main ViewModel ───────────────────────────────────────────────────────────

public sealed class SevenDaysToDieSettingsViewModel : BaseViewModel, IDisposable
{
    private readonly ServerProfile _profile;
    private readonly SevenDaysToDieConfigService _configService = new();
    private readonly SevenDaysToDieValidator _validator = new();
    private readonly ServersJsonService _serversJsonService = new(new AppDataPaths());
    private readonly IGameServerProvider _provider;

    private string _selectedCategory = string.Empty;
    private string _searchText = string.Empty;
    private bool _showAdvanced;
    private string _statusMessage = string.Empty;
    private bool _isStatusError;
    private bool _isBusy;
    private DateTime? _lastSavedAt;
    private System.Windows.Threading.DispatcherTimer? _validationTimer;
    private ConfigFileWatcher? _configWatcher;
    private bool _hasExternalChanges;

    public SevenDaysToDieSettingsViewModel(ServerProfile profile, IGameServerProvider provider)
    {
        _profile = profile;
        _provider = provider;

        ServerName = profile.ServerName;
        InstallPath = profile.InstallPath;
        ConfigFilePath = Path.Combine(InstallPath, "serverconfig.xml");
        ConfigFileExists = File.Exists(ConfigFilePath);

        // Build items from provider definitions
        var allItems = provider.SettingsDefinitions
            .Select(def => new DtdSettingItemViewModel(def,
                profile.Settings.TryGetValue(def.SettingKey, out var v) ? v : def.DefaultValue ?? string.Empty))
            .ToList();

        AllItems = new ObservableCollection<DtdSettingItemViewModel>(allItems);
        FilteredItems = new ObservableCollection<DtdSettingItemViewModel>();

        // Subscribe to value changes for live validation
        foreach (var item in AllItems)
            item.ValueChanged += OnAnyValueChanged;

        // Detect installed saves
        DetectedSaves = DetectSaves();

        // Build categories
        var categoryNames = provider.SettingsDefinitions
            .Select(d => d.Category ?? string.Empty)
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Distinct()
            .ToList();

        Categories = new ObservableCollection<DtdCategoryViewModel>(
            categoryNames.Select(n => new DtdCategoryViewModel { Name = n }));

        SelectedCategory = Categories.FirstOrDefault()?.Name ?? string.Empty;

        SaveCommand = new RelayCommand(_ => _ = SaveAsync(), () => !_isBusy);
        DiscardCommand = new RelayCommand(_ => Discard(), () => HasUnsavedChanges && !_isBusy);
        ReloadFromFileCommand = new RelayCommand(_ => _ = ReloadFromXmlAsync(), () => !_isBusy && ConfigFileExists);
        OpenConfigFolderCommand = new RelayCommand(_ =>
        {
            if (Directory.Exists(InstallPath))
                System.Diagnostics.Process.Start("explorer.exe", InstallPath);
        });
        ApplyCrossplaySettingsCommand = new RelayCommand(_ => ApplyCrossplaySettings());
        CopyConfigPathCommand = new RelayCommand(_ => Clipboard.SetText(ConfigFilePath));
        DismissExternalChangesCommand = new RelayCommand(_ => HasExternalChanges = false);

        RunValidation();

        // Watch serverconfig.xml for edits made outside the app (drift detection).
        _configWatcher = new ConfigFileWatcher(ConfigFilePath);
        _configWatcher.DriftDetected += OnConfigDrift;
        _configWatcher.Start();
    }

    // ── Read-only server metadata ─────────────────────────────────────────────

    public string ServerName { get; }
    public string InstallPath { get; }
    public string ConfigFilePath { get; }
    public bool ConfigFileExists { get; }
    public IReadOnlyList<string> DetectedSaves { get; }

    // ── Collections ───────────────────────────────────────────────────────────

    public ObservableCollection<DtdSettingItemViewModel> AllItems { get; }
    public ObservableCollection<DtdSettingItemViewModel> FilteredItems { get; }
    public ObservableCollection<DtdCategoryViewModel> Categories { get; }

    // ── Navigation ────────────────────────────────────────────────────────────

    public string SelectedCategory
    {
        get => _selectedCategory;
        set
        {
            _selectedCategory = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(ShowSandboxPanel));
            OnPropertyChanged(nameof(ShowDetectedSavesPanel));
            ApplyFilter();
        }
    }

    public string SearchText
    {
        get => _searchText;
        set { _searchText = value; OnPropertyChanged(); ApplyFilter(); }
    }

    public bool ShowAdvanced
    {
        get => _showAdvanced;
        set { _showAdvanced = value; OnPropertyChanged(); ApplyFilter(); }
    }

    // ── Status / feedback ─────────────────────────────────────────────────────

    public string StatusMessage
    {
        get => _statusMessage;
        private set { _statusMessage = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasStatusMessage)); }
    }

    public bool IsStatusError
    {
        get => _isStatusError;
        private set { _isStatusError = value; OnPropertyChanged(); }
    }

    public bool HasStatusMessage => !string.IsNullOrEmpty(_statusMessage);

    public bool IsBusy
    {
        get => _isBusy;
        private set { _isBusy = value; OnPropertyChanged(); }
    }

    public DateTime? LastSavedAt
    {
        get => _lastSavedAt;
        private set { _lastSavedAt = value; OnPropertyChanged(); OnPropertyChanged(nameof(LastSavedText)); }
    }

    public string LastSavedText => _lastSavedAt.HasValue
        ? $"Last saved {_lastSavedAt.Value:HH:mm:ss}"
        : "Not saved yet";

    // ── Change tracking ───────────────────────────────────────────────────────

    public bool HasUnsavedChanges => AllItems.Any(i => i.IsModified);

    public int UnsavedChangeCount => AllItems.Count(i => i.IsModified);

    // ── Validation ────────────────────────────────────────────────────────────

    private SevenDaysToDieValidationResult _validationResult = new();

    public bool HasErrors => _validationResult.Errors.Count > 0;
    public bool HasWarnings => _validationResult.Warnings.Count > 0;

    public string ErrorSummary => string.Join("\n", _validationResult.Errors);
    public string WarningSummary => string.Join("\n", _validationResult.Warnings);

    // ── Crossplay panel ───────────────────────────────────────────────────────

    public bool CrossplayEnabled => GetBool("ServerAllowCrossplay");
    public bool ShowCrossplayPanel => CrossplayEnabled;

    public bool CrossplayMaxPlayersOk =>
        int.TryParse(GetValue("ServerMaxPlayerCount"), out var n) && n <= 8;

    public bool CrossplayEacOk => GetBool("EACEnabled");

    public bool CrossplaySanctionsOk => !GetBool("IgnoreEOSSanctions");

    public bool CrossplayNetworkOk =>
        !GetValue("ServerDisabledNetworkProtocols").Contains("AllNetworkingProtocols", StringComparison.OrdinalIgnoreCase);

    public bool AllCrossplayChecksPass =>
        CrossplayMaxPlayersOk && CrossplayEacOk && CrossplaySanctionsOk && CrossplayNetworkOk;

    public IReadOnlyList<CrossplayCheck> CrossplayChecks =>
    [
        new CrossplayCheck("Max Players ≤ 8", CrossplayMaxPlayersOk,
            CrossplayMaxPlayersOk ? null : "Set Max Players to 8 or fewer"),
        new CrossplayCheck("Easy Anti-Cheat enabled", CrossplayEacOk,
            CrossplayEacOk ? null : "Enable EAC in Security settings"),
        new CrossplayCheck("EOS Sanctions active", CrossplaySanctionsOk,
            CrossplaySanctionsOk ? null : "Disable 'Ignore EOS Sanctions'"),
        new CrossplayCheck("Network protocols valid", CrossplayNetworkOk,
            CrossplayNetworkOk ? null : "Remove 'AllNetworkingProtocols' from disabled list"),
    ];

    // ── SandboxCode ───────────────────────────────────────────────────────────

    public bool ShowSandboxPanel => SelectedCategory == "Sandbox (V3)";

    public bool ShowDetectedSavesPanel =>
        SelectedCategory == "World & Map" && DetectedSaves.Count > 0;

    public DtdSettingItemViewModel? SandboxCodeItem =>
        AllItems.FirstOrDefault(i => i.IsSandboxCode);

    public bool HasSandboxCode =>
        !string.IsNullOrWhiteSpace(SandboxCodeItem?.Value);

    // ── Commands ──────────────────────────────────────────────────────────────

    public RelayCommand SaveCommand { get; }
    public RelayCommand DiscardCommand { get; }
    public RelayCommand ReloadFromFileCommand { get; }
    public RelayCommand OpenConfigFolderCommand { get; }
    public RelayCommand ApplyCrossplaySettingsCommand { get; }
    public RelayCommand CopyConfigPathCommand { get; }
    public RelayCommand DismissExternalChangesCommand { get; }

    // ── Config drift (file changed outside the app) ───────────────────────────

    /// <summary>
    /// True when serverconfig.xml was modified on disk by something other than
    /// this app while the page was open. Surfaces a banner offering Reload /
    /// Keep my changes — we never silently overwrite external edits.
    /// </summary>
    public bool HasExternalChanges
    {
        get => _hasExternalChanges;
        private set { _hasExternalChanges = value; OnPropertyChanged(); }
    }

    private void OnConfigDrift(object? sender, EventArgs e)
    {
        HasExternalChanges = true;
        SetStatus("serverconfig.xml was changed outside the app — reload or keep your changes.", error: false);
    }

    public void Dispose()
    {
        if (_configWatcher != null)
        {
            _configWatcher.DriftDetected -= OnConfigDrift;
            _configWatcher.Dispose();
            _configWatcher = null;
        }

        if (_validationTimer != null)
        {
            _validationTimer.Stop();
            _validationTimer = null;
        }
    }

    // ── Save ──────────────────────────────────────────────────────────────────

    private async Task SaveAsync()
    {
        IsBusy = true;
        SetStatus(string.Empty, error: false);

        try
        {
            // Apply current item values to profile.Settings
            ApplyItemsToProfile();

            // Write to serverconfig.xml (atomic + backup)
            await SevenDaysToDieConfigService.EnsureConfigExistsAsync(ConfigFilePath, _profile.ServerName);
            await _configService.SaveAsync(_profile, ConfigFilePath, createBackup: true);

            // Also persist to servers.json (app's own database)
            await _serversJsonService.UpdateServerAsync(_profile);

            // Re-baseline so this write isn't reported back to us as external drift.
            _configWatcher?.MarkSynced();
            HasExternalChanges = false;

            LastSavedAt = DateTime.Now;
            SetStatus($"Saved — {AllItems.Count} settings written.", error: false);

            // Mark all items as committed
            foreach (var item in AllItems)
                item.CommitSave();

            OnPropertyChanged(nameof(HasUnsavedChanges));
            OnPropertyChanged(nameof(UnsavedChangeCount));
            SaveCommand.NotifyCanExecuteChanged();
            DiscardCommand.NotifyCanExecuteChanged();
        }
        catch (Exception ex)
        {
            SetStatus($"Save failed: {ex.Message}", error: true);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void Discard()
    {
        foreach (var item in AllItems)
            item.Revert();

        SetStatus("Changes discarded.", error: false);
        OnPropertyChanged(nameof(HasUnsavedChanges));
        OnPropertyChanged(nameof(UnsavedChangeCount));
    }

    // ── Reload from XML ───────────────────────────────────────────────────────

    private async Task ReloadFromXmlAsync()
    {
        IsBusy = true;
        try
        {
            await _configService.LoadAsync(_profile, ConfigFilePath);
            RefreshItemsFromProfile();
            _configWatcher?.MarkSynced();
            HasExternalChanges = false;
            SetStatus("Reloaded from serverconfig.xml.", error: false);
            RunValidation();
        }
        catch (Exception ex)
        {
            SetStatus($"Reload failed: {ex.Message}", error: true);
        }
        finally
        {
            IsBusy = false;
        }
    }

    // ── Crossplay auto-fix ────────────────────────────────────────────────────

    private void ApplyCrossplaySettings()
    {
        SetItemValue("ServerMaxPlayerCount", "8");
        SetItemValue("EACEnabled", "True");
        SetItemValue("IgnoreEOSSanctions", "False");

        SetStatus("Applied recommended crossplay settings. Review and save.", error: false);
        RunValidation();
    }

    // ── Filter ────────────────────────────────────────────────────────────────

    private void ApplyFilter()
    {
        var items = AllItems.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(_selectedCategory))
            items = items.Where(i => i.Category == _selectedCategory);

        if (!_showAdvanced)
            items = items.Where(i => !i.IsAdvanced || i.IsModified);

        if (!string.IsNullOrWhiteSpace(_searchText))
        {
            var q = _searchText.Trim();
            items = items.Where(i =>
                i.DisplayName.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                i.Key.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                i.Description.Contains(q, StringComparison.OrdinalIgnoreCase));
        }

        FilteredItems.Clear();
        foreach (var item in items)
            FilteredItems.Add(item);
    }

    // ── Validation ────────────────────────────────────────────────────────────

    private void RunValidation()
    {
        ApplyItemsToProfile();
        _validationResult = _validator.Validate(_profile);

        // Clear per-item messages
        foreach (var item in AllItems)
        {
            item.ValidationError = null;
            item.ValidationWarning = null;
        }

        // Map crossplay errors to specific items
        if (_validationResult.Errors.Any(e => e.Contains("crossplay") || e.Contains("Crossplay")))
        {
            SetItemError("ServerAllowCrossplay", "Crossplay configuration has errors. See the Crossplay panel below.");
        }

        if (_validationResult.Warnings.Any(w => w.Contains("Crossplay")))
        {
            SetItemWarning("ServerMaxPlayerCount", "Must be 8 or fewer for crossplay.");
        }

        if (_validationResult.Warnings.Any(w => w.Contains("V2") || w.Contains("SandboxCode")))
        {
            SetItemWarning("SandboxCode", "V2 gameplay settings detected. Consider using a V3 Sandbox Code.");
        }

        if (_validationResult.Warnings.Any(w => w.Contains("WorldGenSize")))
        {
            SetItemWarning("WorldGenSize", "Non-standard world size. Supported: 6144, 8192, 10240.");
        }

        // Update category badge counts
        foreach (var cat in Categories)
        {
            var catItems = AllItems.Where(i => i.Category == cat.Name).ToList();
            cat.ErrorCount = catItems.Count(i => i.HasError);
            cat.WarningCount = catItems.Count(i => i.HasWarning);
        }

        OnPropertyChanged(nameof(HasErrors));
        OnPropertyChanged(nameof(HasWarnings));
        OnPropertyChanged(nameof(ErrorSummary));
        OnPropertyChanged(nameof(WarningSummary));
        OnPropertyChanged(nameof(CrossplayEnabled));
        OnPropertyChanged(nameof(ShowCrossplayPanel));
        OnPropertyChanged(nameof(CrossplayMaxPlayersOk));
        OnPropertyChanged(nameof(CrossplayEacOk));
        OnPropertyChanged(nameof(CrossplaySanctionsOk));
        OnPropertyChanged(nameof(CrossplayNetworkOk));
        OnPropertyChanged(nameof(AllCrossplayChecksPass));
        OnPropertyChanged(nameof(CrossplayChecks));
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private void OnAnyValueChanged(object? sender, EventArgs e)
    {
        // Debounce: run validation 400 ms after the last change, not on every keystroke
        if (_validationTimer == null)
        {
            _validationTimer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(400)
            };
            _validationTimer.Tick += (_, _) => { _validationTimer.Stop(); RunValidation(); };
        }
        _validationTimer.Stop();
        _validationTimer.Start();

        OnPropertyChanged(nameof(HasUnsavedChanges));
        OnPropertyChanged(nameof(UnsavedChangeCount));
        SaveCommand.NotifyCanExecuteChanged();
        DiscardCommand.NotifyCanExecuteChanged();
    }

    private void ApplyItemsToProfile()
    {
        foreach (var item in AllItems)
        {
            _profile.Settings[item.Key] = item.Value;
        }

        // Keep profile-level fields in sync
        if (_profile.Settings.TryGetValue("ServerMaxPlayerCount", out var mp) &&
            int.TryParse(mp, out var maxPlayers))
        {
            _profile.MaxPlayers = maxPlayers;
        }

        if (_profile.Settings.TryGetValue("ServerPassword", out var pw))
        {
            _profile.Password = pw;
        }
    }

    private void RefreshItemsFromProfile()
    {
        foreach (var item in AllItems)
        {
            if (_profile.Settings.TryGetValue(item.Key, out var v))
            {
                // Temporarily disconnect the event to avoid re-triggering validation during bulk refresh
                item.ValueChanged -= OnAnyValueChanged;
                item.Value = v;
                item.ValueChanged += OnAnyValueChanged;
            }
        }

        OnPropertyChanged(nameof(HasUnsavedChanges));
        OnPropertyChanged(nameof(UnsavedChangeCount));
        ApplyFilter();
    }

    private void SetItemValue(string key, string value)
    {
        var item = AllItems.FirstOrDefault(i => i.Key.Equals(key, StringComparison.OrdinalIgnoreCase));
        if (item != null)
            item.Value = value;
    }

    private void SetItemError(string key, string message)
    {
        var item = AllItems.FirstOrDefault(i => i.Key.Equals(key, StringComparison.OrdinalIgnoreCase));
        if (item != null)
            item.ValidationError = message;
    }

    private void SetItemWarning(string key, string message)
    {
        var item = AllItems.FirstOrDefault(i => i.Key.Equals(key, StringComparison.OrdinalIgnoreCase));
        if (item != null)
            item.ValidationWarning = message;
    }

    private string GetValue(string key)
    {
        var item = AllItems.FirstOrDefault(i => i.Key.Equals(key, StringComparison.OrdinalIgnoreCase));
        return item?.Value ?? _profile.Settings.GetValueOrDefault(key, string.Empty);
    }

    private bool GetBool(string key)
        => GetValue(key).Equals("True", StringComparison.OrdinalIgnoreCase);

    private void SetStatus(string message, bool error)
    {
        StatusMessage = message;
        IsStatusError = error;
    }

    private IReadOnlyList<string> DetectSaves()
    {
        try
        {
            var userDataFolder = _profile.Settings.TryGetValue("UserDataFolder", out var udf) && !string.IsNullOrWhiteSpace(udf)
                ? udf : Path.Combine(InstallPath, "UserData");
            var savesPath = Path.Combine(userDataFolder, "Saves");
            if (!Directory.Exists(savesPath)) return [];
            return Directory.GetDirectories(savesPath, "*", SearchOption.TopDirectoryOnly)
                .Select(Path.GetFileName)
                .Where(n => n != null)
                .Cast<string>()
                .OrderBy(n => n)
                .ToList();
        }
        catch
        {
            return [];
        }
    }
}
