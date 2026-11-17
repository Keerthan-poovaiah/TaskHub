using TaskHub.Infrastructure;
using TaskHub.Scheduler;

// Load the .env file BEFORE anything reads settings
DotEnvLoader.Load();

var builder = Host.CreateApplicationBuilder(args);

string redisConnection = builder.Configuration["REDIS_CONNECTION"] ?? "localhost:6379";
string postgresConnection = builder.Configuration["POSTGRES_CONNECTION"]
    ?? throw new InvalidOperationException(
        "POSTGRES_CONNECTION is not set. Copy .env.example to .env and fill it in.");

// Hand out the tools: the queue (Redis) and the ledger (database)
builder.Services.AddTaskHubRedis(redisConnection);
builder.Services.AddTaskHubDatabase(postgresConnection);

builder.Services.AddHostedService<SchedulerService>();

builder.Build().Run();