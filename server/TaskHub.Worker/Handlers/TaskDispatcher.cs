using TaskHub.Core;

namespace TaskHub.Worker;

// Looks at the task's type and hands it to the right handler
public class TaskDispatcher : IJobHandler
{
    private readonly EmailHandler _email;
    private readonly UrlCheckHandler _urlCheck;

    public TaskDispatcher(EmailHandler email, UrlCheckHandler urlCheck)
    {
        _email = email;
        _urlCheck = urlCheck;
    }

    public Task HandleAsync(TaskItem task, int runId, CancellationToken cancellationToken)
    {
        return task.Type switch
        {
            TaskType.SendEmail => _email.SendAsync(task, runId, cancellationToken),
            TaskType.CheckUrl => _urlCheck.CheckAsync(task, cancellationToken),
            _ => throw new NotSupportedException($"Unknown task type: {task.Type}")
        };
    }
}