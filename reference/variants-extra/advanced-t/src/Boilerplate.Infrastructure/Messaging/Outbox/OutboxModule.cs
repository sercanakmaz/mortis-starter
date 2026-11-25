using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;

namespace Boilerplate.Infrastructure.Messaging.Outbox;

public static class OutboxModule
{
    public static IServiceCollection AddOutboxModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<OutboxSettings>(configuration.GetSection(OutboxSettings.Section));
        services.AddScoped<OutboxRepository>();
        return services;
    }

    public static IServiceCollection AddOutboxWorker(this IServiceCollection services)
    {
        services.AddHostedService<OutboxWorker>();

        return services;
    }
}
