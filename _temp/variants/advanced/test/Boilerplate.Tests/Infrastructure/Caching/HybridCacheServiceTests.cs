using Boilerplate.Infrastructure.Caching;
using Boilerplate.Shared.Application;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Boilerplate.Tests.Infrastructure.Caching;

public class HybridCacheServiceTests
{
    [Fact]
    public async Task GetOrCreateAsync_TwiceForOneKey_Should_LoadOnce()
    {
        await using var serviceProvider = new ServiceCollection().AddCaching(new ConfigurationBuilder().Build()).BuildServiceProvider();
        var cacheService = serviceProvider.GetRequiredService<ICacheService>();
        var loads = 0;

        await cacheService.GetOrCreateAsync("products:t:1:en", ["product:t:1"], _ => Task.FromResult(++loads));
        var second = await cacheService.GetOrCreateAsync("products:t:1:en", ["product:t:1"], _ => Task.FromResult(++loads));

        Assert.Equal((1, 1), (second, loads));
    }

    [Fact]
    public async Task RemoveByTagAsync_Should_MakeEveryKeyWithTheTagLoadAgain()
    {
        await using var serviceProvider = new ServiceCollection().AddCaching(new ConfigurationBuilder().Build()).BuildServiceProvider();
        var cacheService = serviceProvider.GetRequiredService<ICacheService>();
        var loads = 0;
        await cacheService.GetOrCreateAsync("products:t:1:en", ["product:t:1"], _ => Task.FromResult(++loads));
        await cacheService.GetOrCreateAsync("products:t:1:tr", ["product:t:1"], _ => Task.FromResult(++loads));

        await cacheService.RemoveByTagAsync("product:t:1");

        await cacheService.GetOrCreateAsync("products:t:1:en", ["product:t:1"], _ => Task.FromResult(++loads));
        await cacheService.GetOrCreateAsync("products:t:1:tr", ["product:t:1"], _ => Task.FromResult(++loads));
        Assert.Equal(4, loads);
    }
}
