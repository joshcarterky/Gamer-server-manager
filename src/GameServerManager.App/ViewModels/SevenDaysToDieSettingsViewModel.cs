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

    /// <summary>Property found in serverconfig.xml but not in the current schema —
    /// preserved as-is and edited through a generic text control.</summary>
    public bool IsUnrecognized { get; init; }
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

// ─── Sandbox decoded-option row ───────────────────────────────────────────────

public sealed record SandboxOptionRow(string Key, string Display, string Category, string Value);

// ─── Main ViewModel ───────────────────────────────────────────────────────────

public sealed class SevenDaysToDieSettingsViewModel : BaseViewModel, IDisposable
{
    private readonly ServerProfile _profile;
    private readonly SevenDaysToDieConfigService _configService = new();
    private readonly SevenDaysToDieValidator _validator = new();
    private readonly ServersJsonService _serversJsonService;
    private readonly IGameServerProvider _provider;

    private readonly SevenDaysToDieVersionService _versionService = new();
    private readonly SevenDaysToDieConfigDriftService _driftService = new();
    private readonly SevenDaysToDieMigrationService _migrationService = new();
    private readonly SandboxCodecRegistry _codecRegistry = SandboxCodecRegistry.CreateDefault();
    private readonly SandboxPresetStore _presetStore;

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

    // Content hash of serverconfig.xml as of the last load/save by this app.
    // Save refuses to overwrite when the on-disk file no longer matches.
    private string _lastSyncedHash = string.Empty;

    private SevenDaysToDieInstallInfo? _installInfo;
    private MigrationResult? _lastMigrationResult;
    private SandboxSettings? _lastGsoImport;
    private SandboxSettings? _gsoBaseline;

