using Boilerplate.Shared.Domain;
using Boilerplate.Shared.Exceptions;
using Boilerplate.Shared.Persistence;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;

namespace Boilerplate.Infrastructure.Persistence.Mongo;

public abstract class MongoRepository<TAggregate, TKey>(IMongoDatabase database, MongoUnitOfWork unitOfWork, EventContext eventContext, string collectionName) : IRepository<TAggregate, TKey> where TAggregate : AggregateRoot<TKey>
{
    private readonly IMongoCollection<BsonDocument> _documents = database.GetCollection<BsonDocument>(collectionName);

    public virtual async Task CreateAsync(TAggregate aggregate)
    {
        EnsureTransaction();

        await AssignIntegerKeyAsync(aggregate);

        var document = aggregate.ToBsonDocument();

        document[PersistenceConstants.VersionElement] = 1L;

        await MongoExceptionHandler.Run(() => _documents.InsertOneAsync(unitOfWork.Session, document));

        unitOfWork.Remember(aggregate, 1);
        RaiseEvents(aggregate);
    }

    public virtual async Task UpdateAsync(TAggregate aggregate)
    {
        EnsureTransaction();

        var version = unitOfWork.VersionOf(aggregate);
        var document = aggregate.ToBsonDocument();

        document[PersistenceConstants.VersionElement] = version + 1;

        var replaceOneResult = await MongoExceptionHandler.Run(() => _documents.ReplaceOneAsync(unitOfWork.Session, ById(aggregate.Id) & AtVersion(version), document));

        if (replaceOneResult.MatchedCount == 0)
        {
            throw new ConcurrencyConflictException(typeof(TAggregate).Name);
        }

        unitOfWork.Remember(aggregate, version + 1);
        RaiseEvents(aggregate);
    }

    public virtual async Task DeleteAsync(TAggregate aggregate)
    {
        EnsureTransaction();

        var deleteResult = await _documents.DeleteOneAsync(unitOfWork.Session, ById(aggregate.Id) & AtVersion(unitOfWork.VersionOf(aggregate)));

        if (deleteResult.DeletedCount == 0)
        {
            throw new ConcurrencyConflictException(typeof(TAggregate).Name);
        }

        RaiseEvents(aggregate);
    }

    public virtual async Task<TAggregate> FindByIdAsync(TKey id)
    {
        var document = await Find(ById(id)).FirstOrDefaultAsync();

        return document is null ? null : BsonSerializer.Deserialize<TAggregate>(document);
    }

    public virtual Task<TAggregate> FindByIdTrackingAsync(TKey id)
    {
        return FindOneTrackingAsync(ById(id));
    }

    protected async Task<TAggregate> FindOneTrackingAsync(FilterDefinition<BsonDocument> filter)
    {
        var document = await Find(filter).FirstOrDefaultAsync();

        if (document is null) return null;

        var aggregate = BsonSerializer.Deserialize<TAggregate>(document);

        unitOfWork.Remember(aggregate, document[PersistenceConstants.VersionElement].ToInt64());

        return aggregate;
    }

    protected async Task<IReadOnlyList<TAggregate>> FindManyAsync(FilterDefinition<BsonDocument> filter)
    {
        var documents = await Find(filter).ToListAsync();

        return documents.Select(document => BsonSerializer.Deserialize<TAggregate>(document)).ToList();
    }

    protected IFindFluent<BsonDocument, BsonDocument> Find(FilterDefinition<BsonDocument> filter)
    {
        return unitOfWork.Session is null ? _documents.Find(filter) : _documents.Find(unitOfWork.Session, filter);
    }

    private async Task AssignIntegerKeyAsync(TAggregate aggregate)
    {
        if (typeof(TKey) != typeof(int) && typeof(TKey) != typeof(long) || !aggregate.Id.Equals(default(TKey))) return;

        var counter = await database.GetCollection<BsonDocument>(PersistenceConstants.CountersCollection).FindOneAndUpdateAsync(
            unitOfWork.Session,
            Builders<BsonDocument>.Filter.Eq("_id", collectionName),
            Builders<BsonDocument>.Update.Inc("value", 1L),
            new FindOneAndUpdateOptions<BsonDocument> { IsUpsert = true, ReturnDocument = ReturnDocument.After });

        BsonClassMap.LookupClassMap(typeof(TAggregate)).IdMemberMap.Setter(aggregate, Convert.ChangeType(counter["value"].ToInt64(), typeof(TKey)));
    }

    private static FilterDefinition<BsonDocument> ById(TKey id)
    {
        return Builders<BsonDocument>.Filter.Eq("_id", id is Guid guid ? new BsonBinaryData(guid, GuidRepresentation.Standard) : BsonValue.Create(id));
    }

    private static FilterDefinition<BsonDocument> AtVersion(long version)
    {
        return Builders<BsonDocument>.Filter.Eq(PersistenceConstants.VersionElement, version);
    }

    private void RaiseEvents(TAggregate aggregate)
    {
        foreach (var @event in aggregate.DomainEvents ?? [])
        {
            eventContext.AddRaised(@event);
        }

        aggregate.ClearEvents();
    }

    private void EnsureTransaction()
    {
        if (!unitOfWork.HasTransaction)
        {
            throw new TransactionRequiredException(typeof(TAggregate).Name);
        }
    }
}
