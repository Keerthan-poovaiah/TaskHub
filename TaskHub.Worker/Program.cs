using TaskHub.Core;
using TaskHub.Infrastructure;
using TaskHub.Worker;

var builder = Host.CreateApplicationBuilder(args);

// Hand out the tools: the queue (Redis) and the ledger (database)
builder.Services.AddTaskHubRedis("localhost:6379");
builder.Services.AddTaskHubDatabase(
    "Host=localhost;Port=5433;Database=taskhub;Username=taskhub;Password=taskhub_dev");

// One shared web client for all outgoing requests, with a 15-second timeout
builder.Services.AddSingleton(new HttpClient { Timeout = TimeSpan.FromSeconds(15) });

// The two real handlers, and the dispatcher that chooses between them
builder.Services.AddSingleton<EmailJobHandler>();
builder.Services.AddSingleton<UrlCheckHandler>();
builder.Services.AddSingleton<IJobHandler, TaskDispatcher>();

// Register the background loop
builder.Services.AddHostedService<JobWorker>();

builder.Build().Run();