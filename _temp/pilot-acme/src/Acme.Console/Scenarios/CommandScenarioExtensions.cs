using Acme.Shared.Application;
using Microsoft.Extensions.DependencyInjection;

namespace Acme.Console.Scenarios;

public static class CommandScenarioExtensions
{
    public static IServiceCollection AddCommandScenario<TCommand, TContent>(this IServiceCollection services)
        where TCommand : ICommand<TContent>
    {
        services.AddScoped<IScenario, CommandScenario<TCommand, TContent>>();

        return services;
    }
}
