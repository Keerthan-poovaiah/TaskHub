namespace TaskHub.Core;

public interface IJobHandler
{
    Task HandleAsync(TaskItem task, CancellationToken cancellationToken);
}