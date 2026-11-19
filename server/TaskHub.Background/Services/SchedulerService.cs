using Microsoft.EntityFrameworkCore;
using TaskHub.Core;
using TaskHub.Infrastructure;

namespace TaskHub.Background;

public class SchedulerService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IJobQueue _queue;
    private readonly ILogger<SchedulerService> _logger;

    public SchedulerService(
        IServiceScopeFactory scopeFactory,
        IJobQueue queue,
        ILogger<SchedulerService> logger)
    {
        _scopeFactory = scopeFactory;
        _queue = queue;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Scheduler started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunDueTasksAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                // A failed check must never kill the scheduler. Log it and try again.
                _logger.LogError(ex, "Scheduler check failed. Will retry.");
            }

            await Task.Delay(5000, stoppingToken);   // look at the clock every 5 seconds
        }
    }

    private async Task RunDueTasksAsync(CancellationToken ct)
    {
        // A fresh database remote control for this one check
        using IServiceScope scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TaskHubDbContext>();

        DateTime now = DateTime.UtcNow;

        // Which active, scheduled tasks are due right now?
        List<TaskItem> due = await db.Tasks
            .AsNoTracking()
            .Where(t => t.IsActive
                     && t.CronExpression != null
                     && t.NextRunAtUtc != null
                     && t.NextRunAtUtc <= now)
            .OrderBy(t => t.NextRunAtUtc)
            .Take(100)
            .ToListAsync(ct);

        foreach (TaskItem task in due)
        {
            // Work out the next time this task is due. Counting from NOW means missed
            // runs are skipped, not replayed all at once after downtime.
            if (!CronSchedule.TryGetNext(task.CronExpression!, now, out DateTime next))
            {
                _logger.LogWarning("Task {TaskId} has an invalid schedule. Pausing it.", task.Id);
                await db.Tasks
                    .Where(t => t.Id == task.Id)
                    .ExecuteUpdateAsync(s => s.SetProperty(t => t.IsActive, false), ct);
                continue;
            }

            // Do the next steps as one unbreakable block (a database transaction)
            await using var tx = await db.Database.BeginTransactionAsync(ct);

            // CLAIM the task: only succeeds if nobody else changed it since we read it.
            // This prevents two schedulers from running the same task twice.
            int claimed = await db.Tasks
                .Where(t => t.Id == task.Id && t.IsActive && t.NextRunAtUtc == task.NextRunAtUtc)
                .ExecuteUpdateAsync(s => s.SetProperty(t => t.NextRunAtUtc, next), ct);

            if (claimed == 0)
            {
                await tx.RollbackAsync(ct);
                continue;   // someone else got it, or it was paused just now
            }

            // Write the new run in the ledger
            var run = new JobRun { TaskItemId = task.Id };
            db.Runs.Add(run);
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);

            // Put the run's ID on the hook for the workers. If this step fails, the run stays
            // "Queued" in the database and the Sweeper puts it back on the hook later.
            await _queue.EnqueueAsync(run.Id.ToString());

            _logger.LogInformation("Scheduled run {RunId} for task {TaskId} '{Name}'. Next: {Next:u}",
                run.Id, task.Id, task.Name, next);
        }
    }
}