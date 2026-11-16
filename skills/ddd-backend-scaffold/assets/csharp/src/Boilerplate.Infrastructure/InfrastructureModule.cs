using Microsoft.Extensions.DependencyInjection;
using Boilerplate.Infrastructure.Persistence;
using Microsoft.Extensions.Configuration;
using Boilerplate.Infrastructure.Messaging;
using Boilerplate.Infrastructure.Logging;
using Boilerplate.Shared.Application;
using Boilerplate.Shared.Persistence;
using Microsoft.EntityFrameworkCore;
using Boilerplate.Shared.Domain;

namespace Boilerplate.Infrastructure;

public static class InfrastructureModule
{
    public static IServiceCollection AddInfrastructureModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<AppDbContext>(options => options
            .UseNpgsql(configuration.GetConnectionString(PersistenceConstants.ConnectionStringName))
            .UseSnakeCaseNamingConvention());

        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<ICommandHandlerMiddleware, LoggingCommandHandlerMiddleware>();
        services.AddScoped<IEventDispatcher, LoggingEventDispatcher>();
        services.AddScoped<IEventDispatcher, AsyncEventHandlerDispatcher>();
        services.AddPersistenceModule();

        return services;
    }
}
