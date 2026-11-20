using Microsoft.Extensions.DependencyInjection;
using Boilerplate.Infrastructure.Persistence;
using Microsoft.Extensions.Configuration;
using Boilerplate.Infrastructure.Messaging.Outbox;
using Boilerplate.Infrastructure.Logging;
using Boilerplate.Shared.Application;
using Boilerplate.Shared.Persistence;
using Microsoft.EntityFrameworkCore;

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
        services.AddOutboxModule(configuration);
        services.AddPersistenceModule();

        return services;
    }
}
