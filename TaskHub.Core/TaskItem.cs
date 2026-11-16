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

    public List<JobRun> Runs { get; set; } = new();
}