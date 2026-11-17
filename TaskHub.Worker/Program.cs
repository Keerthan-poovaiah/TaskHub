using TaskHub.Core;
using TaskHub.Infrastructure;
using TaskHub.Worker;

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

// One shared web client for all outgoing requests, with a 15-second timeout
builder.Services.AddSingleton(new HttpClient { Timeout = TimeSpan.FromSeconds(15) });

// The two real handlers, and the dispatcher that chooses between them
builder.Services.AddSingleton<EmailJobHandler>();
builder.Services.AddSingleton<UrlCheckHandler>();
builder.Services.AddSingleton<IJobHandler, TaskDispatcher>();

// Register the background loop
builder.Services.AddHostedService<JobWorker>();

builder.Build().Run();