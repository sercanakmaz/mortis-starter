# Infrastructure on MongoDB, C#

Applies when `database` is `mongo`. Project `{Name}.Infrastructure`, folder `Persistence/Mongo/`. Layer 1: `options/database.md`, `architecture/documents.md`.

**Not built or run yet** (decision 45): every snippet on this page is written by hand against MongoDB.Driver 3.14.1 and the interfaces of the reference solutions; phase 2 compiles and proves it. `docs/unverified.md` lists it.

The Domain and Application projects are the same files as with PostgreSQL. Only Infrastructure, the hosts' settings, `docker-compose.yml` and the packages change. Everything on `infrastructure-postgres.md` (EF Core, `AppDbContext`, configurations, migrations, `EfRepository`, `DatabaseExceptionHandler`) is replaced by this page.

## How it fits together

```
an aggregate          one document; child entities and value objects embedded; references to other aggregates are ids
class maps            Persistence/Mongo/MongoClassMaps.cs: conventions + one map per aggregate that needs more; no driver attribute in the Domain
repository            MongoRepository<TAggregate, TKey> implements the same IRepository
  FindByIdTrackingAsync  reads the document, remembers its version in the unit of work
  UpdateAsync            replaces the document where _id = id AND version = the remembered one; nothing matched -> ConcurrencyConflictException
unit of work          a client session with a multi-document transaction (needs a replica set)
outbox                collection outbox_messages, written in the same session; claimed with findOneAndUpdate
indexes               MongoIndexes: ensured at start in Development, by the Console command ensure-indexes elsewhere
```

- The documents keep the element names camelCase (`sku`, `orderNumber`), enums as strings, `decimal` as `Decimal128`, `Guid` as a standard UUID (binary subtype 4).
- A document has two elements the aggregate does not: `version` (a number, 1 on insert, +1 on every replace) and, for integer keys, nothing more: the id comes from a counter.
- There is no foreign key. An order line may name a sku no product has; with PostgreSQL the database refuses it. When a rule needs the referenced aggregate to exist, the check belongs in a domain service, never in Infrastructure.
- A standalone server is not supported: the first write inside a transaction fails, not the connection.
- Searching is by the fields the indexes cover; anything else is a collection scan.

## Packages and settings

`Directory.Packages.props`: `MongoDB.Driver` 3.14.1 in place of `Npgsql.EntityFrameworkCore.PostgreSQL` and `Microsoft.EntityFrameworkCore.Design`; Infrastructure references it. No `dotnet ef` tool.

The connection string names the database and the replica set:

```json
"ConnectionStrings": {
  "Default": "mongodb://localhost:27017/{database}?replicaSet=rs0&directConnection=true"
}
```

`docker-compose.yml` starts a single-node replica set and initiates it in the health check:

```yaml
  mongo:
    image: mongo:8
    command: ["--replSet", "rs0", "--bind_ip_all"]
    ports: ["27017:27017"]
    healthcheck:
      test: ["CMD", "mongosh", "--quiet", "--eval", "try { rs.status().ok } catch (e) { rs.initiate({ _id: 'rs0', members: [{ _id: 0, host: 'localhost:27017' }] }).ok }"]
      interval: 5s
      retries: 20
```

The project README says that a standalone server is not supported, and how to start the compose file.

## MongoModule

File `Persistence/Mongo/MongoModule.cs`, called by `AddInfrastructureModule` in place of `AddDbContext` and `AddPersistenceModule`:

```csharp
public static class MongoModule
{
    public static IServiceCollection AddMongoModule(this IServiceCollection services, IConfiguration configuration)
    {
        MongoClassMaps.Register();

        var mongoUrl = MongoUrl.Create(configuration.GetConnectionString(PersistenceConstants.ConnectionStringName));

        services.AddSingleton<IMongoClient>(new MongoClient(mongoUrl));
        services.AddSingleton(serviceProvider => serviceProvider.GetRequiredService<IMongoClient>().GetDatabase(mongoUrl.DatabaseName));
        services.AddScoped<MongoUnitOfWork>();
        services.AddScoped<IUnitOfWork>(serviceProvider => serviceProvider.GetRequiredService<MongoUnitOfWork>());
        services.AddScoped<ICategoryRepository, CategoryRepository>();
        services.AddScoped<ICustomerRepository, CustomerRepository>();
        services.AddScoped<IOrderRepository, OrderRepository>();
        services.AddScoped<IProductRepository, ProductRepository>();
        services.AddSingleton<MongoIndexes>();
        services.AddHostedService<MongoIndexesService>();

        return services;
    }
}
```

