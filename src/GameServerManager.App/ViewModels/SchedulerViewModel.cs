using System.Collections.ObjectModel;
using System.Windows.Input;
using GameServerManager.Core.Models;
using GameServerManager.GameProviders;
using GameServerManager.Services;

namespace GameServerManager.App.ViewModels;

public class SchedulerViewModel : BaseViewModel
{
    private readonly SchedulerService _scheduler;
    private readonly ServersJsonService _serversJsonService;

    // Form state
    private string _selectedServerId = string.Empty;
    private string _selectedServerName = string.Empty;
    private ScheduledTaskType _selectedTaskType = ScheduledTaskType.Restart;
    private ScheduleType _selectedScheduleType = ScheduleType.Daily;
    private string _dailyTime = "04:00";
    private int _intervalMinutes = 60;
    private string _parameter = string.Empty;
    private int _warnMinutesBefore = 0;
    private string _statusMessage = string.Empty;
    private bool _hasStatusMessage;

    public SchedulerViewModel()
    {
        var paths = new AppDataPaths();
        _scheduler = new SchedulerService(paths);
        _serversJsonService = new ServersJsonService(paths);

        AddTaskCommand = new RelayCommand(async _ => await AddTaskAsync(), () => !string.IsNullOrWhiteSpace(_selectedServerId));
        DeleteTaskCommand = new RelayCommand(async p => await DeleteTaskAsync(p as ScheduledTaskEntryViewModel));
        ToggleEnabledCommand = new RelayCommand(async p => await ToggleEnabledAsync(p as ScheduledTaskEntryViewModel));

        TaskTypes = new List<string> { "Restart", "Broadcast", "Dino Wipe", "Custom RCON" };
        ScheduleTypes = new List<string> { "Daily at time", "Every N minutes" };

        _ = LoadAsync();
    }

    public ObservableCollection<ScheduledTaskEntryViewModel> Tasks { get; } = new();
    public ObservableCollection<string> ServerNames { get; } = new();
    private List<ServerProfile> _profiles = new();

    public ICommand AddTaskCommand { get; }
    public ICommand DeleteTaskCommand { get; }
    public ICommand ToggleEnabledCommand { get; }

    public List<string> TaskTypes { get; }
    public List<string> ScheduleTypes { get; }

    public string SelectedServerName
    {
        get => _selectedServerName;
        set
        {
            if (SetProperty(ref _selectedServerName, value))
            {
                var profile = _profiles.FirstOrDefault(p => p.ProfileName == value);
                _selectedServerId = profile?.Id ?? string.Empty;
                ((RelayCommand)AddTaskCommand).NotifyCanExecuteChanged();
            }
        }
    }

