using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using TaskHub.Api.Extensions;
using TaskHub.Api.Models;
using TaskHub.Api.Validation;
using TaskHub.Core;
using TaskHub.Infrastructure;

namespace TaskHub.Api.Endpoints;

// Everything about tasks. Every door here needs a valid ticket.
public static class TaskEndpoints
{
    public static void MapTaskEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/tasks").RequireAuthorization();

        group.MapPost("/", Create);
        group.MapGet("/", List);
        group.MapPost("/{id:int}/run", RunNow);
        group.MapPost("/{id:int}/pause", Pause);
        group.MapPost("/{id:int}/resume", Resume);
    }

    // Create a task.
    //  - With a "cron" schedule: the scheduler runs it whenever it is due.
    //  - Without one: it runs once, right away.
    private static async Task<IResult> Create(
        CreateTaskRequest request, ClaimsPrincipal principal, TaskHubDbContext db, IJobQueue queue)
    {
        if (principal.GetUserId() is not int userId)
            return Results.Unauthorized();

        if (string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.Target))
            return Results.BadRequest("Name and target are required.");

        string target = request.Target.Trim();

        // Reject bad targets now, instead of retrying a job that can never work
        if (request.Type == TaskType.SendEmail && !InputValidator.IsValidEmail(target))
            return Results.BadRequest("For SendEmail, the target must be a valid email address.");

        if (request.Type == TaskType.CheckUrl && !InputValidator.IsValidHttpUrl(target))
            return Results.BadRequest("For CheckUrl, the target must be a valid http or https URL.");

        bool isScheduled = !string.IsNullOrWhiteSpace(request.Cron);
        DateTime nextRun = default;

        if (isScheduled && !CronSchedule.TryGetNext(request.Cron!.Trim(), DateTime.UtcNow, out nextRun))
            return Results.BadRequest("Invalid cron expression. Example: 0 8 * * MON (times are in UTC).");

        var task = new TaskItem
        {
            UserId = userId,
            Name = request.Name.Trim(),
            Type = request.Type,
            Target = target
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
    }

    // List MY tasks, newest first
    private static async Task<IResult> List(ClaimsPrincipal principal, TaskHubDbContext db)
    {
        if (principal.GetUserId() is not int userId)
            return Results.Unauthorized();

        var tasks = await db.Tasks
            .Where(t => t.UserId == userId)
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
            .ToListAsync();

        return Results.Ok(tasks);
    }

    // Run one of MY tasks again, right now
    private static async Task<IResult> RunNow(
        int id, ClaimsPrincipal principal, TaskHubDbContext db, IJobQueue queue)
    {
        if (principal.GetUserId() is not int userId)
            return Results.Unauthorized();

        // The ownership check: "this task id AND it belongs to me".
        // Someone else's task answers "not found", so its existence isn't revealed.
        bool mine = await db.Tasks.AnyAsync(t => t.Id == id && t.UserId == userId);
        if (!mine)
            return Results.NotFound();

        var run = new JobRun { TaskItemId = id };
        db.Runs.Add(run);
        await db.SaveChangesAsync();

        await queue.EnqueueAsync(run.Id.ToString());

        return Results.Accepted($"/runs/{run.Id}", new { runId = run.Id });
    }

    // Pause one of MY scheduled tasks
    private static async Task<IResult> Pause(int id, ClaimsPrincipal principal, TaskHubDbContext db)
    {
        if (principal.GetUserId() is not int userId)
            return Results.Unauthorized();

        TaskItem? task = await db.Tasks.FirstOrDefaultAsync(t => t.Id == id && t.UserId == userId);
        if (task is null)
            return Results.NotFound();

        task.IsActive = false;
        await db.SaveChangesAsync();

        return Results.Ok(new { taskId = id, isActive = false });
    }

    // Resume one of MY paused tasks. Its next run is counted from now.
    private static async Task<IResult> Resume(int id, ClaimsPrincipal principal, TaskHubDbContext db)
    {
        if (principal.GetUserId() is not int userId)
            return Results.Unauthorized();

        TaskItem? task = await db.Tasks.FirstOrDefaultAsync(t => t.Id == id && t.UserId == userId);
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
    }
}