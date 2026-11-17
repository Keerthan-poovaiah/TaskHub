using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using TaskHub.Api.Extensions;
using TaskHub.Infrastructure;

namespace TaskHub.Api.Endpoints;

// The run history. Needs a valid ticket.
public static class RunEndpoints
{
    public static void MapRunEndpoints(this WebApplication app)
    {
        app.MapGet("/runs", List).RequireAuthorization();
    }

    // List the 50 most recent runs of MY tasks
    private static async Task<IResult> List(ClaimsPrincipal principal, TaskHubDbContext db)
    {
        if (principal.GetUserId() is not int userId)
            return Results.Unauthorized();

        var runs = await db.Runs
            .Where(r => r.TaskItem!.UserId == userId)
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
            .ToListAsync();

        return Results.Ok(runs);
    }
}