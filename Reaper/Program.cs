using StackExchange.Redis;

const int TimeoutSeconds = 10;   // a job stuck longer than this is considered abandoned

var redis = ConnectionMultiplexer.Connect("localhost:6379");
var db = redis.GetDatabase();

Console.WriteLine("Reaper started. Watching for stuck jobs and due retries...");

while (true)
{
    long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

    // PART 1: rescue stuck jobs (same as before)
    RedisValue[] processing = db.ListRange("processing", 0, -1);

    foreach (RedisValue job in processing)
    {
        RedisValue started = db.HashGet("started", job);

        if (started.IsNull)
        {
            db.HashSet("started", job, now);
            continue;
        }

        if (now - (long)started > TimeoutSeconds)
        {
            var tran = db.CreateTransaction();
            _ = tran.ListRemoveAsync("processing", job, 1);
            _ = tran.ListLeftPushAsync("jobs", job);
            _ = tran.HashDeleteAsync("started", job);
            await tran.ExecuteAsync();

            Console.WriteLine($"Recovered stuck job: {job}");
        }
    }

    // PART 2 (NEW): move retries whose waiting time is over back into the line
    RedisValue[] due = db.SortedSetRangeByScore("delayed", double.NegativeInfinity, now);

    foreach (RedisValue job in due)
    {
        var tran = db.CreateTransaction();
        _ = tran.SortedSetRemoveAsync("delayed", job);
        _ = tran.ListLeftPushAsync("jobs", job);
        await tran.ExecuteAsync();

        Console.WriteLine($"Retrying: {job}");
    }

    Thread.Sleep(500);   // check again in half a second
}