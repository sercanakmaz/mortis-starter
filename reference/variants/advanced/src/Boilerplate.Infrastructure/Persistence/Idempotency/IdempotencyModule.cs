using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Boilerplate.Infrastructure.Persistence.Idempotency;

public static class IdempotencyModule
{
    public static IServiceCollection AddIdempotency(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<IdempotencySettings>(configuration.GetSection(IdempotencySettings.Section));
        services.AddSingleton<IdempotencyStore>();
        services.AddHostedService<IdempotencyCleanupService>();

        return services;
    }
}
