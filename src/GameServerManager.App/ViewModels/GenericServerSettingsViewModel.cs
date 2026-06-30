using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows;
using GameServerManager.Core.Models;
using GameServerManager.GameProviders;
using GameServerManager.Services;
using Microsoft.Win32;

namespace GameServerManager.App.ViewModels;

public sealed class GenericServerSettingsViewModel : BaseViewModel
{
    private readonly ServerProfile _profile;
    private readonly ServersJsonService _serversJsonService = new(new AppDataPaths());
    private string _selectedCategory = string.Empty;
    private string _searchText = string.Empty;
    private string _message = string.Empty;

    public GenericServerSettingsViewModel(ServerProfile profile, IGameServerProvider provider)
    {
        _profile = profile;
        GameName = provider.GameName;
        ServerName = profile.ProfileName;

        var allSettings = provider.SettingsDefinitions
            .Select(def => new GenericSettingItemViewModel(
                def,
                profile.Settings.TryGetValue(def.SettingKey, out var v) ? v : def.DefaultValue ?? string.Empty))
            .ToList();

        AllSettings = new ObservableCollection<GenericSettingItemViewModel>(allSettings);
        Categories = new ObservableCollection<string>(
            allSettings.Select(s => s.Category).Where(c => !string.IsNullOrWhiteSpace(c)).Distinct());
        FilteredSettings = new ObservableCollection<GenericSettingItemViewModel>(allSettings);

        SelectedCategory = Categories.FirstOrDefault() ?? string.Empty;
        SaveCommand = new RelayCommand(async _ => await SaveAsync());
        RevertCommand = new RelayCommand(_ => Revert());
    }

    public string GameName { get; }
    public string ServerName { get; }
    public ObservableCollection<GenericSettingItemViewModel> AllSettings { get; }
    public ObservableCollection<GenericSettingItemViewModel> FilteredSettings { get; }
    public ObservableCollection<string> Categories { get; }

    public string SelectedCategory
    {
        get => _selectedCategory;
        set
        {
            _selectedCategory = value;
            OnPropertyChanged();
            ApplyFilter();
        }
    }

    public string SearchText
    {
        get => _searchText;
        set
        {
            _searchText = value;
            OnPropertyChanged();
            ApplyFilter();
        }
    }

    public string Message
    {
        get => _message;
        set => SetProperty(ref _message, value);
    }

    public bool HasUnsavedChanges => AllSettings.Any(s => s.IsModified);
    public bool HasMessage => !string.IsNullOrEmpty(_message);
    public RelayCommand SaveCommand { get; }
    public RelayCommand RevertCommand { get; }

    private void ApplyFilter()
    {
        var items = AllSettings.AsEnumerable();
        if (!string.IsNullOrWhiteSpace(_selectedCategory))
            items = items.Where(s => s.Category == _selectedCategory);
        if (!string.IsNullOrWhiteSpace(_searchText))
            items = items.Where(s =>
                s.DisplayName.Contains(_searchText, StringComparison.OrdinalIgnoreCase) ||
                s.Key.Contains(_searchText, StringComparison.OrdinalIgnoreCase));

        FilteredSettings.Clear();
        foreach (var item in items)
            FilteredSettings.Add(item);
    }

    private async Task SaveAsync()
    {
        foreach (var setting in AllSettings)
            _profile.Settings[setting.Key] = setting.Value;

        await _serversJsonService.UpdateServerAsync(_profile);

        foreach (var setting in AllSettings)
            setting.CommitSave();

        Message = $"Settings saved for {ServerName}.";
        OnPropertyChanged(nameof(HasUnsavedChanges));
        OnPropertyChanged(nameof(HasMessage));
    }

    private void Revert()
    {
        foreach (var setting in AllSettings)
            setting.Revert();

        Message = "Reverted to saved values.";
        OnPropertyChanged(nameof(HasUnsavedChanges));
        OnPropertyChanged(nameof(HasMessage));
    }
}

public sealed class GenericSettingItemViewModel : BaseViewModel
{
    private string _value;
    private string _savedValue;

    public GenericSettingItemViewModel(ServerSettingDefinition def, string currentValue)
    {
        Key = def.SettingKey;
        DisplayName = def.DisplayName;
        Category = def.Category ?? string.Empty;
        DefaultValue = def.DefaultValue ?? string.Empty;
        _value = currentValue;
        _savedValue = currentValue;

        IsText = def.ControlType == SettingControlType.TextBox;
        IsPassword = def.ControlType == SettingControlType.PasswordField;
        IsToggle = def.ControlType == SettingControlType.Toggle;
        IsDropdown = def.ControlType == SettingControlType.Dropdown;
        IsNumber = def.ControlType == SettingControlType.NumberBox;
        IsFolderPicker = def.ControlType is SettingControlType.FolderPicker or SettingControlType.FilePicker;

        ParsedOptions = def.Options?.Select(o =>
        {
            var idx = o.IndexOf(':');
            return idx >= 0 ? new OptionItem(o[..idx], o[(idx + 1)..]) : new OptionItem(o, o);
        }).ToList() ?? new List<OptionItem>();

        MinValue = def.MinValue;
        MaxValue = def.MaxValue;

        BrowseCommand = new RelayCommand(_ =>
        {
            var dialog = new OpenFolderDialog { Title = $"Select folder for {DisplayName}" };
            if (dialog.ShowDialog() == true)
                Value = dialog.FolderName;
        });
    }

    public string Key { get; }
    public string DisplayName { get; }
    public string Category { get; }
    public string DefaultValue { get; }
    public bool IsText { get; }
    public bool IsPassword { get; }
    public bool IsToggle { get; }
    public bool IsDropdown { get; }
    public bool IsNumber { get; }
    public bool IsFolderPicker { get; }
    public IReadOnlyList<OptionItem> ParsedOptions { get; }
    public int? MinValue { get; }
    public int? MaxValue { get; }
    public RelayCommand BrowseCommand { get; }

    public bool IsModified => !string.Equals(_value, _savedValue, StringComparison.Ordinal);

    public string Value
    {
        get => _value;
        set
        {
            _value = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsModified));
        }
    }

    public bool BooleanValue
    {
        get => _value.Equals("True", StringComparison.OrdinalIgnoreCase);
        set
        {
            Value = value ? "True" : "False";
            OnPropertyChanged();
        }
    }

    public OptionItem? SelectedOption
    {
        get => ParsedOptions.FirstOrDefault(o => o.Value.Equals(_value, StringComparison.OrdinalIgnoreCase));
        set
        {
            if (value != null)
            {
                Value = value.Value;
                OnPropertyChanged();
            }
        }
    }

    public void CommitSave()
    {
        _savedValue = _value;
        OnPropertyChanged(nameof(IsModified));
    }

    public void Revert()
    {
        Value = _savedValue;
    }
}

public sealed record OptionItem(string Value, string Label);
