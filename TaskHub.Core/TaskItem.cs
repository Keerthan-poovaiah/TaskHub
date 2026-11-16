namespace TaskHub.Core;

public enum TaskType
{
    SendEmail,
    CheckUrl
}

// One automation the user created, e.g. "email me a reminder"
public class TaskItem
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public TaskType Type { get; set; }
    public string Target { get; set; } = "";   // an email address or a URL
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    // Scheduling. All empty for a one-off task.
    public string? CronExpression { get; set; }    // e.g. "0 8 * * MON"
    public DateTime? NextRunAtUtc { get; set; }    // when the scheduler should run it next
    public bool IsActive { get; set; } = true;     // false = paused

    public List<JobRun> Runs { get; set; } = new();
}