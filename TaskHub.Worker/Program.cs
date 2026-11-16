using TaskHub.Core;
using TaskHub.Infrastructure;
using TaskHub.Worker;

var builder = Host.CreateApplicationBuilder(args);

// Hand out the tools: the queue (Redis) and the ledger (database)
builder.Services.AddTaskHubRedis("localhost:6379");
builder.Services.AddTaskHubDatabase(
    "Host=localhost;Port=5433;Database=taskhub;Username=taskhub;Password=taskhub_dev");

builder.Services.AddSingleton<IJobHandler, FakeEmailHandler>();

// Register the background loop
builder.Services.AddHostedService<JobWorker>();

builder.Build().Run();