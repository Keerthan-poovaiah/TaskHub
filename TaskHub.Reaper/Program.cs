using TaskHub.Infrastructure;
using TaskHub.Reaper;

// Load the .env file BEFORE anything reads settings
DotEnvLoader.Load();

var builder = Host.CreateApplicationBuilder(args);

// The Reaper only talks to Redis. It never touches the database.
string redisConnection = builder.Configuration["REDIS_CONNECTION"] ?? "localhost:6379";

builder.Services.AddTaskHubRedis(redisConnection);
builder.Services.AddHostedService<ReaperService>();

builder.Build().Run();