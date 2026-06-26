using System.Text.Json;
using GameServerManager.Core.Models;

namespace GameServerManager.Services;

public class SchedulerService
{
    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };
    private readonly string _filePath;

    public SchedulerService(AppDataPaths paths)
    {
        _filePath = Path.Combine(paths.SettingsDirectory, "scheduler.json");
    }

    public async Task<List<ScheduledTask>> LoadAsync()
    {
        if (!File.Exists(_filePath))
            return new List<ScheduledTask>();

        try
        {
            var json = await File.ReadAllTextAsync(_filePath);
            return JsonSerializer.Deserialize<List<ScheduledTask>>(json, JsonOpts) ?? new List<ScheduledTask>();
        }
        catch
        {
            return new List<ScheduledTask>();
        }
    }

    public async Task SaveAsync(List<ScheduledTask> tasks)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_filePath)!);
        var json = JsonSerializer.Serialize(tasks, JsonOpts);
        await File.WriteAllTextAsync(_filePath, json);
    }

    public async Task AddOrUpdateAsync(ScheduledTask task)
    {
        var tasks = await LoadAsync();
        var existing = tasks.FindIndex(t => t.Id == task.Id);
        if (existing >= 0)
            tasks[existing] = task;
        else
            tasks.Add(task);
        await SaveAsync(tasks);
    }

    public async Task DeleteAsync(string taskId)
    {
        var tasks = await LoadAsync();
        tasks.RemoveAll(t => t.Id == taskId);
        await SaveAsync(tasks);
    }

    public static DateTime ComputeNextRun(ScheduledTask task, DateTime fromUtc)
    {
        if (task.ScheduleType == ScheduleType.EveryNMinutes)
            return fromUtc.AddMinutes(Math.Max(1, task.IntervalMinutes));

        // Daily: next occurrence of DailyTimeText (local time → UTC)
        if (!TimeSpan.TryParseExact(task.DailyTimeText, @"hh\:mm", null, out var tod))
            tod = new TimeSpan(4, 0, 0);

        var localNow = DateTime.Now;
        var todayAtTime = localNow.Date.Add(tod);
        var next = todayAtTime > localNow ? todayAtTime : todayAtTime.AddDays(1);
        return next.ToUniversalTime();
    }

    public static bool IsDue(ScheduledTask task, DateTime utcNow)
    {
        if (!task.Enabled) return false;
        if (task.NextRunAt == null) return false;
        return utcNow >= task.NextRunAt.Value;
    }

    public static bool IsPreWarnDue(ScheduledTask task, DateTime utcNow)
    {
        if (task.WarnMinutesBefore <= 0 || task.NextRunAt == null) return false;
        var warnTime = task.NextRunAt.Value.AddMinutes(-task.WarnMinutesBefore);
        if (utcNow < warnTime) return false;
        // Only send once — check LastPreWarnAt hasn't been set for this cycle
        if (task.LastPreWarnAt.HasValue && task.LastPreWarnAt.Value >= warnTime) return false;
        return true;
    }
}
