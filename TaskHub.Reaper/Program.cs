using TaskHub.Infrastructure;
using TaskHub.Reaper;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddTaskHubRedis("localhost:6379");
builder.Services.AddHostedService<ReaperService>();

builder.Build().Run();