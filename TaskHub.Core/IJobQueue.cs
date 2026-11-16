namespace TaskHub.Core;

public interface IJobQueue
{
    // Producer side: add a job to the waiting line
    Task EnqueueAsync(string job);

    // Worker side: take the oldest job (safely) or null if none are waiting
    Task<string?> DequeueAsync();

    // Worker side: the job finished, so remove all traces of it
    Task AcknowledgeAsync(string job);

    // Worker side: the job failed, so retry later or give up
    Task<FailResult> FailAsync(string job);

    // Idempotency: true only the first time we are asked about this job
    Task<bool> TryMarkHandledAsync(string job);
    Task ClearHandledAsync(string job);

    // Reaper side
    Task<int> RecoverStuckJobsAsync(TimeSpan timeout);
    Task<int> ReleaseDueRetriesAsync();
}

// What happened when a job failed (used for logging)
public record FailResult(bool IsDead, long Attempts, long DelaySeconds);