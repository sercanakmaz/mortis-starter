using Microsoft.Extensions.DependencyInjection;

namespace Boilerplate.Shared.Application;

public static class CommandHandlerExtensions
{
    public static IServiceCollection AddCommandHandler<TCommand, TContent, TCommandHandler>(this IServiceCollection services)
        where TCommand : ICommand<TContent>
        where TCommandHandler : BaseCommandHandler<TCommand, TContent>
    {
        services.AddScoped<BaseCommandHandler<TCommand, TContent>, TCommandHandler>();

        return services;
    }
}
