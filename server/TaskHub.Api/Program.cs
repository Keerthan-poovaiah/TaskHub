using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using TaskHub.Api.Auth;
using TaskHub.Api.Endpoints;
using TaskHub.Core;
using TaskHub.Infrastructure;

// Load the .env file BEFORE anything reads settings
DotEnvLoader.Load();

var builder = WebApplication.CreateBuilder(args);

// ---------- Settings (from .env or the real environment) ----------

string redisConnection = builder.Configuration["REDIS_CONNECTION"] ?? "localhost:6379";
string postgresConnection = builder.Configuration["POSTGRES_CONNECTION"]
    ?? throw new InvalidOperationException(
        "POSTGRES_CONNECTION is not set. Copy .env.example to .env and fill it in.");

// The one website address we trust to call this API from a browser
string uiOrigin = builder.Configuration["UI_ORIGIN"] ?? "http://localhost:5173";

// Throws a clear error right here if the secret is missing or too short
var tokenService = new TokenService(builder.Configuration["JWT_SECRET"] ?? "");

// ---------- Tools ----------

builder.Services.AddTaskHubRedis(redisConnection);
builder.Services.AddTaskHubDatabase(postgresConnection);
builder.Services.AddSingleton(tokenService);
builder.Services.AddSingleton<IPasswordHasher<User>, PasswordHasher<User>>();

// CORS allow-list. Only pages from uiOrigin may read our answers.
builder.Services.AddCors(options =>
    options.AddPolicy("ui", policy => policy
        .WithOrigins(uiOrigin)
        .AllowAnyHeader()
        .AllowAnyMethod()));

// Show enums as text in JSON ("SendEmail") instead of numbers
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

// "Check the stamp on every request that carries a ticket"
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;   // keep the claim names exactly as we wrote them ("sub")
        options.TokenValidationParameters = tokenService.ValidationParameters;
    });
builder.Services.AddAuthorization();

// ---------- The app ----------

var app = builder.Build();

app.UseCors("ui");   // must come BEFORE authentication
app.UseAuthentication();
app.UseAuthorization();

app.MapAuthEndpoints();
app.MapTaskEndpoints();
app.MapRunEndpoints();

app.Run();