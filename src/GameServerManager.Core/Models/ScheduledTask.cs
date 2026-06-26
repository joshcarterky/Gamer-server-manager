namespace GameServerManager.Core.Models;

public class ScheduledTask
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string ServerId { get; set; } = string.Empty;
    public string ServerName { get; set; } = string.Empty;
    public ScheduledTaskType TaskType { get; set; }
    public ScheduleType ScheduleType { get; set; } = ScheduleType.Daily;

    // For Daily: "HH:mm" string (e.g. "04:00")
    public string DailyTimeText { get; set; } = "04:00";

    // For EveryNMinutes: run every this many minutes
    public int IntervalMinutes { get; set; } = 60;

    // RCON command text or broadcast message
    public string Parameter { get; set; } = string.Empty;

    // Minutes before a Restart to broadcast a warning (0 = no warning)
    public int WarnMinutesBefore { get; set; } = 0;

    public bool Enabled { get; set; } = true;

    public DateTime? LastRunAt { get; set; }
    public DateTime? NextRunAt { get; set; }
    public DateTime? LastPreWarnAt { get; set; }
}

public enum ScheduledTaskType
{
    Restart,
    Broadcast,
    DinoWipe,
    CustomRcon
}

public enum ScheduleType
{
    Daily,
    EveryNMinutes
}
