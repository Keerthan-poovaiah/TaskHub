namespace TaskHub.Core;

public class QueueOptions
{
    public int MaxAttempts { get; set; } = 5;
    public int VisibilityTimeoutSeconds { get; set; } = 10;
}