`PersistenceConstants` gains the element and collection names:

```csharp
    public const string VersionElement = "version";
    public const string CountersCollection = "counters";
    public const string OutboxCollection = "outbox_messages";
```

## MongoClassMaps

File `Persistence/Mongo/MongoClassMaps.cs`. Conventions for every class, then maps only where a class needs more. Registered once per process, before the first client is used:

```csharp
public static class MongoClassMaps
{
    private static readonly Lazy<bool> Registered = new(RegisterAll);

    public static void Register()
    {
        _ = Registered.Value;
    }

    private static bool RegisterAll()
    {
        BsonSerializer.RegisterSerializer(new GuidSerializer(GuidRepresentation.Standard));
        BsonSerializer.RegisterSerializer(new DecimalSerializer(BsonType.Decimal128));

        ConventionRegistry.Register("mortis", new ConventionPack
        {
            new CamelCaseElementNameConvention(),
            new IgnoreExtraElementsConvention(true),
            new EnumRepresentationConvention(BsonType.String)
        }, _ => true);

        BsonClassMap.RegisterClassMap<Order>(map =>
        {
            map.AutoMap();
            map.MapField("_lines").SetElementName("lines");
        });

        return true;
    }
}
```

- `AutoMap` maps properties with any setter (`protected set` included) and finds the non-public parameterless constructor the Domain already has for EF; get-only members (`DomainEvents`, `Version`, `Lines`, `TotalAmount`) are left out.
- An immutable value object (`Money`) is mapped through its constructor by the driver's immutable-type convention.
- With `languages.mode` = `data`, `LocalizedText` is mapped as an embedded document of its texts: `BsonClassMap.RegisterClassMap<LocalizedText>(map => map.MapField("_texts").SetElementName("texts"))` and a creator from the dictionary; search a translated field by `name.texts.tr`.
- `IgnoreExtraElements` lets the aggregate be read from a document that also holds `version`.
- A shape change of a stored document: add a `schemaVersion` element and upgrade old documents when they are read (an `ISupportInitialize` or a custom serializer); there is no migration tool.

## MongoUnitOfWork

File `Persistence/Mongo/MongoUnitOfWork.cs`. The same contract as the EF `UnitOfWork` (`infrastructure-postgres.md`), with the version map the repositories need. Writes reach the server as they happen, inside the session, so `SaveChangesAsync` has nothing to flush. This is the `events.outbox` = `after-command` version; the other publishing models change the lines between `ExecuteRaisedAsync` and the commit exactly as on `outbox.md` and `events-inprocess.md`:

```csharp
public class MongoUnitOfWork(
    IMongoClient mongoClient,
    EventContext eventContext,
    EventHandlerExecutor eventHandlerExecutor,
    OutboxRepository outboxRepository,
    OutboxWorker outboxWorker) : IUnitOfWork
{
    private readonly Dictionary<object, long> _versions = new(ReferenceEqualityComparer.Instance);
    private readonly List<Func<Task>> _committedActions = [];
    private string _transactionKey;

    public IClientSessionHandle Session { get; private set; }
    public bool HasTransaction => Session?.IsInTransaction == true;

    public Task<int> SaveChangesAsync()
    {
        return Task.FromResult(0);
    }

    public async Task BeginTransactionAsync(string transactionKey)
    {
        if (HasTransaction) return;

        Session ??= await mongoClient.StartSessionAsync();
        Session.StartTransaction(new TransactionOptions(ReadConcern.Snapshot, writeConcern: WriteConcern.WMajority));
        _transactionKey = transactionKey;
    }

    public async Task CommitTransactionAsync(string transactionKey)
    {
        if (!HasTransaction || transactionKey != _transactionKey) return;

        await eventHandlerExecutor.ExecuteRaisedAsync();

        var queueEvents = new List<EventBase>();

        while (eventContext.TakeDispatched() is { } @event)
        {
            if (@event is IQueueEvent) queueEvents.Add(@event);
        }

        await outboxRepository.AddAsync(queueEvents);
        await Session.CommitTransactionAsync();

        _transactionKey = null;
        _versions.Clear();

        foreach (var queueEvent in queueEvents)
        {
            await outboxWorker.PublishAsync(queueEvent.EventId);
        }

        var committedActions = _committedActions.ToList();

        _committedActions.Clear();

        foreach (var committedAction in committedActions)
        {
            await committedAction();
        }
    }

    public async Task RollbackTransactionAsync()
    {
        eventContext.Clear();
        _committedActions.Clear();
        _versions.Clear();

        if (!HasTransaction) return;

        await Session.AbortTransactionAsync();

        _transactionKey = null;
    }

    public void OnCommitted(Func<Task> action)
    {
        _committedActions.Add(action);
    }

    public void Remember(object aggregate, long version)
    {
        _versions[aggregate] = version;
    }

    public long VersionOf(object aggregate)
    {
        return _versions.TryGetValue(aggregate, out var version)
            ? version
            : throw new InvalidOperationException($"{aggregate.GetType().Name} is not tracked. Load it with FindByIdTrackingAsync before UpdateAsync.");
    }
}
```

