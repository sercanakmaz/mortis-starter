using Boilerplate.Infrastructure.Messaging.Outbox;
using Boilerplate.Infrastructure.Persistence;
using Boilerplate.IntegrationTests.TestSupport;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Boilerplate.IntegrationTests.Messaging;

[Collection(IntegrationConstants.PostgresCollection)]
public class OutboxClaimTests(PostgresFixture postgresFixture)
{
    [SkippableFact]
    public async Task ClaimNextAsync_InTwoConcurrentTransactions_Should_ClaimDifferentRows()
    {
        Skip.If(postgresFixture.Unavailable is not null, postgresFixture.Unavailable);
        await using var serviceProvider = IntegrationServices.Create(await postgresFixture.CreateDatabaseAsync(nameof(OutboxClaimTests)));
        using var first = serviceProvider.CreateScope();
        using var second = serviceProvider.CreateScope();
        var firstContext = first.ServiceProvider.GetRequiredService<AppDbContext>();
        var secondContext = second.ServiceProvider.GetRequiredService<AppDbContext>();
        await firstContext.Database.ExecuteSqlRawAsync(OutboxRows.TwoPending);

        await using var firstTransaction = await firstContext.Database.BeginTransactionAsync();
        await using var secondTransaction = await secondContext.Database.BeginTransactionAsync();
        var firstClaim = await first.ServiceProvider.GetRequiredService<OutboxRepository>().ClaimNextAsync();
        var secondClaim = await second.ServiceProvider.GetRequiredService<OutboxRepository>().ClaimNextAsync();

        Assert.NotNull(firstClaim);
        Assert.NotNull(secondClaim);
        Assert.NotEqual(firstClaim.Id, secondClaim.Id);
    }
}
