using System.Diagnostics;
using TaskHub.Core;

namespace TaskHub.Worker;

// Visits a website and fails if it does not answer properly
public class UrlCheckHandler
{
    private readonly HttpClient _http;
    private readonly ILogger<UrlCheckHandler> _logger;

    public UrlCheckHandler(HttpClient http, ILogger<UrlCheckHandler> logger)
    {
        _http = http;
        _logger = logger;
    }

    public async Task CheckAsync(TaskItem task, CancellationToken ct)
    {
        // Only accept real web addresses like https://example.com
        if (!Uri.TryCreate(task.Target, UriKind.Absolute, out Uri? uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            throw new InvalidOperationException($"'{task.Target}' is not a valid http(s) URL.");
        }

        var timer = Stopwatch.StartNew();
        using HttpResponseMessage response =
            await _http.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, ct);
        timer.Stop();

        // Anything other than 200-299 counts as "down"
        if (!response.IsSuccessStatusCode)
            throw new Exception($"{uri} answered {(int)response.StatusCode} in {timer.ElapsedMilliseconds} ms");

        _logger.LogInformation("URL OK: {Url} answered {Code} in {Ms} ms",
            uri, (int)response.StatusCode, timer.ElapsedMilliseconds);
    }
}