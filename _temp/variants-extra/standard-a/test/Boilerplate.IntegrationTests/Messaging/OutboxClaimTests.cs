using Boilerplate.Infrastructure.Messaging.Outbox;
using Boilerplate.Infrastructure.Persistence;
using Boilerplate.IntegrationTests.TestSupport;
using Boilerplate.Shared.Persistence;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Boilerplate.IntegrationTests.Messaging;

[Collection(IntegrationConstants.MongoCollection)]
public class OutboxClaimTests(MongoFixture mongoFixture)
{
    [SkippableFact]
    public async Task ClaimNextAsync_InTwoConcurrentTransactions_Should_NeverClaimTheSameDocumentTwice()
    {
        Skip.If(mongoFixture.Unavailable is not null, mongoFixture.Unavailable);
        await using var serviceProvider = IntegrationServices.Create(mongoFixture.CreateDatabase(nameof(OutboxClaimTests)));
        var due = DateTime.UtcNow.AddMinutes(-1);
        await serviceProvider.GetRequiredService<IMongoDatabase>().GetCollection<BsonDocument>(PersistenceConstants.OutboxCollection).InsertManyAsync([
            new BsonDocument { ["_id"] = new BsonBinaryData(Guid.CreateVersion7(), GuidRepresentation.Standard), ["eventId"] = new BsonBinaryData(Guid.CreateVersion7(), GuidRepresentation.Standard), ["consumer"] = "probe", ["type"] = "probe", ["payload"] = "{}", ["occurredAt"] = due, ["attempts"] = 0, ["maxAttempts"] = 5, ["nextAttemptAt"] = due },
            new BsonDocument { ["_id"] = new BsonBinaryData(Guid.CreateVersion7(), GuidRepresentation.Standard), ["eventId"] = new BsonBinaryData(Guid.CreateVersion7(), GuidRepresentation.Standard), ["consumer"] = "probe", ["type"] = "probe", ["payload"] = "{}", ["occurredAt"] = due, ["attempts"] = 0, ["maxAttempts"] = 5, ["nextAttemptAt"] = due }]);
        using var first = serviceProvider.CreateScope();
        using var second = serviceProvider.CreateScope();
        await first.ServiceProvider.GetRequiredService<IUnitOfWork>().BeginTransactionAsync(nameof(OutboxClaimTests));
        await second.ServiceProvider.GetRequiredService<IUnitOfWork>().BeginTransactionAsync(nameof(OutboxClaimTests));

        var firstClaim = await first.ServiceProvider.GetRequiredService<OutboxRepository>().ClaimNextAsync();
        OutboxMessage secondClaim = null;
        var conflict = await Record.ExceptionAsync(async () => secondClaim = await second.ServiceProvider.GetRequiredService<OutboxRepository>().ClaimNextAsync());

        Assert.NotNull(firstClaim);
        Assert.True(conflict is MongoException mongoException ? mongoException.HasErrorLabel(OutboxConstants.TransientTransactionErrorLabel) : secondClaim.Id != firstClaim.Id);
    }
}
