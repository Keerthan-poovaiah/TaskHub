using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;
using TaskHub.Core;

namespace TaskHub.Infrastructure;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddTaskHubRedis(
        this IServiceCollection services, string connectionString)
    {
        ConfigurationOptions options = ConnectionStrings.ParseRedis(connectionString);

        // Singleton = create it once and share it everywhere
        services.AddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(options));
        services.AddSingleton(new QueueOptions());
        services.AddSingleton<IJobQueue, RedisJobQueue>();
        return services;
    }

    public static IServiceCollection AddTaskHubDatabase(
        this IServiceCollection services, string connectionString)
    {
        string normalized = ConnectionStrings.NormalizePostgres(connectionString);

        services.AddDbContext<TaskHubDbContext>(options => options.UseNpgsql(normalized));

        // Scoped = a fresh copy for each unit of work
        services.AddScoped<IRunStore, EfRunStore>();
        return services;
    }
}