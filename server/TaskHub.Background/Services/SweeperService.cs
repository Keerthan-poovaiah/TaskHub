using Microsoft.EntityFrameworkCore;
using TaskHub.Core;
using TaskHub.Infrastructure;

namespace TaskHub.Background;

// The safety net between the database and Redis.
// A run that stays "Queued" in the database but can't be found anywhere in the queue for a
// couple of minutes was lost (the API crashed before queueing it, or Redis forgot its data).
// The sweeper puts it back on the hook.
public class SweeperService : BackgroundService
{
    private static readonly TimeSpan MinimumAge = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan CheckEvery = TimeSpan.FromSeconds(30);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IJobQueue _queue;
    private readonly ILogger<SweeperService> _logger;

    public SweeperService(
        IServiceScopeFactory scopeFactory,
        IJobQueue queue,
        ILogger<SweeperService> logger)
    {
        _scopeFactory = scopeFactory;
        _queue = queue;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Sweeper started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await SweepAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Sweeper check failed. Will retry.");
            }

            await Task.Delay(CheckEvery, stoppingToken);
        }
    }

    private async Task SweepAsync(CancellationToken ct)
    {
        using IServiceScope scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TaskHubDbContext>();

        DateTime cutoff = DateTime.UtcNow - MinimumAge;

        // Runs that have been "Queued" for a while
        List<int> oldQueuedRunIds = await db.Runs
            .AsNoTracking()
            .Where(r => r.Status == RunStatus.Queued && r.CreatedAtUtc < cutoff)
            .OrderBy(r => r.Id)
            .Select(r => r.Id)
            .Take(100)
            .ToListAsync(ct);

        foreach (int runId in oldQueuedRunIds)
        {
            string job = runId.ToString();

            // Still waiting on one of the shelves? Then nothing is lost.
            if (await _queue.IsTrackedAsync(job))
                continue;

            await _queue.EnqueueAsync(job);
            _logger.LogWarning("Sweeper re-queued lost run {RunId}", runId);
        }
    }
}