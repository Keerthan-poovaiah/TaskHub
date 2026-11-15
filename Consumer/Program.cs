using StackExchange.Redis;

const int MaxAttempts = 5;      // after this many failures, give up
const int FailPercent = 70;     // pretend 70% of attempts fail (so we can watch retries)

var redis = ConnectionMultiplexer.Connect("localhost:6379");
var db = redis.GetDatabase();

Console.WriteLine("Worker started. Waiting for jobs...");

while (true)
{
    // Take the oldest job from "jobs" and put it into "processing", in one step
    RedisValue job = db.ListMove("jobs", "processing", ListSide.Right, ListSide.Left);

    // Nothing waiting? Rest a moment and check again
    if (job.IsNull)
    {
        Thread.Sleep(500);
        continue;
    }

    // Write the pick-up time on the slip
    db.HashSet("started", job, DateTimeOffset.UtcNow.ToUnixTimeSeconds());

    try
    {
        Console.WriteLine($"Processing: {job}");
        Thread.Sleep(2000);                      // pretend the work takes 2 seconds

        // Pretend the work sometimes fails
        if (Random.Shared.Next(100) < FailPercent)
            throw new Exception("Simulated failure");

        // SUCCESS: clean up everything about this job
        db.ListRemove("processing", job, 1);
        db.HashDelete("started", job);
        db.HashDelete("attempts", job);
        Console.WriteLine($"Finished: {job}");
    }
    catch (Exception ex)
    {
        // FAILURE: count this failure (1, 2, 3, ...)
        long attempts = db.HashIncrement("attempts", job);

        // Everything below happens together, as one transaction
        var tran = db.CreateTransaction();
        _ = tran.ListRemoveAsync("processing", job, 1);
        _ = tran.HashDeleteAsync("started", job);

        if (attempts >= MaxAttempts)
        {
            // Too many failures: park it in the dead-letter list
            _ = tran.ListLeftPushAsync("dead", job);
            _ = tran.HashDeleteAsync("attempts", job);
            await tran.ExecuteAsync();
            Console.WriteLine($"DEAD: {job} failed {attempts} times ({ex.Message})");
        }
        else
        {
            // Wait 1s, 2s, 4s, 8s... then retry
            long delay = (long)Math.Pow(2, attempts - 1);
            long retryAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds() + delay;
            _ = tran.SortedSetAddAsync("delayed", job, retryAt);
            await tran.ExecuteAsync();
            Console.WriteLine($"Failed: {job} (attempt {attempts}). Retry in {delay}s");
        }
    }
}