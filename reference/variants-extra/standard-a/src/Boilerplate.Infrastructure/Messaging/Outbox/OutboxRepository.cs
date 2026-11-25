using Boilerplate.Infrastructure.Persistence;
using Boilerplate.Infrastructure.Persistence.Mongo;
using Boilerplate.Shared.Domain;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Boilerplate.Infrastructure.Messaging.Outbox;

public class OutboxRepository(IMongoDatabase database, MongoSession mongoSession, EventConsumerRegistry eventConsumerRegistry)
{
    private readonly IMongoCollection<OutboxMessage> _messages = database.GetCollection<OutboxMessage>(PersistenceConstants.OutboxCollection);

    public Task AddAsync(IEnumerable<EventBase> events)
    {
        var outboxMessages = events
            .SelectMany(@event => eventConsumerRegistry.For(@event.GetType()).Select(eventConsumerEndpoint => OutboxMessage.From(@event, eventConsumerEndpoint)))
            .ToList();

        return outboxMessages.Count == 0 ? Task.CompletedTask : _messages.InsertManyAsync(mongoSession.Handle, outboxMessages);
    }

    public Task<OutboxMessage> ClaimNextAsync()
    {
        return ClaimAsync(Builders<OutboxMessage>.Filter.Empty);
    }

    public Task<OutboxMessage> ClaimAsync(Guid eventId)
    {
        return ClaimAsync(Builders<OutboxMessage>.Filter.Eq(message => message.EventId, eventId) & Builders<OutboxMessage>.Filter.Eq(message => message.Attempts, 0));
    }

    public Task DeleteAsync(OutboxMessage message)
    {
        return _messages.DeleteOneAsync(mongoSession.Handle, Builders<OutboxMessage>.Filter.Eq(candidate => candidate.Id, message.Id));
    }

    public Task RecordFailureAsync(OutboxMessage message, Exception exception)
    {
        return _messages.UpdateOneAsync(
            Builders<OutboxMessage>.Filter.Eq(candidate => candidate.Id, message.Id),
            Builders<OutboxMessage>.Update
                .Inc(candidate => candidate.Attempts, 1)
                .Set(candidate => candidate.NextAttemptAt, DateTime.UtcNow.AddSeconds(Math.Pow(2, message.Attempts + 1)))
                .Set(candidate => candidate.LastError, exception.Message));
    }

    private Task<OutboxMessage> ClaimAsync(FilterDefinition<OutboxMessage> filter)
    {
        var due = new BsonDocumentFilterDefinition<OutboxMessage>(new BsonDocument("$expr", new BsonDocument("$lt", new BsonArray { "$attempts", "$maxAttempts" })))
            & Builders<OutboxMessage>.Filter.Lte(message => message.NextAttemptAt, DateTime.UtcNow);

        return _messages.FindOneAndUpdateAsync(
            mongoSession.Handle,
            filter & due,
            Builders<OutboxMessage>.Update.Set(message => message.ClaimedAt, DateTime.UtcNow),
            new FindOneAndUpdateOptions<OutboxMessage> { Sort = Builders<OutboxMessage>.Sort.Ascending(message => message.NextAttemptAt) });
    }
}
