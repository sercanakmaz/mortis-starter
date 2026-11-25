using Boilerplate.Infrastructure.Persistence.Idempotency;
using Boilerplate.IntegrationTests.TestSupport;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Boilerplate.IntegrationTests.Persistence;

[Collection(IntegrationConstants.PostgresCollection)]
public class IdempotencyStoreTests(PostgresFixture postgresFixture)
{
    [SkippableFact]
    public async Task StartAsync_TwiceWithTheSameKey_Should_OwnTheKeyThenReturnTheRecordInProgress()
    {
        Skip.If(postgresFixture.Unavailable is not null, postgresFixture.Unavailable);
        await using var serviceProvider = IntegrationServices.Create(await postgresFixture.CreateDatabaseAsync(nameof(IdempotencyStoreTests)));
        var idempotencyStore = new IdempotencyStore(serviceProvider.GetRequiredService<IServiceScopeFactory>(), Options.Create(new IdempotencySettings()));

        var first = await idempotencyStore.StartAsync("tenant|user", "k-1", "hash");
        var second = await idempotencyStore.StartAsync("tenant|user", "k-1", "hash");

        Assert.Null(first);
        Assert.Equal(("hash", null), (second.RequestHash, second.StatusCode));
    }

    [SkippableFact]
    public async Task StartAsync_WithARecordInProgressOlderThanTheTimeout_Should_TakeItOver()
    {
        Skip.If(postgresFixture.Unavailable is not null, postgresFixture.Unavailable);
        await using var serviceProvider = IntegrationServices.Create(await postgresFixture.CreateDatabaseAsync(nameof(IdempotencyStoreTests)));
        var idempotencyStore = new IdempotencyStore(serviceProvider.GetRequiredService<IServiceScopeFactory>(), Options.Create(new IdempotencySettings { InProgressTimeoutSeconds = -1 }));
        await idempotencyStore.StartAsync("tenant|user", "k-2", "hash");

        var takenOver = await idempotencyStore.StartAsync("tenant|user", "k-2", "hash");

        Assert.Null(takenOver);
    }

    [SkippableFact]
    public async Task FinishAsync_ThenDeleteExpiredAsync_Should_ReplayTheBodyAndDeleteOnlyExpiredRecords()
    {
        Skip.If(postgresFixture.Unavailable is not null, postgresFixture.Unavailable);
        await using var serviceProvider = IntegrationServices.Create(await postgresFixture.CreateDatabaseAsync(nameof(IdempotencyStoreTests)));
        var idempotencyStore = new IdempotencyStore(serviceProvider.GetRequiredService<IServiceScopeFactory>(), Options.Create(new IdempotencySettings()));
        await idempotencyStore.StartAsync("tenant|user", "k-3", "hash");
        await idempotencyStore.FinishAsync("tenant|user", "k-3", 201, "orders/1", """{"id":"1"}""");

        var deleted = await idempotencyStore.DeleteExpiredAsync();
        var finished = await idempotencyStore.StartAsync("tenant|user", "k-3", "hash");

        Assert.Equal(0, deleted);
        Assert.Equal((201, "orders/1"), (finished.StatusCode, finished.Location));
        Assert.Contains("\"id\"", finished.ResponseBody);
    }
}
