using TaskHub.Core;

namespace TaskHub.Background;

// One line per program: each executable (or the API, in single-process mode) picks the loops it wants
public static class BackgroundServiceCollectionExtensions
{
    // The worker: takes jobs and runs them
    public static IServiceCollection AddTaskHubWorker(this IServiceCollection services)
    {
        // One shared web client for all outgoing requests, with a 15-second timeout
        services.AddSingleton(new HttpClient { Timeout = TimeSpan.FromSeconds(15) });

        services.AddSingleton<EmailHandler>();
        services.AddSingleton<UrlCheckHandler>();
        services.AddSingleton<IJobHandler, TaskDispatcher>();

        services.AddHostedService<WorkerService>();
        return services;
    }

    // The reaper: rescues stuck jobs and releases due retries
    public static IServiceCollection AddTaskHubReaper(this IServiceCollection services)
    {
        services.AddHostedService<ReaperService>();
        return services;
    }

    // The scheduler (creates runs for scheduled tasks) and the sweeper (re-queues lost runs)
    public static IServiceCollection AddTaskHubScheduler(this IServiceCollection services)
    {
        services.AddHostedService<SchedulerService>();
        services.AddHostedService<SweeperService>();
        return services;
    }

    // Everything at once, for hosting that allows only one program
    public static IServiceCollection AddTaskHubAllBackground(this IServiceCollection services)
    {
        services.AddTaskHubWorker();
        services.AddTaskHubReaper();
        services.AddTaskHubScheduler();
        return services;
    }
}