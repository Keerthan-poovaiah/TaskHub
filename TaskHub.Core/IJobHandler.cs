namespace TaskHub.Core;

public interface IJobHandler
{
    Task HandleAsync(string job, CancellationToken cancellationToken);
}