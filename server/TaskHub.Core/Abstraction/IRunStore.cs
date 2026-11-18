namespace TaskHub.Core;

// The ledger: how the worker reads and updates run records in the database
public interface IRunStore
{
    // Marks the run as Running, adds 1 to its attempts, and returns the task to do.
    // Returns null if no run with that ID exists.
    Task<TaskItem?> StartRunAsync(int runId);

    // The work finished successfully
    Task MarkSucceededAsync(int runId);

    // The work failed. isDead = true means we gave up for good.
    Task MarkFailedAsync(int runId, string error, bool isDead);
}