using System.Net.Mail;
using System.Security.Claims;
using System.Text;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using TaskHub.Core;
using TaskHub.Infrastructure;

const string TokenIssuer = "taskhub-api";
const string TokenAudience = "taskhub-clients";

// Load the .env file BEFORE anything reads settings
DotEnvLoader.Load();

var builder = WebApplication.CreateBuilder(args);

// Settings come from .env (or the real environment). Nothing secret is typed in the code.
string redisConnection = builder.Configuration["REDIS_CONNECTION"] ?? "localhost:6379";
string postgresConnection = builder.Configuration["POSTGRES_CONNECTION"]
    ?? throw new InvalidOperationException(
        "POSTGRES_CONNECTION is not set. Copy .env.example to .env and fill it in.");

// Hand out the tools: the Redis queue and the database
builder.Services.AddTaskHubRedis(redisConnection);
builder.Services.AddTaskHubDatabase(postgresConnection);

// Show enums as text in JSON ("SendEmail") instead of numbers
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

// The stamping tool. It comes from .env, never from the code.
string jwtSecret = builder.Configuration["JWT_SECRET"] ?? "";
if (jwtSecret.Length < 32)
    throw new InvalidOperationException(
        "JWT_SECRET is missing or too short. Put a random string of at least 32 characters in your .env file.");

var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret));

// The password scrambler (salted and deliberately slow, so guessing is expensive)
builder.Services.AddSingleton<IPasswordHasher<User>, PasswordHasher<User>>();

// "Check the stamp on every request that carries a ticket"
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;   // keep the claim names exactly as we wrote them ("sub")
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = TokenIssuer,
            ValidateAudience = true,
            ValidAudience = TokenAudience,
            ValidateLifetime = true,                 // reject expired tickets
            ValidateIssuerSigningKey = true,         // reject forged stamps
            IssuerSigningKey = signingKey,
            ClockSkew = TimeSpan.FromSeconds(30)
        };
    });
builder.Services.AddAuthorization();

var app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();

// ---------- Helpers ----------

// Makes a stamped ticket that says "this is user #N"
string CreateToken(User user)
{
    var descriptor = new SecurityTokenDescriptor
    {
        Subject = new ClaimsIdentity(new[]
        {
            new Claim("sub", user.Id.ToString()),
            new Claim("email", user.Email)
        }),
        Issuer = TokenIssuer,
        Audience = TokenAudience,
        Expires = DateTime.UtcNow.AddMinutes(60),
        SigningCredentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256)
    };

    return new JsonWebTokenHandler().CreateToken(descriptor);
}

// Reads "who is asking?" out of a ticket that has already passed the stamp check
static int? GetUserId(ClaimsPrincipal principal)
{
    string? value = principal.FindFirst("sub")?.Value;
    return int.TryParse(value, out int id) ? id : null;
}

static bool IsValidEmail(string value) =>
    value.Length <= 254
    && MailAddress.TryCreate(value, out MailAddress? parsed)
    && parsed.Address == value;

static bool IsValidHttpUrl(string value) =>
    Uri.TryCreate(value, UriKind.Absolute, out Uri? uri)
    && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);

// ---------- Open doors (no ticket needed) ----------

app.MapPost("/auth/register", async (AuthRequest request, TaskHubDbContext db, IPasswordHasher<User> hasher) =>
{
    string email = (request.Email ?? "").Trim().ToLowerInvariant();
    string password = request.Password ?? "";

    if (!IsValidEmail(email))
        return Results.BadRequest("Enter a valid email address.");

    if (password.Length < 8 || password.Length > 100)
        return Results.BadRequest("Password must be between 8 and 100 characters.");

    if (await db.Users.AnyAsync(u => u.Email == email))
        return Results.Conflict("That email is already registered.");

    var newUser = new User { Email = email };
    newUser.PasswordHash = hasher.HashPassword(newUser, password);   // store the scramble only

    db.Users.Add(newUser);

    try
    {
        await db.SaveChangesAsync();
    }
    catch (DbUpdateException)
    {
        // Two sign-ups with the same email at the same instant: the unique index stopped one
        return Results.Conflict("That email is already registered.");
    }

    return Results.Created($"/users/{newUser.Id}", new { id = newUser.Id, email = newUser.Email });
});

app.MapPost("/auth/login", async (AuthRequest request, TaskHubDbContext db, IPasswordHasher<User> hasher) =>
{
    string email = (request.Email ?? "").Trim().ToLowerInvariant();
    User? found = await db.Users.FirstOrDefaultAsync(u => u.Email == email);

    // Same message for "no such user" and "wrong password", so an attacker
    // can't use this door to find out which emails are registered
    if (found is null
        || hasher.VerifyHashedPassword(found, found.PasswordHash, request.Password ?? "")
            == PasswordVerificationResult.Failed)
    {
        return Results.Json(new { error = "Invalid email or password." }, statusCode: 401);
    }

    return Results.Ok(new { token = CreateToken(found), expiresInMinutes = 60 });
});

// ---------- Doors that need a ticket ----------

var secured = app.MapGroup("").RequireAuthorization();

// Create a task.
//  - With a "cron" schedule: the scheduler runs it whenever it is due.
//  - Without one: it runs once, right away.
secured.MapPost("/tasks", async (
    CreateTaskRequest request, ClaimsPrincipal principal, TaskHubDbContext db, IJobQueue queue) =>
{
    if (GetUserId(principal) is not int userId)
        return Results.Unauthorized();

    if (string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.Target))
        return Results.BadRequest("Name and target are required.");

    string target = request.Target.Trim();

    // Reject bad targets now, instead of retrying a job that can never work
    if (request.Type == TaskType.SendEmail && !IsValidEmail(target))
        return Results.BadRequest("For SendEmail, the target must be a valid email address.");

    if (request.Type == TaskType.CheckUrl && !IsValidHttpUrl(target))
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
});

// Run one of MY tasks again, right now
secured.MapPost("/tasks/{id:int}/run", async (
    int id, ClaimsPrincipal principal, TaskHubDbContext db, IJobQueue queue) =>
{
    if (GetUserId(principal) is not int userId)
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
});

// Pause one of MY scheduled tasks
secured.MapPost("/tasks/{id:int}/pause", async (int id, ClaimsPrincipal principal, TaskHubDbContext db) =>
{
    if (GetUserId(principal) is not int userId)
        return Results.Unauthorized();

    TaskItem? task = await db.Tasks.FirstOrDefaultAsync(t => t.Id == id && t.UserId == userId);
    if (task is null)
        return Results.NotFound();

    task.IsActive = false;
    await db.SaveChangesAsync();

    return Results.Ok(new { taskId = id, isActive = false });
});

// Resume one of MY paused tasks. Its next run is counted from now.
secured.MapPost("/tasks/{id:int}/resume", async (int id, ClaimsPrincipal principal, TaskHubDbContext db) =>
{
    if (GetUserId(principal) is not int userId)
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
});

// List MY tasks, newest first
secured.MapGet("/tasks", async (ClaimsPrincipal principal, TaskHubDbContext db) =>
{
    if (GetUserId(principal) is not int userId)
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
});

// List the 50 most recent runs of MY tasks
secured.MapGet("/runs", async (ClaimsPrincipal principal, TaskHubDbContext db) =>
{
    if (GetUserId(principal) is not int userId)
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
});

app.Run();

// The shape of the JSON a client sends to sign up or log in
public record AuthRequest(string? Email, string? Password);

// The shape of the JSON a client sends to create a task ("cron" is optional)
public record CreateTaskRequest(string Name, TaskType Type, string Target, string? Cron = null);