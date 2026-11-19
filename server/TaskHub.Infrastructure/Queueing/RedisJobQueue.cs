using StackExchange.Redis;
using TaskHub.Core;

namespace TaskHub.Infrastructure;

public class RedisJobQueue : IJobQueue
{
    private readonly IDatabase _db;
    private readonly int _maxAttempts;

    public RedisJobQueue(IConnectionMultiplexer redis, QueueOptions options)
    {
        _db = redis.GetDatabase();
        _maxAttempts = options.MaxAttempts;
    }

    public async Task EnqueueAsync(string job)
    {
        await _db.ListLeftPushAsync(QueueKeys.Jobs, job);
    }

    public async Task<string?> DequeueAsync()
    {
        // Move the oldest job from "jobs" to "processing" in one atomic step
        RedisValue job = await _db.ListMoveAsync(
            QueueKeys.Jobs, QueueKeys.Processing, ListSide.Right, ListSide.Left);

        if (job.IsNull)
            return null;

        // Write the pick-up time so the Reaper can detect a stuck job
        await _db.HashSetAsync(QueueKeys.Started, job, DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        return job.ToString();
    }

    public async Task AcknowledgeAsync(string job)
    {
        var tran = _db.CreateTransaction();
        _ = tran.ListRemoveAsync(QueueKeys.Processing, job, 1);
        _ = tran.HashDeleteAsync(QueueKeys.Started, job);
        _ = tran.HashDeleteAsync(QueueKeys.Attempts, job);
        await tran.ExecuteAsync();
    }

    public async Task<FailResult> FailAsync(string job)
    {
        long attempts = await _db.HashIncrementAsync(QueueKeys.Attempts, job);

        var tran = _db.CreateTransaction();
        _ = tran.ListRemoveAsync(QueueKeys.Processing, job, 1);
        _ = tran.HashDeleteAsync(QueueKeys.Started, job);

        if (attempts >= _maxAttempts)
        {
            // Too many failures: park it in the dead-letter list
            _ = tran.ListLeftPushAsync(QueueKeys.Dead, job);
            _ = tran.HashDeleteAsync(QueueKeys.Attempts, job);
            await tran.ExecuteAsync();
            return new FailResult(true, attempts, 0);
        }

        // Exponential backoff: wait 1s, 2s, 4s, 8s...
        long delay = (long)Math.Pow(2, attempts - 1);
        long retryAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds() + delay;
        _ = tran.SortedSetAddAsync(QueueKeys.Delayed, job, retryAt);
        await tran.ExecuteAsync();
        return new FailResult(false, attempts, delay);
    }

    public async Task<bool> TryMarkHandledAsync(string job)
    {
        // true only for the first caller (NX = only if the key does not exist)
        return await _db.StringSetAsync(
            $"{QueueKeys.HandledPrefix}{job}", "1",
            expiry: TimeSpan.FromHours(24),
            when: When.NotExists);
    }

    public async Task ClearHandledAsync(string job)
    {
        await _db.KeyDeleteAsync($"{QueueKeys.HandledPrefix}{job}");
    }

    public async Task<int> RecoverStuckJobsAsync(TimeSpan timeout)
    {
        long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        int recovered = 0;

        RedisValue[] processing = await _db.ListRangeAsync(QueueKeys.Processing, 0, -1);

        foreach (RedisValue job in processing)
        {
            RedisValue started = await _db.HashGetAsync(QueueKeys.Started, job);

            // No time note yet: start the clock now
            if (started.IsNull)
            {
                await _db.HashSetAsync(QueueKeys.Started, job, now);
                continue;
            }

            // Stuck too long: put it back in the line
            if (now - (long)started > timeout.TotalSeconds)
            {
                var tran = _db.CreateTransaction();
                _ = tran.ListRemoveAsync(QueueKeys.Processing, job, 1);
                _ = tran.ListLeftPushAsync(QueueKeys.Jobs, job);
                _ = tran.HashDeleteAsync(QueueKeys.Started, job);
                await tran.ExecuteAsync();
                recovered++;
            }
        }

        return recovered;
    }

    public async Task<int> ReleaseDueRetriesAsync()
    {
        long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        RedisValue[] due = await _db.SortedSetRangeByScoreAsync(
            QueueKeys.Delayed, double.NegativeInfinity, now);

        foreach (RedisValue job in due)
        {
            var tran = _db.CreateTransaction();
            _ = tran.SortedSetRemoveAsync(QueueKeys.Delayed, job);
            _ = tran.ListLeftPushAsync(QueueKeys.Jobs, job);
            await tran.ExecuteAsync();
        }

        return due.Length;
    }

    public async Task<bool> IsTrackedAsync(string job)
    {
        // Look on the three shelves a live job can be on: waiting, in someone's hands, or waiting to retry
        if (await _db.ListPositionAsync(QueueKeys.Jobs, job) >= 0)
            return true;

        if (await _db.ListPositionAsync(QueueKeys.Processing, job) >= 0)
            return true;

        return await _db.SortedSetScoreAsync(QueueKeys.Delayed, job) is not null;
    }
}