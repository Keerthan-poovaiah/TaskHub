using TaskHub.Core;

namespace TaskHub.Worker;

public class FakeEmailHandler : IJobHandler
{
    private const int FailPercent = 100;   // pretend the email service fails 20% of the time

    private readonly ILogger<FakeEmailHandler> _logger;

    public FakeEmailHandler(ILogger<FakeEmailHandler> logger)
    {
        _logger = logger;
    }

    public async Task HandleAsync(TaskItem task, CancellationToken cancellationToken)
    {
        await Task.Delay(1000, cancellationToken);   // pretend the work takes 1 second

        if (Random.Shared.Next(100) < FailPercent)
            throw new Exception("Simulated email service failure");

        _logger.LogInformation("EMAIL SENT to {Target} for task '{Name}'", task.Target, task.Name);
    }
}