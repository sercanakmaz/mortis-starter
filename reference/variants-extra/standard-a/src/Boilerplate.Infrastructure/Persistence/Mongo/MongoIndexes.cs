using MongoDB.Bson;
using MongoDB.Driver;

namespace Boilerplate.Infrastructure.Persistence.Mongo;

public class MongoIndexes(IMongoDatabase database)
{
    public async Task EnsureAsync()
    {
        await UniqueAsync("categories", "name");
        await UniqueAsync("customers", "email");
        await UniqueAsync("orders", "orderNumber");
        await UniqueAsync("products", "sku");
        await database.GetCollection<BsonDocument>(PersistenceConstants.OutboxCollection).Indexes.CreateOneAsync(
            new CreateIndexModel<BsonDocument>(Builders<BsonDocument>.IndexKeys.Ascending("eventId").Ascending("nextAttemptAt")));
    }

    private Task UniqueAsync(string collectionName, params string[] fields)
    {
        var keys = Builders<BsonDocument>.IndexKeys.Combine(fields.Select(field => Builders<BsonDocument>.IndexKeys.Ascending(field)));

        return database.GetCollection<BsonDocument>(collectionName).Indexes.CreateOneAsync(
            new CreateIndexModel<BsonDocument>(keys, new CreateIndexOptions { Unique = true, Name = $"ux_{collectionName}_{string.Join("_", fields)}" }));
    }
}
