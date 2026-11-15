using StackExchange.Redis;

// Connect to Redis (the shared notebook running in Docker)
var redis = ConnectionMultiplexer.Connect("localhost:6379");
var db = redis.GetDatabase();

// Repeat 10 times: add one job to the line
for (int i = 1; i <= 100; i++)
{
    string job = $"job {i}";
    db.ListLeftPush("jobs", job);        // same as typing LPUSH jobs "job 1"
    Console.WriteLine($"Added: {job}");
    Thread.Sleep(500);                   // wait half a second before the next one
}

Console.WriteLine("Done adding jobs.");