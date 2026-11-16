using Microsoft.Extensions.DependencyInjection;
using Boilerplate.Shared.Application;

namespace Boilerplate.Console.Scenarios;

public static class CommandScenarioExtensions
{
    public static IServiceCollection AddCommandScenario<TCommand, TContent>(this IServiceCollection services)
        where TCommand : ICommand<TContent>
    {
        services.AddScoped<IScenario, CommandScenario<TCommand, TContent>>();

        return services;
    }
}
