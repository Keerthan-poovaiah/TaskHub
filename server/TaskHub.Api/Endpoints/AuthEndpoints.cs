using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using TaskHub.Api.Auth;
using TaskHub.Api.Models;
using TaskHub.Api.Validation;
using TaskHub.Core;
using TaskHub.Infrastructure;

namespace TaskHub.Api.Endpoints;

// The open doors: no ticket needed
public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this WebApplication app)
    {
        app.MapPost("/auth/register", Register);
        app.MapPost("/auth/login", Login);
    }

    private static async Task<IResult> Register(
        AuthRequest request, TaskHubDbContext db, IPasswordHasher<User> hasher)
    {
        string email = (request.Email ?? "").Trim().ToLowerInvariant();
        string password = request.Password ?? "";

        if (!InputValidator.IsValidEmail(email))
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
    }

    private static async Task<IResult> Login(
        AuthRequest request, TaskHubDbContext db, IPasswordHasher<User> hasher, TokenService tokens)
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

        return Results.Ok(new { token = tokens.CreateToken(found), expiresInMinutes = tokens.LifetimeMinutes });
    }
}