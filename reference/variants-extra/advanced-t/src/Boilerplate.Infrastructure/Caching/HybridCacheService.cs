using Boilerplate.Shared.Application;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Options;

namespace Boilerplate.Infrastructure.Caching;

public class HybridCacheService(HybridCache hybridCache, IOptions<CachingSettings> settings) : ICacheService
{
    public async Task<T> GetOrCreateAsync<T>(string key, IReadOnlyCollection<string> tags, Func<CancellationToken, Task<T>> load, CancellationToken cancellationToken = default)
    {
        var lifetime = TimeSpan.FromSeconds(settings.Value.Seconds);

        return await hybridCache.GetOrCreateAsync(key, async token => await load(token), new HybridCacheEntryOptions { Expiration = lifetime, LocalCacheExpiration = lifetime }, tags, cancellationToken);
    }

    public async Task RemoveByTagAsync(string tag, CancellationToken cancellationToken = default)
    {
        await hybridCache.RemoveByTagAsync(tag, cancellationToken);
    }
}
