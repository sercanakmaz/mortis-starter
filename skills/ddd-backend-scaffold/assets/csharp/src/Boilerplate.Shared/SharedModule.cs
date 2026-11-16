using Microsoft.Extensions.DependencyInjection;
using Boilerplate.Shared.Application;
using Boilerplate.Shared.Domain;

namespace Boilerplate.Shared;

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
