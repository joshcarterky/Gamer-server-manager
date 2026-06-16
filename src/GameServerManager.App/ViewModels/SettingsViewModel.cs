using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Windows;
using GameServerManager.Core.Models;
using GameServerManager.Services;
using GameServerManager.Services.CurseForge;
using GameServerManager.App.Views;

namespace GameServerManager.App.ViewModels;

public class SettingsCategoryViewModel : BaseViewModel
{
    private bool _isSelected;

    public SettingsCategoryViewModel(string key, string name)
    {
        Key = key;
        Name = name;
    }

    public string Key { get; }
    public string Name { get; }

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }
}

public class SettingsViewModel : BaseViewModel
{
    private readonly AppSettingsService _settingsService;
    private CancellationTokenSource? _autoSaveCancellation;
    private AppSettings _settings = new();
    private SettingsCategoryViewModel? _selectedCategory;
    private string _statusMessage = "Settings loaded.";
    private string _searchText = string.Empty;
    private string _integrationsStatus = string.Empty;

    public SettingsViewModel()
    {
        _settingsService = new AppSettingsService();
        Categories = new ObservableCollection<SettingsCategoryViewModel>
        {
            new("General", "General"),
            new("Appearance", "Appearance"),
            new("Application", "Application"),
            new("ServerDefaults", "Server Defaults"),
            new("Network", "Network"),
            new("Security", "Security"),
            new("Users", "Users and Permissions"),
            new("Notifications", "Notifications"),
            new("Backups", "Backups"),
            new("Monitoring", "Monitoring"),
            new("Updates", "Updates"),
            new("Integrations", "Integrations"),
            new("Storage", "Storage"),
            new("Advanced", "Advanced")
        };

        SelectCategoryCommand = new RelayCommand(SelectCategory);
        SaveCommand = new RelayCommand(async _ => await SaveAsync());
        ResetCommand = new RelayCommand(async _ => await ResetAsync());
        ExportCommand = new RelayCommand(async _ => await ExportAsync());
        ImportCommand = new RelayCommand(async _ => await ImportAsync());
        TestCurseForgeCommand = new RelayCommand(async _ => await TestCurseForgeAsync());
        SelectCategory(Categories.First());
        _ = LoadAsync();
    }

    public ObservableCollection<SettingsCategoryViewModel> Categories { get; }
    public IReadOnlyList<string> LanguageOptions { get; } = new[]
    {
        "English (US)",
        "English (UK)",
        "Spanish",
        "French",
        "German"
    };

    public IReadOnlyList<string> TimeZoneOptions { get; } = new[]
    {
        "(UTC-08:00) Pacific Time (US & Canada)",
        "(UTC-07:00) Mountain Time (US & Canada)",
        "(UTC-06:00) Central Time (US & Canada)",
        "(UTC-05:00) Eastern Time (US & Canada)",
        "(UTC+00:00) UTC",
        "(UTC+01:00) Central European Time"
    };

    public IReadOnlyList<string> DateFormatOptions { get; } = new[]
    {
        "MMM dd, yyyy",
        "MM/dd/yyyy",
        "dd/MM/yyyy",
        "yyyy-MM-dd"
    };

    public IReadOnlyList<string> ThemeOptions { get; } = new[]
    {
        "Dark",
        "Darker",
        "Ocean",
        "Midnight",
        "Forest",
        "Sunset",
        "Cyber Blue",
        "Neon Purple",
        "High Contrast",
        "Slate"
    };

    public IReadOnlyList<string> AccentColorOptions { get; } = new[]
    {
        "Blue",
        "Cyan",
        "Purple",
        "Green",
        "Orange",
        "Red"
    };

    public IReadOnlyList<string> DensityOptions { get; } = new[]
    {
        "Compact",
        "Comfortable",
        "Spacious"
    };

    public IReadOnlyList<string> StartupBehaviorOptions { get; } = new[]
    {
        "Open Dashboard",
        "Open Servers",
        "Restore Last View",
        "Start Minimized"
    };

    public IReadOnlyList<string> LoggingLevelOptions { get; } = new[]
    {
        "Trace",
        "Debug",
        "Information",
        "Warning",
        "Error"
    };

