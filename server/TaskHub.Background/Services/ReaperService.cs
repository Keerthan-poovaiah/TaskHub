using TaskHub.Core;

namespace TaskHub.Background;

public class ReaperService : BackgroundService
{
    private readonly IJobQueue _queue;
    private readonly QueueOptions _options;
    private readonly ILogger<ReaperService> _logger;

    public ReaperService(IJobQueue queue, QueueOptions options, ILogger<ReaperService> logger)
    {
        _queue = queue;
        _options = options;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Reaper started.");
        var timeout = TimeSpan.FromSeconds(_options.VisibilityTimeoutSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // Situations 1 and 2: stuck jobs
                int recovered = await _queue.RecoverStuckJobsAsync(timeout);
                if (recovered > 0)
                    _logger.LogWarning("Recovered {Count} stuck job(s)", recovered);

                // Situation 3: retries whose waiting time is over
                int released = await _queue.ReleaseDueRetriesAsync();
                if (released > 0)
                    _logger.LogInformation("Released {Count} job(s) for retry", released);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;   // shutting down
            }
            catch (Exception ex)
            {
                // Redis is unreachable right now. Log it and try again on the next round.
                _logger.LogError(ex, "Reaper hit a problem. Will try again.");
            }

            await Task.Delay(1000, stoppingToken);
        }
    }
}