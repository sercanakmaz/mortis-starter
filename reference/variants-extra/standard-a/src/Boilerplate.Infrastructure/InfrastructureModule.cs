using Microsoft.Extensions.DependencyInjection;
using Boilerplate.Infrastructure.Persistence.Mongo;
using Boilerplate.Infrastructure.Messaging;
using Microsoft.Extensions.Configuration;
using Boilerplate.Infrastructure.Messaging.Outbox;
using Boilerplate.Infrastructure.Logging;
using Boilerplate.Infrastructure.Authentication;
using Boilerplate.Shared.Application;

namespace Boilerplate.Infrastructure;

public static class InfrastructureModule
{
    public static IServiceCollection AddInfrastructureModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddMongoModule(configuration);
        services.AddScoped<ICommandHandlerMiddleware, LoggingCommandHandlerMiddleware>();
        services.Configure<AuthSettings>(configuration.GetSection(AuthSettings.Section));
        services.AddOutboxModule(configuration);
        services.AddEventConsumerRegistry();

        return services;
    }
}
