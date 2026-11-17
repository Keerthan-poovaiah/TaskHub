using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using TaskHub.Core;

namespace TaskHub.Worker;

// Sends a real email through the Resend API
public class EmailJobHandler
{
    private readonly HttpClient _http;
    private readonly string _apiKey;
    private readonly string _from;
    private readonly ILogger<EmailJobHandler> _logger;

    public EmailJobHandler(HttpClient http, IConfiguration config, ILogger<EmailJobHandler> logger)
    {
        _http = http;
        _logger = logger;

        // Secrets come from environment variables, never from the code
        _apiKey = config["RESEND_API_KEY"] ?? "";
        _from = config["EMAIL_FROM"] ?? "TaskHub <onboarding@resend.dev>";
    }

    public async Task SendAsync(TaskItem task, int runId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(_apiKey))
            throw new InvalidOperationException("RESEND_API_KEY is not set on the worker.");

        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.resend.com/emails");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);

        // The idempotency key: the same run never sends the email twice,
        // even if this code runs again after a crash or a retry
        request.Headers.Add("Idempotency-Key", $"taskhub-run-{runId}");

        // HtmlEncode stops a task name like "<script>" from becoming real HTML
        string safeName = WebUtility.HtmlEncode(task.Name);

        request.Content = JsonContent.Create(new
        {
            from = _from,
            to = new[] { task.Target },
            subject = $"TaskHub: {task.Name}",
            html = $"<p>Hello from TaskHub.</p><p>Your task <strong>{safeName}</strong> ran at {DateTime.UtcNow:u} (UTC).</p>"
        });

        using HttpResponseMessage response = await _http.SendAsync(request, ct);

        if (!response.IsSuccessStatusCode)
        {
            string body = await response.Content.ReadAsStringAsync(ct);
            throw new Exception($"Email service returned {(int)response.StatusCode}: {body}");
        }

        _logger.LogInformation("EMAIL SENT to {Target} for run {RunId}", task.Target, runId);
    }
}