using Boilerplate.Shared.Application;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Boilerplate.Infrastructure.Scheduling;

public static class SchedulerModule
{
    public static IServiceCollection AddScheduler(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<SchedulerSettings>(configuration.GetSection(SchedulerSettings.Section));
        services.AddSingleton<RecurringJobRegistry>();
        services.AddHostedService<SchedulerService>();

        return services;
    }

    public static IServiceCollection AddRecurringJob<TJob>(this IServiceCollection services) where TJob : class, IRecurringJob
    {
        services.AddScoped<TJob>();
        services.AddSingleton(new RecurringJobEntry(TJob.Name, TJob.DefaultCron, typeof(TJob)));

        return services;
    }
}
