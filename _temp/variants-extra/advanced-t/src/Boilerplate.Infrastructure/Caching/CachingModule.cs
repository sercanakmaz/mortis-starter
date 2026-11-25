using Boilerplate.Shared.Application;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Boilerplate.Infrastructure.Caching;

public static class CachingModule
{
    public static IServiceCollection AddCaching(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<CachingSettings>(configuration.GetSection(CachingSettings.Section));
        services.AddHybridCache();

        if (configuration.GetConnectionString(CachingSettings.RedisConnectionStringName) is { Length: > 0 } redis)
        {
            services.AddStackExchangeRedisCache(options => options.Configuration = redis);
        }

        services.AddSingleton<ICacheService, HybridCacheService>();

        return services;
    }
}
