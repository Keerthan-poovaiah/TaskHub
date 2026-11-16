using TaskHub.Core;

namespace TaskHub.Worker;

public class JobWorker : BackgroundService
{
    private readonly IJobQueue _queue;
    private readonly IJobHandler _handler;
    private readonly ILogger<JobWorker> _logger;

    // DI hands these three tools in automatically
    public JobWorker(IJobQueue queue, IJobHandler handler, ILogger<JobWorker> logger)
    {
        _queue = queue;
        _handler = handler;
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

            try
            {
                _logger.LogInformation("Processing: {Job}", job);

                // Idempotency: only do the real work the first time
                if (await _queue.TryMarkHandledAsync(job))
                    await _handler.HandleAsync(job, stoppingToken);
                else
                    _logger.LogInformation("Duplicate ignored: {Job} was already handled", job);

                await _queue.AcknowledgeAsync(job);
                _logger.LogInformation("Finished: {Job}", job);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // Shutting down: leave the job in "processing". The Reaper will rescue it.
                break;
            }
            catch (Exception ex)
            {
                // Let the retry run the work again, then schedule the retry
                await _queue.ClearHandledAsync(job);
                FailResult result = await _queue.FailAsync(job);

                if (result.IsDead)
                    _logger.LogWarning("DEAD: {Job} failed {Attempts} times ({Reason})",
                        job, result.Attempts, ex.Message);
                else
                    _logger.LogWarning("Failed: {Job} (attempt {Attempts}). Retry in {Delay}s",
                        job, result.Attempts, result.DelaySeconds);
            }
        }
    }
}