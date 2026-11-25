using Acme.Infrastructure.Authentication;
using Acme.Infrastructure.Logging;
using Acme.Infrastructure.Messaging;
using Acme.Infrastructure.Messaging.Outbox;
using Acme.Infrastructure.Persistence;
using Acme.Shared.Application;
using Acme.Shared.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Acme.Infrastructure;

public static class InfrastructureModule
{
    public static IServiceCollection AddInfrastructureModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<AppDbContext>(options => options
            .UseNpgsql(configuration.GetConnectionString(PersistenceConstants.ConnectionStringName))
            .UseSnakeCaseNamingConvention());

        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<ICommandHandlerMiddleware, LoggingCommandHandlerMiddleware>();
        services.Configure<AuthSettings>(configuration.GetSection(AuthSettings.Section));
        services.AddOutboxModule(configuration);
        services.AddPersistenceModule();
        services.AddEventConsumerRegistry();

        return services;
    }
}
