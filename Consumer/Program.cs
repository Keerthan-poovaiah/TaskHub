using StackExchange.Redis;

const int MaxAttempts = 5;
const int FailPercent = 0;               // no normal failures in this experiment
const int CrashAfterWorkPercent = 40;    // pretend the worker dies right after sending
const bool UseIdempotencyKey = false;    // experiment 1: false, experiment 2: true

var redis = ConnectionMultiplexer.Connect("localhost:6379");
var db = redis.GetDatabase();

Console.WriteLine($"Worker started. Idempotency key on: {UseIdempotencyKey}");

while (true)
{
    // Take the oldest job from "jobs" and put it into "processing", in one step
    RedisValue job = db.ListMove("jobs", "processing", ListSide.Right, ListSide.Left);

    if (job.IsNull)
    {
        Thread.Sleep(500);
        continue;
    }

    db.HashSet("started", job, DateTimeOffset.UtcNow.ToUnixTimeSeconds());

    try
    {
        Console.WriteLine($"Processing: {job}");
        Thread.Sleep(2000);

        if (Random.Shared.Next(100) < FailPercent)
            throw new Exception("Simulated failure");

        // The real-world side effect (pretend: send an email)
        bool shouldSend = true;

        if (UseIdempotencyKey)
        {
            // Succeeds only the FIRST time this job name is seen (NX = not exists).
            // The key expires after 24 hours so Redis doesn't fill up forever.
            shouldSend = db.StringSet(
                $"email-sent:{job}", "1",
                expiry: TimeSpan.FromHours(24),
                when: When.NotExists);
        }

        if (shouldSend)
        {
            db.HashIncrement("sent", job);       // counts how many emails really went out
            Console.WriteLine($"EMAIL SENT for {job}");
        }
        else
        {
            Console.WriteLine($"Duplicate ignored: {job} was already handled");
        }

        // Pretend the worker dies right here: work done, but no ack
        if (Random.Shared.Next(100) < CrashAfterWorkPercent)
        {
            Console.WriteLine($"!! Simulated crash on {job} (no ack)");
            continue;
        }

        // SUCCESS: ack and clean up
        db.ListRemove("processing", job, 1);
        db.HashDelete("started", job);
        db.HashDelete("attempts", job);
        Console.WriteLine($"Finished: {job}");
    }
    catch (Exception ex)
    {
        long attempts = db.HashIncrement("attempts", job);

        var tran = db.CreateTransaction();
        _ = tran.ListRemoveAsync("processing", job, 1);
        _ = tran.HashDeleteAsync("started", job);

        if (attempts >= MaxAttempts)
        {
            _ = tran.ListLeftPushAsync("dead", job);
            _ = tran.HashDeleteAsync("attempts", job);
            await tran.ExecuteAsync();
            Console.WriteLine($"DEAD: {job} failed {attempts} times ({ex.Message})");
        }
        else
        {
            long delay = (long)Math.Pow(2, attempts - 1);
            long retryAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds() + delay;
            _ = tran.SortedSetAddAsync("delayed", job, retryAt);
            await tran.ExecuteAsync();
            Console.WriteLine($"Failed: {job} (attempt {attempts}). Retry in {delay}s");
        }
    }
}