The session is disposed with the scope: `MongoUnitOfWork` also implements `IDisposable` with `Session?.Dispose();`.

## MongoRepository

File `Persistence/Mongo/MongoRepository.cs`. Every query of a repository goes through `Find`, `FindOneTrackingAsync` or `FindManyAsync` of this base, never through `Documents` directly, so that one place adds the tenant filter when tenancy is on (an architecture test checks it):

```csharp
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
```

- An integer key comes from the `counters` collection inside the same transaction (the example's `Category`): `keys.default` cannot be `int` or `long` with MongoDB (R12), but an aggregate whose key the Domain fixes as an integer still works. Two concurrent creates of the same type touch the same counter; the second gets a transient write conflict and is retried by the caller.
- `ConcurrencyConflictException`, file `Persistence/Mongo/ConcurrencyConflictException.cs`: `public class ConcurrencyConflictException(string name) : Exception($"{name} was changed by someone else since it was loaded.");`. It is not an `ExceptionBase`: like EF's `DbUpdateConcurrencyException` it reaches the caller as a 500, and event consumers retry it by their `Retry(n)`.

A repository adds its own lookups through the base:

```csharp
public class ProductRepository(IMongoDatabase database, MongoUnitOfWork unitOfWork, EventContext eventContext) : MongoRepository<Product, Guid>(database, unitOfWork, eventContext, "products"), IProductRepository
{
    public Task<Product> FindBySkuTrackingAsync(string sku)
    {
        return FindOneTrackingAsync(Builders<BsonDocument>.Filter.Eq("sku", sku));
    }
}
```

Collections are named after the aggregate in snake case and plural: `products`, `customers`, `orders`, `categories`.

## MongoExceptionHandler

File `Persistence/Mongo/MongoExceptionHandler.cs`. A duplicate key (11000) becomes the same `DuplicateException` as with PostgreSQL, named after the index:

```csharp
public static class MongoExceptionHandler
{
    private const int DuplicateKey = 11000;

    public static async Task<T> Run<T>(Func<Task<T>> write)
    {
        try
        {
            return await write();
        }
        catch (MongoWriteException exception) when (exception.WriteError.Category == ServerErrorCategory.DuplicateKey)
        {
            throw new DuplicateException(IndexOf(exception.WriteError.Message), exception);
        }
        catch (MongoCommandException exception) when (exception.Code == DuplicateKey)
        {
            throw new DuplicateException(IndexOf(exception.ErrorMessage), exception);
        }
    }

    public static Task Run(Func<Task> write)
    {
        return Run(async () =>
        {
            await write();

            return true;
        });
    }

    private static string IndexOf(string message)
    {
        var start = message.IndexOf("index: ", StringComparison.Ordinal);

        return start < 0 ? "unique index" : message[(start + 7)..].Split(' ')[0];
    }
}
```

Inside a transaction a duplicate may come back as a command error rather than a write error, hence both branches.

## Indexes

File `Persistence/Mongo/MongoIndexes.cs`. Unique indexes are the rules the PostgreSQL configurations declare with `IsUnique`; with tenancy each starts with `tenantId`:

```csharp
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
```

Creating an index that already exists with the same keys and options does nothing, so `EnsureAsync` is safe to repeat. Changing an index's options needs a drop by hand.

File `Persistence/Mongo/MongoIndexesService.cs`, a hosted service that ensures them at start in Development only:

```csharp
public class MongoIndexesService(MongoIndexes mongoIndexes, IHostEnvironment hostEnvironment) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        return hostEnvironment.IsDevelopment() ? mongoIndexes.EnsureAsync() : Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
```

In deployment the Console runs them once: `dotnet run -- ensure-indexes`. `ConsoleConstants` gains `public const string EnsureIndexesCommand = "ensure-indexes";`, and `Program.cs` handles it before running a command:

```csharp
    if (arguments[0] == ConsoleConstants.EnsureIndexesCommand)
    {
        await host.Services.GetRequiredService<MongoIndexes>().EnsureAsync();

        return 0;
    }
```

## The outbox on MongoDB

`OutboxMessage` and `OutboxWorker` are the classes of `outbox.md`; the message is class-mapped like an aggregate and lives in `outbox_messages`. `OutboxRepository` writes and claims with the session. This is the `events.transport` = `database` version (one document per registered consumer):

```csharp
public class OutboxRepository(IMongoDatabase database, MongoUnitOfWork unitOfWork, EventConsumerRegistry eventConsumerRegistry)
{
    private readonly IMongoCollection<OutboxMessage> _messages = database.GetCollection<OutboxMessage>(PersistenceConstants.OutboxCollection);

    public Task AddAsync(IEnumerable<EventBase> events)
    {
        var outboxMessages = events
            .SelectMany(@event => eventConsumerRegistry.For(@event.GetType()).Select(eventConsumerEndpoint => OutboxMessage.From(@event, eventConsumerEndpoint)))
            .ToList();

        return outboxMessages.Count == 0 ? Task.CompletedTask : _messages.InsertManyAsync(unitOfWork.Session, outboxMessages);
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
        return _messages.DeleteOneAsync(unitOfWork.Session, Builders<OutboxMessage>.Filter.Eq(candidate => candidate.Id, message.Id));
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
            unitOfWork.Session,
            filter & due,
            Builders<OutboxMessage>.Update.Set(message => message.ClaimedAt, DateTime.UtcNow),
            new FindOneAndUpdateOptions<OutboxMessage> { Sort = Builders<OutboxMessage>.Sort.Ascending(message => message.NextAttemptAt) });
    }
}
```

- The claim writes `ClaimedAt` (a property `OutboxMessage` gains with MongoDB) so that the document is locked by the worker's transaction. A second worker that picks the same document gets a transient write conflict instead of PostgreSQL's `skip locked`; `OutboxWorker` treats `MongoException` with the `TransientTransactionError` label as "taken by another worker": it rolls back without counting an attempt and moves on.
- `RecordFailureAsync` runs outside the rolled-back session, so the attempt is counted even though the consumer's work was undone.
- With `events.transport` = `rabbitmq` there is one document per event and no consumer or attempt limit, as on `rabbitmq.md`.

## Tenancy

With `tenancy.mode` = `interface`, `MongoRepository` takes `TenantScope` and `Find` scopes every filter; there is no global filter to forget, which is why every query goes through the base:

```csharp
    protected IFindFluent<BsonDocument, BsonDocument> Find(FilterDefinition<BsonDocument> filter)
    {
        var scoped = typeof(IMultiTenant).IsAssignableFrom(typeof(TAggregate)) && !tenantScope.Bypassed
            ? filter & Builders<BsonDocument>.Filter.Eq("tenantId", new BsonBinaryData(tenantScope.Required(typeof(TAggregate).Name), GuidRepresentation.Standard))
            : filter;

        return unitOfWork.Session is null ? _documents.Find(scoped) : _documents.Find(unitOfWork.Session, scoped);
    }
```

`CreateAsync` stamps the tenant on the document before the insert (`document["tenantId"] = ...`) and `UpdateAsync` and `DeleteAsync` add the same tenant condition to their filters. Every unique index starts with `tenantId`.

## Tests

Hand-written for phase 2. `test/{Name}.Tests/Infrastructure/Persistence/Mongo/MongoClassMapsTests.cs`, the trap the plan names: the driver's default Guid representation has changed across versions, so the bytes are pinned:

```csharp
public class MongoClassMapsTests
{
    [Fact]
    public void ToBsonDocument_Should_StoreTheIdAsAStandardUuid()
    {
        MongoClassMaps.Register();
        var product = FakeProduct.Create("SKU-1", 10);

        var id = product.ToBsonDocument()["_id"].AsBsonBinaryData;

        Assert.Equal(BsonBinarySubType.UuidStandard, id.SubType);
        Assert.Equal(product.Id.ToByteArray(bigEndian: true), id.Bytes);
    }

    [Fact]
    public void ToBsonDocument_Should_UseCamelCaseNamesAndEmbedTheOrderLines()
    {
        MongoClassMaps.Register();
        var order = FakeOrder.Create().WithLine("SKU-1", quantity: 2);

        var document = order.ToBsonDocument();

        Assert.True(document.Contains("orderNumber"));
        Assert.Single(document["lines"].AsBsonArray);
        Assert.False(document.Contains("totalAmount"));
        Assert.False(document.Contains("domainEvents"));
    }

    [Fact]
    public void Deserialize_Should_ReadBackAnOrderWithItsLinesFromADocumentWithAVersion()
    {
        MongoClassMaps.Register();
        var order = FakeOrder.Create().WithLine("SKU-1", quantity: 2);
        var document = order.ToBsonDocument();
        document[PersistenceConstants.VersionElement] = 3L;

        var read = BsonSerializer.Deserialize<FakeOrder>(document);

        Assert.Equal([("SKU-1", 2)], read.Lines.Select(line => (line.Sku, line.Quantity)));
    }
}
```

`test/{Name}.Tests/Infrastructure/Persistence/Mongo/MongoRepositoryRulesTests.cs`, the architecture test for tenancy: no repository reaches the collection except through the base.

```csharp
public class MongoRepositoryRulesTests
{
    [Fact]
    public void Repositories_Should_QueryOnlyThroughTheBase()
    {
        var mongoFolder = Path.Combine(SolutionFolder.Find(), "src", "{Name}.Infrastructure", "Persistence", "Mongo");

        var offenders = Directory.GetFiles(mongoFolder, "*Repository.cs", SearchOption.AllDirectories)
            .Where(file => Path.GetFileName(file) != "MongoRepository.cs")
            .Where(file => File.ReadAllText(file).Contains("GetCollection<"));

        Assert.Empty(offenders);
    }
}
```

`SolutionFolder.Find()`, a new file in `TestSupport/`, walks up from `AppContext.BaseDirectory` to the folder holding `{Name}.slnx`. `OutboxRepository` is outside the rule: the outbox is not tenant-owned.

The rest is proven by `scripts/conformance/database-mongo.sh` against a running replica set.

## Namespaces and usings

| File | Namespace | Usings |
|---|---|---|
| `MongoModule.cs` | `{Name}.Infrastructure.Persistence.Mongo` | `Microsoft.Extensions.Configuration`, `Microsoft.Extensions.DependencyInjection`, `MongoDB.Driver`, `{Name}.Shared.Persistence`, the four `{Name}.Domain.Aggregates.*` namespaces, `{Name}.Infrastructure.Persistence.Mongo.*` repository namespaces |
| `MongoClassMaps.cs` | same | `MongoDB.Bson`, `MongoDB.Bson.Serialization`, `MongoDB.Bson.Serialization.Conventions`, `MongoDB.Bson.Serialization.Serializers`, `{Name}.Domain.Aggregates.Orders` |
| `MongoUnitOfWork.cs` | same | `MongoDB.Driver`, `{Name}.Infrastructure.Messaging.Outbox`, `{Name}.Shared.Domain`, `{Name}.Shared.Persistence` |
| `MongoRepository.cs` | same | `MongoDB.Bson`, `MongoDB.Bson.Serialization`, `MongoDB.Driver`, `{Name}.Shared.Domain`, `{Name}.Shared.Exceptions`, `{Name}.Shared.Persistence` |
| `MongoExceptionHandler.cs`, `ConcurrencyConflictException.cs` | same | `MongoDB.Driver`, `{Name}.Shared.Exceptions` |
| `MongoIndexes.cs`, `MongoIndexesService.cs` | same | `MongoDB.Bson`, `MongoDB.Driver`, `Microsoft.Extensions.Hosting` |
| `Products/ProductRepository.cs` and the other repositories | `{Name}.Infrastructure.Persistence.Mongo.Products` (and so on) | `MongoDB.Bson`, `MongoDB.Driver`, `{Name}.Domain.Aggregates.Products`, `{Name}.Shared.Domain` |

## Decision record

`docs/adr/0017-aggregates-as-documents-in-mongodb.md`, in `project-docs-adr.md`.
