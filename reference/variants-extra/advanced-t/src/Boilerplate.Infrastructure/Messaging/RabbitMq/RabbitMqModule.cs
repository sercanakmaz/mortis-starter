using Microsoft.Extensions.DependencyInjection;

namespace Boilerplate.Infrastructure.Messaging.RabbitMq;

public static class RabbitMqModule
{
    public static IServiceCollection AddRabbitMqModule(this IServiceCollection services)
    {
        services.AddSingleton<RabbitMqConnection>();
        services.AddSingleton<RabbitMqPublisher>();

        return services;
    }
}