    public int SelectedTaskTypeIndex
    {
        get => (int)_selectedTaskType;
        set
        {
            _selectedTaskType = (ScheduledTaskType)value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(ShowParameter));
            OnPropertyChanged(nameof(ShowWarnMinutes));
            OnPropertyChanged(nameof(ParameterLabel));
        }
    }

    public int SelectedScheduleTypeIndex
    {
        get => (int)_selectedScheduleType;
        set
        {
            _selectedScheduleType = (ScheduleType)value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(ShowDailyTime));
            OnPropertyChanged(nameof(ShowInterval));
        }
    }

    public string DailyTime
    {
        get => _dailyTime;
        set => SetProperty(ref _dailyTime, value);
    }

    public int IntervalMinutes
    {
        get => _intervalMinutes;
        set => SetProperty(ref _intervalMinutes, value);
    }

    public string Parameter
    {
        get => _parameter;
        set => SetProperty(ref _parameter, value);
    }

    public int WarnMinutesBefore
    {
        get => _warnMinutesBefore;
        set => SetProperty(ref _warnMinutesBefore, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set
        {
            SetProperty(ref _statusMessage, value);
            HasStatusMessage = !string.IsNullOrEmpty(value);
        }
    }

    public bool HasStatusMessage
    {
        get => _hasStatusMessage;
        private set => SetProperty(ref _hasStatusMessage, value);
    }

    public bool HasTasks => Tasks.Count > 0;
    public bool HasNoTasks => Tasks.Count == 0;

    public bool ShowParameter => _selectedTaskType is ScheduledTaskType.Broadcast or ScheduledTaskType.CustomRcon;
    public bool ShowWarnMinutes => _selectedTaskType == ScheduledTaskType.Restart;
    public bool ShowDailyTime => _selectedScheduleType == ScheduleType.Daily;
    public bool ShowInterval => _selectedScheduleType == ScheduleType.EveryNMinutes;

    public string ParameterLabel => _selectedTaskType switch
    {
        ScheduledTaskType.Broadcast => "Broadcast message",
        ScheduledTaskType.CustomRcon => "RCON command",
        _ => "Parameter"
    };

    private async Task LoadAsync()
    {
        _profiles = (await _serversJsonService.LoadServersAsync()).ToList();
        ServerNames.Clear();
        foreach (var p in _profiles)
            ServerNames.Add(p.ProfileName);

        var tasks = await _scheduler.LoadAsync();
        Tasks.Clear();
        foreach (var task in tasks)
            Tasks.Add(new ScheduledTaskEntryViewModel(task));

        OnPropertyChanged(nameof(HasTasks));
        OnPropertyChanged(nameof(HasNoTasks));
    }

    private async Task AddTaskAsync()
    {
        var task = new ScheduledTask
        {
            ServerId = _selectedServerId,
            ServerName = _selectedServerName,
            TaskType = _selectedTaskType,
            ScheduleType = _selectedScheduleType,
            DailyTimeText = string.IsNullOrWhiteSpace(_dailyTime) ? "04:00" : _dailyTime.Trim(),
            IntervalMinutes = Math.Max(1, _intervalMinutes),
            Parameter = _parameter.Trim(),
            WarnMinutesBefore = Math.Max(0, _warnMinutesBefore),
            Enabled = true
        };
        task.NextRunAt = SchedulerService.ComputeNextRun(task, DateTime.UtcNow);

        await _scheduler.AddOrUpdateAsync(task);
        Tasks.Add(new ScheduledTaskEntryViewModel(task));
        StatusMessage = $"Task added — next run: {task.NextRunAt?.ToLocalTime():g}";
        OnPropertyChanged(nameof(HasTasks));
        OnPropertyChanged(nameof(HasNoTasks));
    }

    private async Task DeleteTaskAsync(ScheduledTaskEntryViewModel? entry)
    {
        if (entry == null) return;
        await _scheduler.DeleteAsync(entry.Task.Id);
        Tasks.Remove(entry);
        StatusMessage = "Task deleted.";
        OnPropertyChanged(nameof(HasTasks));
        OnPropertyChanged(nameof(HasNoTasks));
    }

    private async Task ToggleEnabledAsync(ScheduledTaskEntryViewModel? entry)
    {
        if (entry == null) return;
        entry.Task.Enabled = !entry.Task.Enabled;
        entry.RefreshEnabled();
        await _scheduler.AddOrUpdateAsync(entry.Task);
        StatusMessage = $"Task {(entry.Task.Enabled ? "enabled" : "disabled")}.";
    }
}

public class ScheduledTaskEntryViewModel : BaseViewModel
{
    public ScheduledTask Task { get; }

    public ScheduledTaskEntryViewModel(ScheduledTask task)
    {
        Task = task;
    }

    public string ServerName => Task.ServerName;

    public string TypeText => Task.TaskType switch
    {
        ScheduledTaskType.Restart => "Restart",
        ScheduledTaskType.Broadcast => "Broadcast",
        ScheduledTaskType.DinoWipe => "Dino Wipe",
        ScheduledTaskType.CustomRcon => "Custom RCON",
        _ => "Unknown"
    };

    public string ScheduleText => Task.ScheduleType == ScheduleType.Daily
        ? $"Daily at {Task.DailyTimeText}"
        : $"Every {Task.IntervalMinutes} min";

    public string NextRunText => Task.NextRunAt?.ToLocalTime().ToString("g") ?? "Not scheduled";

    public string LastRunText => Task.LastRunAt?.ToLocalTime().ToString("g") ?? "Never";

    public string ParameterText => string.IsNullOrWhiteSpace(Task.Parameter) ? "—" : Task.Parameter;

    public string EnabledText => Task.Enabled ? "ON" : "OFF";
    public string EnabledColor => Task.Enabled ? "#3DDA85" : "#7A95AA";

    public void RefreshEnabled()
    {
        OnPropertyChanged(nameof(EnabledText));
        OnPropertyChanged(nameof(EnabledColor));
    }

    public void RefreshRun()
    {
        OnPropertyChanged(nameof(NextRunText));
        OnPropertyChanged(nameof(LastRunText));
    }
}
