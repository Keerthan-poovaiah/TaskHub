using TaskHub.Core;
using TaskHub.Infrastructure;
using TaskHub.Worker;

var builder = Host.CreateApplicationBuilder(args);

// Hand out the tools
builder.Services.AddTaskHubRedis("localhost:6379");
builder.Services.AddSingleton<IJobHandler, FakeEmailHandler>();

// Register the background loop
builder.Services.AddHostedService<JobWorker>();

builder.Build().Run();