using Acme.Shared.Application;
using Acme.Shared.Domain;
using Microsoft.Extensions.DependencyInjection;

namespace Acme.Shared;

public static class SharedModule
{
    public static IServiceCollection AddSharedModule(this IServiceCollection services)
    {
        services.AddScoped<EventContext>();
        services.AddScoped<EventHandlerExecutor>();
        services.AddScoped(typeof(CommandPipeline<>));
        services.AddScoped<ICommandDispatcher, CommandDispatcher>();

        return services;
    }
}
