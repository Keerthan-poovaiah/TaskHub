using TaskHub.Core;

namespace TaskHub.Worker;

public class WorkerService : BackgroundService
{
    private readonly IJobQueue _queue;
    private readonly IJobHandler _handler;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<WorkerService> _logger;

    public WorkerService(
        IJobQueue queue,
        IJobHandler handler,
        IServiceScopeFactory scopeFactory,
        ILogger<WorkerService> logger)
    {
        _queue = queue;
        _handler = handler;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Worker started (process {Pid}).", Environment.ProcessId);

        while (!stoppingToken.IsCancellationRequested)
        {
            string? job = await _queue.DequeueAsync();

            if (job is null)
            {
                await Task.Delay(500, stoppingToken);
                continue;
            }

            // The job text must be a run ID like "7". Anything else is junk: drop it.
            if (!int.TryParse(job, out int runId))
            {
                _logger.LogWarning("Skipping '{Job}': not a run ID", job);
                await _queue.AcknowledgeAsync(job);
                continue;
            }

            // A fresh "ledger" for this one job, thrown away at the end of the loop turn
            using IServiceScope scope = _scopeFactory.CreateScope();
            IRunStore store = scope.ServiceProvider.GetRequiredService<IRunStore>();

            try
            {
                // Ledger: look up the task and write "Running"
                TaskItem? task = await store.StartRunAsync(runId);

                if (task is null)
                {
                    _logger.LogWarning("Run {RunId} not found in the database. Dropping it.", runId);
                    await _queue.AcknowledgeAsync(job);
                    continue;
                }

                _logger.LogInformation("Processing run {RunId}: '{Name}'", runId, task.Name);

                // Idempotency: only do the real work the first time
                if (await _queue.TryMarkHandledAsync(job))
                    await _handler.HandleAsync(task, runId, stoppingToken);
                else
                    _logger.LogInformation("Duplicate ignored: run {RunId} was already handled", runId);

                // Ledger: write "Succeeded", then remove the slip from the queue
                await store.MarkSucceededAsync(runId);
                await _queue.AcknowledgeAsync(job);
                _logger.LogInformation("Finished run {RunId}", runId);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // Shutting down: leave the job in "processing". The Reaper will rescue it.
                break;
            }
            catch (Exception ex)
            {
                // Let a retry run the work again, then decide: retry later or give up
                await _queue.ClearHandledAsync(job);
                FailResult result = await _queue.FailAsync(job);

                // Ledger: write "Failed" (will retry) or "Dead" (gave up)
                await store.MarkFailedAsync(runId, ex.Message, result.IsDead);

                if (result.IsDead)
                    _logger.LogWarning("DEAD: run {RunId} failed {Attempts} times ({Reason})",
                        runId, result.Attempts, ex.Message);
                else
                    _logger.LogWarning("Failed: run {RunId} (attempt {Attempts}). Retry in {Delay}s",
                        runId, result.Attempts, result.DelaySeconds);
            }
        }
    }
}