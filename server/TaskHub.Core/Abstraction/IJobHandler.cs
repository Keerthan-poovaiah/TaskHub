namespace TaskHub.Core;

public interface IJobHandler
{
    Task HandleAsync(TaskItem task, int runId, CancellationToken cancellationToken);
}