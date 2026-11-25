using Boilerplate.Infrastructure.Secrets;
using Boilerplate.Infrastructure.Caching;
using Boilerplate.Infrastructure.ReadModels;
using Microsoft.Extensions.DependencyInjection;
using Boilerplate.Infrastructure.Persistence;
using Microsoft.Extensions.Configuration;
using Boilerplate.Infrastructure.Messaging.Outbox;
using Boilerplate.Infrastructure.Messaging.RabbitMq;
using Boilerplate.Infrastructure.Logging;
using Boilerplate.Infrastructure.Authentication;
using Boilerplate.Infrastructure.Tenancy;
using Boilerplate.Infrastructure.Localization;
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
        services.AddScoped<ICommandHandlerMiddleware, TenantCommandHandlerMiddleware>();
        services.AddScoped<TenantScope>();
        services.AddSingleton<MessageLocalizer>();
        services.Configure<AuthSettings>(configuration.GetSection(AuthSettings.Section));
        services.AddOutboxModule(configuration);
        services.AddCaching(configuration);
        services.AddSecrets(configuration);
        services.AddRabbitMqModule();
        services.AddPersistenceModule();
        services.AddReadModelModule();

        return services;
    }
}
