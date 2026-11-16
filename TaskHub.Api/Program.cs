using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using TaskHub.Core;
using TaskHub.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// Hand out the tools: the Redis queue and the database
builder.Services.AddTaskHubRedis("localhost:6379");
builder.Services.AddTaskHubDatabase(
    "Host=localhost;Port=5433;Database=taskhub;Username=taskhub;Password=taskhub_dev");

// Show enums as text in JSON ("SendEmail") instead of numbers
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

var app = builder.Build();

// Create a task.
//  - With a "cron" schedule: the scheduler runs it whenever it is due.
//  - Without one: it runs once, right away.
app.MapPost("/tasks", async (CreateTaskRequest request, TaskHubDbContext db, IJobQueue queue) =>
{
    if (string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.Target))
        return Results.BadRequest("Name and target are required.");

    bool isScheduled = !string.IsNullOrWhiteSpace(request.Cron);
    DateTime nextRun = default;

    if (isScheduled && !CronSchedule.TryGetNext(request.Cron!.Trim(), DateTime.UtcNow, out nextRun))
        return Results.BadRequest("Invalid cron expression. Example: 0 8 * * MON (times are in UTC).");

    var task = new TaskItem
    {
        Name = request.Name.Trim(),
        Type = request.Type,
        Target = request.Target.Trim()
    };

    if (isScheduled)
    {
        task.CronExpression = request.Cron!.Trim();
        task.NextRunAtUtc = nextRun;

        db.Tasks.Add(task);
        await db.SaveChangesAsync();

        return Results.Created($"/tasks/{task.Id}", new { taskId = task.Id, nextRunAtUtc = task.NextRunAtUtc });
    }

    // One-off task: create a run and put it on the hook right now
    var run = new JobRun();
    task.Runs.Add(run);

    db.Tasks.Add(task);
    await db.SaveChangesAsync();          // run.Id now exists

    await queue.EnqueueAsync(run.Id.ToString());

    return Results.Created($"/tasks/{task.Id}", new { taskId = task.Id, runId = run.Id });
});

// Run an existing task again, right now
app.MapPost("/tasks/{id:int}/run", async (int id, TaskHubDbContext db, IJobQueue queue) =>
{
    bool exists = await db.Tasks.AnyAsync(t => t.Id == id);
    if (!exists)
        return Results.NotFound();

    var run = new JobRun { TaskItemId = id };
    db.Runs.Add(run);
    await db.SaveChangesAsync();

    await queue.EnqueueAsync(run.Id.ToString());

    return Results.Accepted($"/runs/{run.Id}", new { runId = run.Id });
});

// Pause a scheduled task (the scheduler skips paused tasks)
app.MapPost("/tasks/{id:int}/pause", async (int id, TaskHubDbContext db) =>
{
    TaskItem? task = await db.Tasks.FindAsync(id);
    if (task is null)
        return Results.NotFound();

    task.IsActive = false;
    await db.SaveChangesAsync();

    return Results.Ok(new { taskId = id, isActive = false });
});

// Resume a paused task. Its next run is counted from now.
app.MapPost("/tasks/{id:int}/resume", async (int id, TaskHubDbContext db) =>
{
    TaskItem? task = await db.Tasks.FindAsync(id);
    if (task is null)
        return Results.NotFound();

    if (task.CronExpression is null)
        return Results.BadRequest("This task has no schedule.");

    if (!CronSchedule.TryGetNext(task.CronExpression, DateTime.UtcNow, out DateTime next))
        return Results.BadRequest("The saved schedule is invalid.");

    task.IsActive = true;
    task.NextRunAtUtc = next;
    await db.SaveChangesAsync();

    return Results.Ok(new { taskId = id, isActive = true, nextRunAtUtc = next });
});

// List all tasks, newest first
app.MapGet("/tasks", async (TaskHubDbContext db) =>
    await db.Tasks
        .OrderByDescending(t => t.Id)
        .Select(t => new
        {
            t.Id,
            t.Name,
            t.Type,
            t.Target,
            t.CronExpression,
            t.NextRunAtUtc,
            t.IsActive,
            t.CreatedAtUtc,
            RunCount = t.Runs.Count
        })
        .ToListAsync());

// List the 50 most recent runs
app.MapGet("/runs", async (TaskHubDbContext db) =>
    await db.Runs
        .OrderByDescending(r => r.Id)
        .Take(50)
        .Select(r => new
        {
            r.Id,
            r.TaskItemId,
            TaskName = r.TaskItem!.Name,
            r.Status,
            r.Attempts,
            r.Error,
            r.CreatedAtUtc,
            r.FinishedAtUtc
        })
        .ToListAsync());

app.Run();

// The shape of the JSON a client sends to create a task ("cron" is optional)
public record CreateTaskRequest(string Name, TaskType Type, string Target, string? Cron = null);