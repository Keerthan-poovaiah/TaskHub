namespace TaskHub.Core;

public enum RunStatus
{
    Queued,
    Running,
    Succeeded,
    Failed,
    Dead
}

// One execution of a task. A task can have many runs (history).
public class JobRun
{
    public int Id { get; set; }

    public int TaskItemId { get; set; }
    public TaskItem? TaskItem { get; set; }

    public RunStatus Status { get; set; } = RunStatus.Queued;
    public int Attempts { get; set; }
    public string? Error { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? FinishedAtUtc { get; set; }
}