    public IReadOnlyList<string> BackupScheduleOptions { get; } = new[]
    {
        "Disabled",
        "Hourly",
        "Daily at 02:00",
        "Every 6 hours",
        "Weekly"
    };

    public IReadOnlyList<string> RestartPolicyOptions { get; } = new[]
    {
        "Never",
        "Restart on Failure",
        "Restart Daily",
        "Restart After Update"
    };

    public IReadOnlyList<string> SslTlsOptions { get; } = new[]
    {
        "Disabled",
        "Manual Certificate",
        "Let's Encrypt (Auto)"
    };

    public IReadOnlyList<string> ApiAccessOptions { get; } = new[]
    {
        "Disabled",
        "Local Only",
        "Restricted",
        "Admin Only"
    };

    public IReadOnlyList<string> EncryptionOptions { get; } = new[]
    {
        "AES-256-GCM",
        "AES-128-GCM",
        "ChaCha20-Poly1305"
    };

    public RelayCommand SelectCategoryCommand { get; }
    public RelayCommand SaveCommand { get; }
    public RelayCommand ResetCommand { get; }
    public RelayCommand ExportCommand { get; }
    public RelayCommand ImportCommand { get; }
    public RelayCommand TestCurseForgeCommand { get; }

    public AppSettings Settings
    {
        get => _settings;
        set
        {
            _settings.PropertyChanged -= OnSettingsPropertyChanged;
            if (SetProperty(ref _settings, value))
            {
                _settings.PropertyChanged += OnSettingsPropertyChanged;
                NotifySummaryChanged();
                ApplyRuntimeSettings();
            }
        }
    }

    public SettingsCategoryViewModel? SelectedCategory
    {
        get => _selectedCategory;
        private set
        {
            if (SetProperty(ref _selectedCategory, value))
            {
                OnPropertyChanged(nameof(IsGeneralSelected));
                OnPropertyChanged(nameof(IsAppearanceSelected));
                OnPropertyChanged(nameof(IsApplicationSelected));
                OnPropertyChanged(nameof(IsServerDefaultsSelected));
                OnPropertyChanged(nameof(IsNetworkSelected));
                OnPropertyChanged(nameof(IsSecuritySelected));
                OnPropertyChanged(nameof(IsIntegrationsSelected));
                OnPropertyChanged(nameof(IsComingSoonSelected));
            }
        }
    }

