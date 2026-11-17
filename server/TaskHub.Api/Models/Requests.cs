using TaskHub.Core;

namespace TaskHub.Api.Models;

// The JSON a client sends to sign up or log in
public record AuthRequest(string? Email, string? Password);

// The JSON a client sends to create a task ("Cron" is optional)
public record CreateTaskRequest(string Name, TaskType Type, string Target, string? Cron = null);