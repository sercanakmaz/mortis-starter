using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Acme.Infrastructure.Messaging.Outbox;

public static class OutboxModule
{
    public static IServiceCollection AddOutboxModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<OutboxSettings>(configuration.GetSection(OutboxSettings.Section));
        services.AddScoped<OutboxRepository>();
        services.AddSingleton<OutboxWorker>();
        return services;
    }

    public static IServiceCollection AddOutboxWorker(this IServiceCollection services)
    {
        services.AddHostedService(serviceProvider => serviceProvider.GetRequiredService<OutboxWorker>());

        return services;
    }
}