    public string SearchText
    {
        get => _searchText;
        set => SetProperty(ref _searchText, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public bool IsGeneralSelected => SelectedCategory?.Key == "General";
    public bool IsAppearanceSelected => SelectedCategory?.Key == "Appearance";
    public bool IsApplicationSelected => SelectedCategory?.Key == "Application";
    public bool IsServerDefaultsSelected => SelectedCategory?.Key == "ServerDefaults";
    public bool IsNetworkSelected => SelectedCategory?.Key == "Network";
    public bool IsSecuritySelected => SelectedCategory?.Key == "Security";
    public bool IsIntegrationsSelected => SelectedCategory?.Key == "Integrations";
    public bool IsComingSoonSelected => SelectedCategory is not null
        && !IsGeneralSelected
        && !IsAppearanceSelected
        && !IsApplicationSelected
        && !IsServerDefaultsSelected
        && !IsNetworkSelected
        && !IsSecuritySelected
        && !IsIntegrationsSelected;

    public string IntegrationsStatus
    {
        get => _integrationsStatus;
        private set
        {
            if (SetProperty(ref _integrationsStatus, value))
                OnPropertyChanged(nameof(HasIntegrationsStatus));
        }
    }

    public bool HasIntegrationsStatus => !string.IsNullOrEmpty(_integrationsStatus);

    public int TotalCategories => Categories.Count;
    public int TotalSettings => 41;
    public string LastModifiedText => Settings.LastModifiedUtc.ToLocalTime().ToString("MMM dd, yyyy HH:mm:ss");
    public string VersionText => Settings.Version;

    private async Task LoadAsync()
    {
        try
        {
            Settings = await _settingsService.LoadAsync();
            Settings.PropertyChanged -= OnSettingsPropertyChanged;
            Settings.PropertyChanged += OnSettingsPropertyChanged;
            ApplyRuntimeSettings();
            StatusMessage = $"Loaded settings from {_settingsService.SettingsPath}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Could not load settings: {ex.Message}";
        }
    }

    private async Task SaveAsync()
    {
        try
        {
            await _settingsService.SaveAsync(Settings);
            NotifySummaryChanged();
            ApplyRuntimeSettings();
            StatusMessage = $"Saved settings to {_settingsService.SettingsPath}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Could not save settings: {ex.Message}";
        }
    }

    private async Task ResetAsync()
    {
        try
        {
            Settings = await _settingsService.ResetAsync();
            ApplyRuntimeSettings();
            StatusMessage = "Settings reset to defaults.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Could not reset settings: {ex.Message}";
        }
    }

    private async Task ExportAsync()
    {
        try
        {
            var exportPath = await _settingsService.ExportAsync(Settings);
            StatusMessage = $"Exported settings to {exportPath}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Could not export settings: {ex.Message}";
        }
    }

    private async Task ImportAsync()
    {
        try
        {
            Settings = await _settingsService.ImportAsync();
            ApplyRuntimeSettings();
            StatusMessage = $"Imported settings from {_settingsService.ImportPath}";
        }
        catch (FileNotFoundException)
        {
            StatusMessage = $"Place a JSON file at {_settingsService.ImportPath}, then click Import Configuration.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Could not import settings: {ex.Message}";
        }
    }

    private void SelectCategory(object? parameter)
    {
        if (parameter is not SettingsCategoryViewModel category)
        {
            return;
        }

        foreach (var item in Categories)
        {
            item.IsSelected = item == category;
        }

        SelectedCategory = category;
        StatusMessage = $"{category.Name} settings selected.";
    }

    private void NotifySummaryChanged()
    {
        OnPropertyChanged(nameof(LastModifiedText));
        OnPropertyChanged(nameof(VersionText));
    }

    private void OnSettingsPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(AppSettings.LastModifiedUtc))
        {
            return;
        }

        StatusMessage = Settings.AutoSaveSettings
            ? "Setting changed. Auto-save queued."
            : "Setting changed. Click Save Changes to keep it.";

        if (e.PropertyName is nameof(AppSettings.ApplicationName)
            or nameof(AppSettings.Theme)
            or nameof(AppSettings.AccentColor)
            or nameof(AppSettings.Density)
            or nameof(AppSettings.SidebarWidth)
            or nameof(AppSettings.CornerRadius)
            or nameof(AppSettings.BackgroundIntensity)
            or nameof(AppSettings.GlassPanels)
            or nameof(AppSettings.CompactHeader))
        {
            ApplyRuntimeSettings();
        }

        if (Settings.AutoSaveSettings)
        {
            QueueAutoSave();
        }
    }

    private void QueueAutoSave()
    {
        _autoSaveCancellation?.Cancel();
        var cancellation = new CancellationTokenSource();
        _autoSaveCancellation = cancellation;

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(500, cancellation.Token);
                await _settingsService.SaveAsync(Settings);

                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    NotifySummaryChanged();
                    StatusMessage = $"Auto-saved settings to {_settingsService.SettingsPath}";
                });
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                await Application.Current.Dispatcher.InvokeAsync(() =>
                    StatusMessage = $"Could not auto-save settings: {ex.Message}");
            }
        }, cancellation.Token);
    }

    private async Task TestCurseForgeAsync()
    {
        if (string.IsNullOrWhiteSpace(Settings.CurseForgeApiKey))
        {
            IntegrationsStatus = "Enter an API key before testing.";
            return;
        }

        IntegrationsStatus = "Testing connection...";
        try
        {
            using var service = new CurseForgeService(Settings.CurseForgeApiKey, Settings.CurseForgeGameId, Settings.CurseForgeTimeoutSeconds);
            var result = await service.TestConnectionAsync();
            IntegrationsStatus = result.Status == CurseForgeLookupStatus.Success
                ? "Connection successful. API key is valid."
                : $"Connection failed: {result.ErrorMessage}";
        }
        catch (Exception ex)
        {
            IntegrationsStatus = $"Test error: {ex.Message}";
        }
    }

    private void ApplyRuntimeSettings()
    {
        var shell = Application.Current.Windows.OfType<Shell>().FirstOrDefault();
        shell?.ApplySettings(Settings);
    }
}
