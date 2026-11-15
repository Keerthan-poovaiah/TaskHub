using StackExchange.Redis;

const int MaxAttempts = 5;
const int FailPercent = 10;              // 10% of attempts fail normally
const int CrashAfterWorkPercent = 10;    // 10% of the time the worker "dies" after sending
const bool UseIdempotencyKey = true;     // protection stays on from now on

var redis = ConnectionMultiplexer.Connect("localhost:6379");
var db = redis.GetDatabase();

// Prints a message with this worker's process number in front
void Log(string message) => Console.WriteLine($"[W{Environment.ProcessId}] {message}");

Log("Worker started.");

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
        Log($"Processing: {job}");
        Thread.Sleep(1000);

        if (Random.Shared.Next(100) < FailPercent)
            throw new Exception("Simulated failure");

        // The real-world side effect (pretend: send an email)
        bool shouldSend = true;

        if (UseIdempotencyKey)
        {
            // Succeeds only the FIRST time this job name is seen (NX = not exists)
            shouldSend = db.StringSet(
                $"email-sent:{job}", "1",
                expiry: TimeSpan.FromHours(24),
                when: When.NotExists);
        }

        if (shouldSend)
        {
            long timesSent = db.HashIncrement("sent", job);

            // If an email for this job was ever sent twice, count it as a double
            if (timesSent > 1)
                db.StringIncrement("doubles");

            Log($"EMAIL SENT for {job}");
        }
        else
        {
            Log($"Duplicate ignored: {job} was already handled");
        }

        // Pretend the worker dies right here: work done, but no ack
        if (Random.Shared.Next(100) < CrashAfterWorkPercent)
        {
            Log($"!! Simulated crash on {job} (no ack)");
            continue;
        }

        // SUCCESS: ack, clean up, and record the job as finished
        db.ListRemove("processing", job, 1);
        db.HashDelete("started", job);
        db.HashDelete("attempts", job);
        db.SetAdd("done", job);
        Log($"Finished: {job}");
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
            Log($"DEAD: {job} failed {attempts} times ({ex.Message})");
        }
        else
        {
            long delay = (long)Math.Pow(2, attempts - 1);
            long retryAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds() + delay;
            _ = tran.SortedSetAddAsync("delayed", job, retryAt);
            await tran.ExecuteAsync();
            Log($"Failed: {job} (attempt {attempts}). Retry in {delay}s");
        }
    }
}