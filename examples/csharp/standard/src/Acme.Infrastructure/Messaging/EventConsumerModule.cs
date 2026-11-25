using Acme.Shared.Domain;
using Microsoft.Extensions.DependencyInjection;

namespace Acme.Infrastructure.Messaging;

public static class EventConsumerModule
{
    public static IServiceCollection AddEventConsumerRegistry(this IServiceCollection services)
    {
        RegistryOf(services);

        return services;
    }

    public static IServiceCollection AddEventConsumer<TConsumer>(this IServiceCollection services, Action<EventConsumerEndpoint> configure) where TConsumer : class, IEventConsumer
    {
        var eventConsumerEndpoint = new EventConsumerEndpoint(typeof(TConsumer));

        configure(eventConsumerEndpoint);

        RegistryOf(services).Endpoints.Add(eventConsumerEndpoint);
        services.AddScoped<TConsumer>();

        return services;
    }

    private static EventConsumerRegistry RegistryOf(IServiceCollection services)
    {
        var eventConsumerRegistry = services.Select(descriptor => descriptor.ImplementationInstance).OfType<EventConsumerRegistry>().FirstOrDefault();

        if (eventConsumerRegistry is null)
        {
            eventConsumerRegistry = new EventConsumerRegistry();
            services.AddSingleton(eventConsumerRegistry);
        }

        return eventConsumerRegistry;
    }
}
