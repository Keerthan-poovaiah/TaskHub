using Microsoft.EntityFrameworkCore;
using TaskHub.Core;

namespace TaskHub.Infrastructure;

public class EfRunStore : IRunStore
{
    private readonly TaskHubDbContext _db;

    public EfRunStore(TaskHubDbContext db)
    {
        _db = db;
    }

    public async Task<TaskItem?> StartRunAsync(int runId)
    {
        // Load the run together with its task (Include = fetch the related row too)
        JobRun? run = await _db.Runs
            .Include(r => r.TaskItem)
            .FirstOrDefaultAsync(r => r.Id == runId);

        if (run is null || run.TaskItem is null)
            return null;

        run.Status = RunStatus.Running;
        run.Attempts++;
        run.Error = null;
        await _db.SaveChangesAsync();

        return run.TaskItem;
    }

    public async Task MarkSucceededAsync(int runId)
    {
        JobRun? run = await _db.Runs.FindAsync(runId);
        if (run is null)
            return;

        run.Status = RunStatus.Succeeded;
        run.Error = null;
        run.FinishedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync();
    }

    public async Task MarkFailedAsync(int runId, string error, bool isDead)
    {
        JobRun? run = await _db.Runs.FindAsync(runId);
        if (run is null)
            return;

        run.Status = isDead ? RunStatus.Dead : RunStatus.Failed;
        run.Error = error;

        if (isDead)
            run.FinishedAtUtc = DateTime.UtcNow;

        await _db.SaveChangesAsync();
    }
}