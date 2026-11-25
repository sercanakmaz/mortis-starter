using Boilerplate.Infrastructure.Messaging.RabbitMq;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Boilerplate.Shared.Domain;

namespace Boilerplate.Infrastructure.Messaging;

public static class EventConsumerModule
{
    public static IServiceCollection AddEventConsumer<TConsumer>(this IServiceCollection services, Action<EventConsumerEndpoint> configure) where TConsumer : class, IEventConsumer
    {
        var eventConsumerEndpoint = new EventConsumerEndpoint(typeof(TConsumer));

        configure(eventConsumerEndpoint);

        services.AddScoped<TConsumer>();
        services.AddSingleton<IHostedService>(serviceProvider => ActivatorUtilities.CreateInstance<ReceiveEndpointService>(serviceProvider, eventConsumerEndpoint));

        return services;
    }
}