    public SevenDaysToDieSettingsViewModel(ServerProfile profile, IGameServerProvider provider)
    {
        _profile = profile;
        _provider = provider;

        var paths = new AppDataPaths();
        _serversJsonService = new ServersJsonService(paths);
        _presetStore = new SandboxPresetStore(paths.SettingsDirectory);

        ServerName = profile.ServerName;
        InstallPath = profile.InstallPath;
        ConfigFilePath = Path.Combine(InstallPath, "serverconfig.xml");
        ConfigFileExists = File.Exists(ConfigFilePath);

        // Build items from provider definitions. Values pass through
        // NormalizeSettingValue so shapes an older app version stored wrong
        // (e.g. HideCommandExecutionLog as True/False) heal on load.
        var allItems = provider.SettingsDefinitions
            .Select(def => new DtdSettingItemViewModel(def,
                SevenDaysToDieConfigService.NormalizeSettingValue(def.SettingKey,
                    profile.Settings.TryGetValue(def.SettingKey, out var v) ? v : def.DefaultValue ?? string.Empty)))
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
        DismissExternalChangesCommand = new RelayCommand(_ =>
        {
            // Explicit "Keep My Changes": re-baseline so the next save is allowed
            // to overwrite the external edit the user just chose to discard.
            _lastSyncedHash = ConfigFileSnapshot.Compute(ConfigFilePath);
            HasExternalChanges = false;
        });

        CopySandboxCodeCommand = new RelayCommand(_ =>
        {
            SandboxCodeItem?.CopySandboxCode();
            SetStatus("Sandbox code copied to clipboard.", error: false);
        }, () => HasSandboxCode);
        PasteSandboxCodeCommand = new RelayCommand(_ =>
        {
            SandboxCodeItem?.PasteSandboxCode();
            SetStatus("Sandbox code pasted — review and save.", error: false);
        });
        ImportGsoCommand = new RelayCommand(_ => ImportGso(), () => !string.IsNullOrWhiteSpace(GsoImportText));
        SetGsoBaselineCommand = new RelayCommand(_ => SetGsoBaseline(), () => _lastGsoImport != null);
        ApplyPresetCommand = new RelayCommand(p => ApplyPreset(p as SandboxPreset ?? SelectedPreset));
        DeletePresetCommand = new RelayCommand(p => _ = DeletePresetAsync(p as SandboxPreset));
        SaveCurrentAsPresetCommand = new RelayCommand(_ => _ = SaveCurrentAsPresetAsync(),
            () => HasSandboxCode && !string.IsNullOrWhiteSpace(NewPresetName));
        RunMigrationCommand = new RelayCommand(_ => _ = RunMigrationAsync(), () => !_isBusy && MigrationPlan.Count > 0);
        RollbackMigrationCommand = new RelayCommand(_ => _ = RollbackMigrationAsync(),
            () => !_isBusy && _lastMigrationResult != null);

        _lastSyncedHash = ConfigFileSnapshot.Compute(ConfigFilePath);

        RunValidation();

        // Watch serverconfig.xml for edits made outside the app (drift detection).
        _configWatcher = new ConfigFileWatcher(ConfigFilePath);
        _configWatcher.DriftDetected += OnConfigDrift;
        _configWatcher.Start();

        // Installed-files-first insights: version/branch detection, unrecognized
        // property surfacing, migration plan, presets. Async — UI fills in as it lands.
        _ = InitializeInsightsAsync();
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
        private set
        {
            _isBusy = value;
            OnPropertyChanged();
            // Every busy-gated command must re-query, or the Save button stays
            // greyed out after the first save (the last notification fired while
            // the ViewModel was still busy).
            SaveCommand?.NotifyCanExecuteChanged();
            DiscardCommand?.NotifyCanExecuteChanged();
            ReloadFromFileCommand?.NotifyCanExecuteChanged();
            RunMigrationCommand?.NotifyCanExecuteChanged();
            RollbackMigrationCommand?.NotifyCanExecuteChanged();
        }
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
    public RelayCommand CopySandboxCodeCommand { get; }
    public RelayCommand PasteSandboxCodeCommand { get; }
    public RelayCommand ImportGsoCommand { get; }
    public RelayCommand SetGsoBaselineCommand { get; }
    public RelayCommand ApplyPresetCommand { get; }
    public RelayCommand DeletePresetCommand { get; }
    public RelayCommand SaveCurrentAsPresetCommand { get; }
    public RelayCommand RunMigrationCommand { get; }
    public RelayCommand RollbackMigrationCommand { get; }

    // ── Installed version info ────────────────────────────────────────────────

    public string VersionSummary => _installInfo?.Summary ?? string.Empty;
    public bool HasVersionInfo => _installInfo != null &&
        (_installInfo.GameVersion != null || _installInfo.BuildId != null);

    // ── V2 → V3 migration assistant ───────────────────────────────────────────

    public ObservableCollection<MigrationPlanEntry> MigrationPlan { get; } = new();
    public bool ShowMigrationPanel => MigrationPlan.Count > 0;
    public bool CanRollbackMigration => _lastMigrationResult != null;

    // ── Sandbox: decoded view, gso import, presets ────────────────────────────

    public ObservableCollection<SandboxOptionRow> DecodedOptions { get; } = new();
    public ObservableCollection<SandboxDiff> GsoDiffs { get; } = new();
    public ObservableCollection<SandboxPreset> Presets { get; } = new();

    public bool HasDecodedOptions => DecodedOptions.Count > 0;
    public bool HasGsoDiffs => GsoDiffs.Count > 0;

    private string _sandboxDecodeStatus = string.Empty;
    public string SandboxDecodeStatus
    {
        get => _sandboxDecodeStatus;
        private set { _sandboxDecodeStatus = value; OnPropertyChanged(); }
    }

    private string _gsoImportText = string.Empty;
    public string GsoImportText
    {
        get => _gsoImportText;
        set { _gsoImportText = value; OnPropertyChanged(); ImportGsoCommand.NotifyCanExecuteChanged(); }
    }

    private string _newPresetName = string.Empty;
    public string NewPresetName
    {
        get => _newPresetName;
        set { _newPresetName = value; OnPropertyChanged(); SaveCurrentAsPresetCommand.NotifyCanExecuteChanged(); }
    }

    private SandboxPreset? _selectedPreset;
    public SandboxPreset? SelectedPreset
    {
        get => _selectedPreset;
        set { _selectedPreset = value; OnPropertyChanged(); }
    }

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

    // ── Insights: installed version, unrecognized keys, migration, presets ────

    private async Task InitializeInsightsAsync()
    {
        try
        {
            _installInfo = await _versionService.DetectAsync(InstallPath);
            OnPropertyChanged(nameof(VersionSummary));
            OnPropertyChanged(nameof(HasVersionInfo));

            await RefreshDriftAndMigrationAsync();
            await RefreshPresetsAsync();
            UpdateSandboxDecodeState();
        }
        catch (Exception ex)
        {
            // Insights are additive — never block the settings page on them.
            SetStatus($"Server inspection incomplete: {ex.Message}", error: false);
        }
    }

    private async Task RefreshDriftAndMigrationAsync()
    {
        var report = await _driftService.AnalyzeAsync(
            ConfigFilePath, _provider.SettingsDefinitions.Select(d => d.SettingKey));

        // Surface properties present in the file but unknown to the schema as
        // editable "Unrecognized" items instead of hiding them.
        var known = new HashSet<string>(AllItems.Select(i => i.Key), StringComparer.OrdinalIgnoreCase);
        foreach (var prop in report.UnknownProperties.Where(p => !known.Contains(p.Name)))
        {
            var def = new ServerSettingDefinition
            {
                SettingKey = prop.Name,
                DisplayName = prop.Name,
                DefaultValue = prop.Value,
                ControlType = SettingControlType.TextBox,
                Category = "Unrecognized",
                Description = "Not recognized by the current schema — possibly added by a newer " +
                              "game version or a mod. Preserved in serverconfig.xml exactly as-is.",
                RequiresRestart = true
            };
            var item = new DtdSettingItemViewModel(def, prop.Value) { IsUnrecognized = true };
            item.ValueChanged += OnAnyValueChanged;
            AllItems.Add(item);
            _profile.Settings.TryAdd(prop.Name, prop.Value);
        }

        if (AllItems.Any(i => i.IsUnrecognized) &&
            Categories.All(c => c.Name != "Unrecognized"))
        {
            Categories.Add(new DtdCategoryViewModel { Name = "Unrecognized" });
        }

        // Legacy V2 gameplay properties → migration plan.
        var plan = await _migrationService.BuildPlanAsync(ConfigFilePath);
        MigrationPlan.Clear();
        foreach (var entry in plan)
            MigrationPlan.Add(entry);

        OnPropertyChanged(nameof(ShowMigrationPanel));
        RunMigrationCommand.NotifyCanExecuteChanged();
        ApplyFilter();
    }

    private async Task RunMigrationAsync()
    {
        IsBusy = true;
        try
        {
            var result = await _migrationService.ApplyAsync(ConfigFilePath, MigrationPlan.ToList());
            _lastMigrationResult = result;

            // Keep the removed keys out of the profile too, or the next save
            // would write them straight back into the cleaned file.
            foreach (var entry in MigrationPlan)
                _profile.Settings.Remove(entry.LegacyKey);
            await _serversJsonService.UpdateServerAsync(_profile);

            _configWatcher?.MarkSynced();
            _lastSyncedHash = ConfigFileSnapshot.Compute(ConfigFilePath);
            MigrationPlan.Clear();
            OnPropertyChanged(nameof(ShowMigrationPanel));
            OnPropertyChanged(nameof(CanRollbackMigration));
            RollbackMigrationCommand.NotifyCanExecuteChanged();

            SetStatus($"Migration complete — {result.RemovedKeyCount} legacy properties removed. " +
                      $"Backup: {Path.GetFileName(result.BackupPath)} · Report: {Path.GetFileName(result.ReportPath)}",
                error: false);
        }
        catch (Exception ex)
        {
            SetStatus($"Migration failed: {ex.Message}", error: true);
        }
        finally
        {
            IsBusy = false;
            RunMigrationCommand.NotifyCanExecuteChanged();
        }
    }

    private async Task RollbackMigrationAsync()
    {
        if (_lastMigrationResult == null) return;
        IsBusy = true;
        try
        {
            _migrationService.Rollback(_lastMigrationResult, ConfigFilePath);
            _lastMigrationResult = null;
            await _configService.LoadAsync(_profile, ConfigFilePath);
            RefreshItemsFromProfile();
            _configWatcher?.MarkSynced();
            _lastSyncedHash = ConfigFileSnapshot.Compute(ConfigFilePath);
            await RefreshDriftAndMigrationAsync();
            OnPropertyChanged(nameof(CanRollbackMigration));
            SetStatus("Migration rolled back — the original configuration was restored.", error: false);
        }
        catch (Exception ex)
        {
            SetStatus($"Rollback failed: {ex.Message}", error: true);
        }
        finally
        {
            IsBusy = false;
            RollbackMigrationCommand.NotifyCanExecuteChanged();
        }
    }

    // ── Sandbox helpers ───────────────────────────────────────────────────────

    private void UpdateSandboxDecodeState()
    {
        var code = SandboxCodeItem?.Value?.Trim() ?? string.Empty;
        CopySandboxCodeCommand.NotifyCanExecuteChanged();

        if (code.Length == 0)
        {
            SandboxDecodeStatus = "No sandbox code set — the server will use default V3 gameplay settings.";
            return;
        }

        var codec = _codecRegistry.ResolveForCode(code);
        if (codec != null && codec.TryDecode(code, out var decoded, out _))
        {
            ShowDecodedOptions(decoded, $"Decoded by {codec.CodecId} (verified against {codec.VerifiedAgainst}).");
            return;
        }

        // No verified decoder: rule 1 — never guess, never touch the stored code.
        SandboxDecodeStatus = "No verified decoder is available for this game build — the code is stored " +
                              "and applied exactly as entered. Run 'getsandboxoptions' (gso) in the server " +
                              "console and paste the output below to view the decoded settings.";
    }

    private void ImportGso()
    {
        if (!GetSandboxOptionsParser.TryParse(GsoImportText, out var settings, out var error))
        {
            SetStatus($"gso import failed: {error}", error: true);
            return;
        }

        settings.GameVersion = _installInfo?.GameVersion;
        _lastGsoImport = settings;
        SetGsoBaselineCommand.NotifyCanExecuteChanged();
        ShowDecodedOptions(settings, $"{settings.Count} options imported from gso output.");

        GsoDiffs.Clear();
        if (_gsoBaseline != null)
        {
            foreach (var diff in SandboxComparer.Compare(_gsoBaseline, settings))
                GsoDiffs.Add(diff);
        }
        OnPropertyChanged(nameof(HasGsoDiffs));
        SetStatus($"Imported {settings.Count} sandbox options from console output.", error: false);
    }

    private void SetGsoBaseline()
    {
        if (_lastGsoImport == null) return;
        _gsoBaseline = _lastGsoImport.Clone();
        GsoDiffs.Clear();
        OnPropertyChanged(nameof(HasGsoDiffs));
        SetStatus("Baseline captured — the next gso import will show what changed.", error: false);
    }

    private void ShowDecodedOptions(SandboxSettings settings, string statusText)
    {
        var schema = SandboxSchema.BuiltIn;
        DecodedOptions.Clear();
        foreach (var (key, value) in settings.Values)
        {
            var def = schema.Find(key);
            DecodedOptions.Add(new SandboxOptionRow(
                key,
                def?.DisplayName ?? key,
                def?.Category ?? "Other",
                value));
        }

        OnPropertyChanged(nameof(HasDecodedOptions));
        SandboxDecodeStatus = statusText;
    }

    // ── Presets ───────────────────────────────────────────────────────────────

    private async Task RefreshPresetsAsync()
    {
        var presets = await _presetStore.LoadAsync();
        Presets.Clear();
        foreach (var preset in presets)
            Presets.Add(preset);
    }

    private void ApplyPreset(SandboxPreset? preset)
    {
        if (preset == null) return;
        if (!preset.HasCode)
        {
            SetStatus($"'{preset.Name}' has no captured code yet — generate it in the game menu " +
                      "on your installed build, then save it here as a preset.", error: true);
            return;
        }

        if (SandboxCodeItem != null)
            SandboxCodeItem.Value = preset.Code;

        var buildNote = string.IsNullOrEmpty(preset.CapturedOnBuild)
            ? string.Empty
            : $" (captured on {preset.CapturedOnBuild})";
        SetStatus($"Preset '{preset.Name}' applied{buildNote} — review and save.", error: false);
    }

    private async Task SaveCurrentAsPresetAsync()
    {
        var code = SandboxCodeItem?.Value?.Trim();
        if (string.IsNullOrEmpty(code)) return;

        try
        {
            var build = _installInfo?.GameVersion != null
                ? $"{_installInfo.GameVersion}" + (_installInfo.GameBuild != null ? $" (b{_installInfo.GameBuild})" : "")
                : null;
            await _presetStore.SaveAsync(new SandboxPreset
            {
                Name = NewPresetName.Trim(),
                Description = "Captured from this server's configuration.",
                Code = code,
                CapturedOnBuild = build
            });
            NewPresetName = string.Empty;
            await RefreshPresetsAsync();
            SetStatus("Preset saved.", error: false);
        }
        catch (Exception ex)
        {
            SetStatus($"Could not save preset: {ex.Message}", error: true);
        }
    }

    private async Task DeletePresetAsync(SandboxPreset? preset)
    {
        preset ??= SelectedPreset;
        if (preset == null || (preset.IsOfficial && !preset.HasCode)) return;

        try
        {
            await _presetStore.DeleteAsync(preset.Name);
            await RefreshPresetsAsync();
            SetStatus($"Preset '{preset.Name}' deleted.", error: false);
        }
        catch (Exception ex)
        {
            SetStatus($"Could not delete preset: {ex.Message}", error: true);
        }
    }

    // ── Save ──────────────────────────────────────────────────────────────────

    private async Task SaveAsync()
    {
        IsBusy = true;
        SetStatus(string.Empty, error: false);

        try
        {
            // Concurrent-modification guard: never overwrite edits made outside
            // the app since our last load/save. The banner offers Reload / Keep.
            if (File.Exists(ConfigFilePath) &&
                ConfigFileSnapshot.HasDrifted(_lastSyncedHash, ConfigFilePath))
            {
                HasExternalChanges = true;
                SetStatus("Save blocked: serverconfig.xml was changed outside the app. " +
                          "Reload from file to pick up those edits, or choose 'Keep My Changes' to overwrite them.",
                    error: true);
                return;
            }

            // Apply current item values to profile.Settings
            ApplyItemsToProfile();

            // Write to serverconfig.xml (atomic + backup)
            await SevenDaysToDieConfigService.EnsureConfigExistsAsync(ConfigFilePath, _profile.ServerName);
            await _configService.SaveAsync(_profile, ConfigFilePath, createBackup: true);

            // Also persist to servers.json (app's own database)
            await _serversJsonService.UpdateServerAsync(_profile);

            // Re-baseline so this write isn't reported back to us as external drift.
            _configWatcher?.MarkSynced();
            _lastSyncedHash = ConfigFileSnapshot.Compute(ConfigFilePath);
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
            _lastSyncedHash = ConfigFileSnapshot.Compute(ConfigFilePath);
            HasExternalChanges = false;
            await RefreshDriftAndMigrationAsync();
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

        UpdateSandboxDecodeState();
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
