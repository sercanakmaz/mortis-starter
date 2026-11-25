# Infrastructure on MongoDB, C#

Applies when `database` is `mongo`. Project `{Name}.Infrastructure`, folder `Persistence/Mongo/`. Layer 1: `options/database.md`, `architecture/documents.md`.

Built and verified (phase 2 step 04, run A): every snippet on this page comes from a standard-preset project on MongoDB 7 (a single-node replica set) with MongoDB.Driver 3.14.1, which passes `verify.sh` including `scripts/conformance/database-mongo.sh`. Tenancy on MongoDB (the last section) is still written by hand and not built.

The Domain and Application projects are the same files as with PostgreSQL. Only Infrastructure, the hosts' settings, `docker-compose.yml`, the Console's `ensure-indexes` command, the packages and the persistence tests change. Everything on `infrastructure-postgres.md` (EF Core, `AppDbContext`, configurations, migrations, `EfRepository`, `DatabaseExceptionHandler`, `PersistenceModule`, `UnitOfWork`, `DesignTimeDbContextFactory`) and `OutboxMessageConfiguration` are replaced by this page; `AppDbContextBuilder`, the configuration tests and the EF repository tests are not written.

## How it fits together

```
an aggregate          one document; child entities and value objects embedded; references to other aggregates are ids
class maps            Persistence/Mongo/MongoClassMaps.cs: conventions + one map per aggregate that needs more; no driver attribute in the Domain
session               MongoSession: one client session per scope, shared by the unit of work, the repositories and the outbox
repository            MongoRepository<TAggregate, TKey> implements the same IRepository
  FindByIdTrackingAsync  reads the document, remembers its version in the unit of work
  UpdateAsync            replaces the document where _id = id AND version = the remembered one; nothing matched -> ConcurrencyConflictException
reader                {Aggregate}Reader reads the typed collection and maps to the view in memory
unit of work          a multi-document transaction on the session (needs a replica set)
outbox                collection outbox_messages, written in the same session; claimed with findOneAndUpdate
indexes               MongoIndexes: ensured at start in Development, by the Console command ensure-indexes elsewhere
```

- The documents keep the element names camelCase (`sku`, `orderNumber`), enums as strings, `decimal` as `Decimal128`, `Guid` as a standard UUID (binary subtype 4).
- A document has one element the aggregate does not: `version` (a number, 1 on insert, +1 on every replace). An integer key comes from a counter.
- There is no foreign key. An order line may name a sku no product has, and a product may name a category that does not exist; with PostgreSQL the database refuses both. When a rule needs the referenced aggregate to exist, the check belongs in a domain service, never in Infrastructure.
- A standalone server is not supported: the first write inside a transaction fails, not the connection.
- Searching is by the fields the indexes cover; anything else is a collection scan.

## Packages and settings

`Directory.Packages.props`: `MongoDB.Driver` 3.14.1 in place of `Microsoft.EntityFrameworkCore`, `Microsoft.EntityFrameworkCore.Design`, `Microsoft.EntityFrameworkCore.Relational`, `Npgsql.EntityFrameworkCore.PostgreSQL` and `EFCore.NamingConventions`; with `testing` at `integration` or above, `Testcontainers.MongoDb` 4.17.0 in place of `Testcontainers.PostgreSql`. Infrastructure references `Microsoft.Extensions.Hosting`, `Microsoft.IdentityModel.Protocols.OpenIdConnect` (with `auth` = `jwt`) and `MongoDB.Driver`. No `dotnet ef` tool and no `Migrations` folder.

The connection string names the database and the replica set, in both hosts' `appsettings.json`:

```json
{
  "ConnectionStrings": {
    "Default": "mongodb://localhost:27017/{database}?replicaSet=rs0&directConnection=true"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning",
      "Microsoft.EntityFrameworkCore": "Warning"
    }
  },
  "Auth": {
    "Authority": "",
    "Issuer": "{database}",
    "Audience": "{database}",
    "SubjectClaim": "sub",
    "EmailClaim": "email"
  }
}
```

`docker-compose.yml` starts a single-node replica set and initiates it in the health check:

```yaml
services:
  mongo:
    image: mongo:7
    command: ["--replSet", "rs0", "--bind_ip_all"]
    ports: ["27017:27017"]
    volumes:
      - mongo-data:/data/db
    healthcheck:
      test: ["CMD", "mongosh", "--quiet", "--eval", "try { rs.status().ok } catch (e) { rs.initiate({ _id: 'rs0', members: [{ _id: 0, host: 'localhost:27017' }] }).ok }"]
      interval: 5s
      retries: 20

  api:
    build:
      context: .
      dockerfile: Dockerfile
    environment:
      ConnectionStrings__Default: mongodb://mongo:27017/{database}?replicaSet=rs0&directConnection=true
    ports:
      - "8080:8080"
    depends_on:
      mongo:
        condition: service_healthy

volumes:
  mongo-data:
```

The project README says that a standalone server is not supported, and how to start the compose file.

## MongoModule

File `Persistence/Mongo/MongoModule.cs`, called by `AddInfrastructureModule`. Written at step 6 without the example's lines; the repository, reader and class-map lines of the example's aggregates are added at step 10 with them in place of `AddDbContext`, the `IUnitOfWork` registration and `AddPersistenceModule` (`services.AddMongoModule(configuration);`). It registers the readers too when `queries` is `split` or `read-models`:

```csharp
public static class MongoModule
```

```csharp
public static IServiceCollection AddMongoModule(this IServiceCollection services, IConfiguration configuration)
{
    MongoClassMaps.Register();

    var mongoUrl = MongoUrl.Create(configuration.GetConnectionString(PersistenceConstants.ConnectionStringName));

    services.AddSingleton<IMongoClient>(new MongoClient(mongoUrl));
    services.AddSingleton(serviceProvider => serviceProvider.GetRequiredService<IMongoClient>().GetDatabase(mongoUrl.DatabaseName));
    services.AddScoped<MongoSession>();
    services.AddScoped<MongoUnitOfWork>();
    services.AddScoped<IUnitOfWork>(serviceProvider => serviceProvider.GetRequiredService<MongoUnitOfWork>());
    services.AddScoped<ICategoryRepository, CategoryRepository>();
    services.AddScoped<ICustomerRepository, CustomerRepository>();
    services.AddScoped<IOrderRepository, OrderRepository>();
    services.AddScoped<IProductRepository, ProductRepository>();
    services.AddScoped<ICategoryReader, CategoryReader>();
    services.AddScoped<ICustomerReader, CustomerReader>();
    services.AddScoped<IOrderReader, OrderReader>();
    services.AddScoped<IProductReader, ProductReader>();
    services.AddSingleton<MongoIndexes>();
    services.AddHostedService<MongoIndexesService>();

    return services;
}
```

`PersistenceConstants` loses `DesignTimeConnectionString` and `RowVersionColumn` and gains the element and collection names:

```csharp
public const string VersionElement = "version";
public const string CountersCollection = "counters";
public const string OutboxCollection = "outbox_messages";
```

## MongoClassMaps

File `Persistence/Mongo/MongoClassMaps.cs`. Conventions for every class, then maps only where a class needs more. Registered once per process, before the first client is used:

```csharp
public static class MongoClassMaps
```

```csharp
private const string OrderLinesField = "_lines";
private static readonly Lazy<bool> Registered = new(RegisterAll);
```

```csharp
public static void Register()
{
    _ = Registered.Value;
}
```

```csharp
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
        map.MapField(OrderLinesField).SetElementName("lines");
        map.MapCreator((List<OrderLine> lines) => WithLines(lines), OrderLinesField);
    });

    return true;
}
```

```csharp
private static Order WithLines(List<OrderLine> lines)
{
    var order = (Order)Activator.CreateInstance(typeof(Order), nonPublic: true);

    typeof(Order).GetField(OrderLinesField, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(order, lines ?? []);

    return order;
}
```

- `AutoMap` maps properties with any setter (`protected set` included) and finds the non-public parameterless constructor the Domain already has for EF; get-only members (`DomainEvents`, `Version`, `Lines`, `TotalAmount`) are left out.
- **A `readonly` collection field is written but never read back by the driver.** `Order._lines` is `readonly`, so the map gives `Order` a creator that receives the lines and sets the field by reflection (`WithLines`); without it every order loads with no lines. Every aggregate with a `readonly` child collection gets the same creator. A creator is not inherited: deserialize as the aggregate type, never as its fake.
- An immutable value object (`Money`) is mapped through its constructor by the driver's immutable-type convention.
- With `languages.mode` = `data`, `LocalizedText` is mapped as an embedded document of its texts: `BsonClassMap.RegisterClassMap<LocalizedText>(map => map.MapField("_texts").SetElementName("texts"))` and a creator from the dictionary; search a translated field by `name.texts.tr`. Not built yet.
- `IgnoreExtraElements` lets the aggregate be read from a document that also holds `version`.
- A shape change of a stored document: add a `schemaVersion` element and upgrade old documents when they are read (an `ISupportInitialize` or a custom serializer); there is no migration tool.

## MongoSession

File `Persistence/Mongo/MongoSession.cs`, scoped. The unit of work, the repositories and `OutboxRepository` all write in the scope's one session; holding it here, not in the unit of work, keeps `MongoUnitOfWork` → `OutboxRepository` → session free of a dependency cycle (the container refuses `MongoUnitOfWork` ↔ `OutboxRepository`). The scope disposes it:

```csharp
public class MongoSession(IMongoClient mongoClient) : IDisposable
```

```csharp
public IClientSessionHandle Handle { get; private set; }
```

```csharp
public async Task<IClientSessionHandle> StartAsync()
{
    return Handle ??= await mongoClient.StartSessionAsync();
}
```

```csharp
public void Dispose()
{
    Handle?.Dispose();
}
```

## MongoUnitOfWork

File `Persistence/Mongo/MongoUnitOfWork.cs`. The same contract as the EF `UnitOfWork` (`infrastructure-postgres.md`), with the version map the repositories need. Writes reach the server as they happen, inside the session, so `SaveChangesAsync` has nothing to flush. This is the `events.outbox` = `after-command` version; the other publishing models change the lines between `ExecuteRaisedAsync` and the commit exactly as on `outbox.md` and `events-inprocess.md`:

```csharp
public class MongoUnitOfWork(
    MongoSession mongoSession,
    EventContext eventContext,
    EventHandlerExecutor eventHandlerExecutor,
    OutboxRepository outboxRepository,
    OutboxWorker outboxWorker) : IUnitOfWork
```

```csharp
private readonly Dictionary<object, long> _versions = new(ReferenceEqualityComparer.Instance);
```

```csharp
private readonly List<Func<Task>> _committedActions = [];
```

```csharp
private string _transactionKey;
```

```csharp
public IClientSessionHandle Session => mongoSession.Handle;
```

```csharp
public bool HasTransaction => Session?.IsInTransaction == true;
```

```csharp
public Task<int> SaveChangesAsync()
{
    return Task.FromResult(0);
}
```

```csharp
public async Task BeginTransactionAsync(string transactionKey)
{
    if (HasTransaction) return;

    var session = await mongoSession.StartAsync();

    session.StartTransaction(new TransactionOptions(ReadConcern.Snapshot, writeConcern: WriteConcern.WMajority));
    _transactionKey = transactionKey;
}
```

```csharp
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
```

```csharp
public async Task RollbackTransactionAsync()
{
    eventContext.Clear();
    _committedActions.Clear();
    _versions.Clear();

    if (!HasTransaction) return;

    await Session.AbortTransactionAsync();

    _transactionKey = null;
}
```

```csharp
public void OnCommitted(Func<Task> action)
{
    _committedActions.Add(action);
}
```

```csharp
public void Remember(object aggregate, long version)
{
    _versions[aggregate] = version;
}
```

```csharp
public long VersionOf(object aggregate)
{
    return _versions.TryGetValue(aggregate, out var version)
        ? version
        : throw new InvalidOperationException($"{aggregate.GetType().Name} is not tracked. Load it with FindByIdTrackingAsync before UpdateAsync.");
}
```

## MongoRepository

File `Persistence/Mongo/MongoRepository.cs`. Every query of a repository goes through `Find`, `FindOneTrackingAsync` or `FindManyAsync` of this base, never through the collection directly, so that one place adds the tenant filter when tenancy is on (an architecture test checks it):

```csharp
public abstract class MongoRepository<TAggregate, TKey>(IMongoDatabase database, MongoUnitOfWork unitOfWork, EventContext eventContext, string collectionName) : IRepository<TAggregate, TKey> where TAggregate : AggregateRoot<TKey>
```

```csharp
private readonly IMongoCollection<BsonDocument> _documents = database.GetCollection<BsonDocument>(collectionName);
```

```csharp
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
```

```csharp
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
```

```csharp
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
```

```csharp
public virtual async Task<TAggregate> FindByIdAsync(TKey id)
{
    var document = await Find(ById(id)).FirstOrDefaultAsync();

    return document is null ? null : BsonSerializer.Deserialize<TAggregate>(document);
}
```

```csharp
public virtual Task<TAggregate> FindByIdTrackingAsync(TKey id)
{
    return FindOneTrackingAsync(ById(id));
}
```

```csharp
protected async Task<TAggregate> FindOneTrackingAsync(FilterDefinition<BsonDocument> filter)
{
    var document = await Find(filter).FirstOrDefaultAsync();

    if (document is null) return null;

    var aggregate = BsonSerializer.Deserialize<TAggregate>(document);

    unitOfWork.Remember(aggregate, document[PersistenceConstants.VersionElement].ToInt64());

    return aggregate;
}
```

```csharp
protected async Task<IReadOnlyList<TAggregate>> FindManyAsync(FilterDefinition<BsonDocument> filter)
{
    var documents = await Find(filter).ToListAsync();

    return documents.Select(document => BsonSerializer.Deserialize<TAggregate>(document)).ToList();
}
```

```csharp
protected IFindFluent<BsonDocument, BsonDocument> Find(FilterDefinition<BsonDocument> filter)
{
    return unitOfWork.Session is null ? _documents.Find(filter) : _documents.Find(unitOfWork.Session, filter);
}
```

```csharp
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
```

```csharp
private static FilterDefinition<BsonDocument> ById(TKey id)
{
    return Builders<BsonDocument>.Filter.Eq("_id", id is Guid guid ? new BsonBinaryData(guid, GuidRepresentation.Standard) : BsonValue.Create(id));
}
```

```csharp
private static FilterDefinition<BsonDocument> AtVersion(long version)
{
    return Builders<BsonDocument>.Filter.Eq(PersistenceConstants.VersionElement, version);
}
```

```csharp
private void RaiseEvents(TAggregate aggregate)
{
    foreach (var @event in aggregate.DomainEvents ?? [])
    {
        eventContext.AddRaised(@event);
    }

    aggregate.ClearEvents();
}
```

```csharp
private void EnsureTransaction()
{
    if (!unitOfWork.HasTransaction)
    {
        throw new TransactionRequiredException(typeof(TAggregate).Name);
    }
}
```

- An integer key comes from the `counters` collection inside the same transaction (the example's `Category`): `keys.default` cannot be `int` or `long` with MongoDB (R12), but an aggregate whose key the Domain fixes as an integer still works. Two concurrent creates of the same type touch the same counter; the second gets a transient write conflict and is retried by the caller.
- `ConcurrencyConflictException`, file `Persistence/Mongo/ConcurrencyConflictException.cs`: `public class ConcurrencyConflictException(string name) : Exception($"{name} was changed by someone else since it was loaded.");`. It is not an `ExceptionBase`: like EF's `DbUpdateConcurrencyException` it reaches the caller as a 500, and event consumers retry it by their `Retry(n)`. So does a write conflict between two open transactions on one document (a `MongoException` labelled `TransientTransactionError`).

A repository adds its own lookups through the base; one without lookups is a single line:

```csharp
public class ProductRepository(IMongoDatabase database, MongoUnitOfWork unitOfWork, EventContext eventContext) : MongoRepository<Product, Guid>(database, unitOfWork, eventContext, "products"), IProductRepository
```

```csharp
public Task<Product> FindBySkuTrackingAsync(string sku)
{
    return FindOneTrackingAsync(Builders<BsonDocument>.Filter.Eq("sku", sku));
}
```

```csharp
public class OrderRepository(IMongoDatabase database, MongoUnitOfWork unitOfWork, EventContext eventContext) : MongoRepository<Order, Guid>(database, unitOfWork, eventContext, "orders"), IOrderRepository;
```

Collections are named after the aggregate in snake case and plural: `products`, `customers`, `orders`, `categories`. Repositories live in `Persistence/Mongo/{Plural}/`.

## Readers

With `queries` = `split` or `read-models`, each reader of `queries.md` is written for MongoDB in `Persistence/Mongo/{Plural}/{Aggregate}Reader.cs`. A reader reads the typed collection (the class maps make it work) and maps to the view in memory; it is not tenant-scoped, so with tenancy it filters by the tenant itself:

```csharp
public class ProductReader(IMongoDatabase database) : IProductReader
```

```csharp
private readonly IMongoCollection<Product> _products = database.GetCollection<Product>("products");
```

```csharp
public async Task<ProductView> FindAsync(Guid id)
{
    var product = await _products.Find(candidate => candidate.Id == id).FirstOrDefaultAsync();

    return product is null ? null : AsView(product);
}
```

```csharp
public async Task<PagedResult<ProductView>> ListAsync(ProductListFilter filter)
{
    var filtered = Filtered(filter);
    var total = (int)await _products.CountDocumentsAsync(filtered);
    var products = await _products.Find(filtered).Sort(SortOf(filter.Sort)).Skip(PageRequest.Skip(filter.Page, filter.Size)).Limit(filter.Size).ToListAsync();

    return new PagedResult<ProductView>(products.Select(AsView).ToList(), filter.Page, filter.Size, total);
}
```

```csharp
public static FilterDefinition<Product> Filtered(ProductListFilter filter)
{
    var filtered = Builders<Product>.Filter.Empty;

    if (!string.IsNullOrWhiteSpace(filter.NameContains))
    {
        filtered &= Builders<Product>.Filter.Regex(product => product.Name, new BsonRegularExpression(Regex.Escape(filter.NameContains), "i"));
    }

    if (filter.CategoryId is not null)
    {
        filtered &= Builders<Product>.Filter.Eq(product => product.CategoryId, filter.CategoryId);
    }

    return filtered;
}
```

```csharp
public static SortDefinition<Product> SortOf(ProductSort sort)
{
    return sort switch
    {
        ProductSort.NameAscending => Builders<Product>.Sort.Ascending(product => product.Name).Ascending(product => product.Id),
        ProductSort.StockDescending => Builders<Product>.Sort.Descending(product => product.Stock).Ascending(product => product.Id),
        _ => Builders<Product>.Sort.Ascending(product => product.Sku)
    };
}
```

The views are those of `queries.md`; the two it does not show:

```csharp
public record CategoryView(int Id, string Name);
public record CustomerView(Guid Id, string Name, string Email, bool IsBlocked, int CompletedOrderCount, string Note);
```

`Filtered` and `SortOf` are public so that a unit test renders them without a server. The name filter is a case-insensitive regular expression of the escaped text (`Regex.Escape`), PostgreSQL's `ILIKE`. The other readers find one document and map it:

```csharp
public async Task<OrderView> FindAsync(Guid id)
{
    var order = await _orders.Find(candidate => candidate.Id == id).FirstOrDefaultAsync();

    return order is null ? null : new OrderView(
        order.Id,
        order.OrderNumber,
        order.CustomerId,
        order.Status.ToString(),
        order.TotalAmount,
        order.Lines.Select(line => new OrderLineView(line.Id, line.Sku, line.Price.Amount, line.Price.Currency, line.Quantity, line.IsCancelled, line.CancellationReason)).ToList());
}
```

## MongoExceptionHandler

File `Persistence/Mongo/MongoExceptionHandler.cs`. A duplicate key (11000) becomes the same `DuplicateException` as with PostgreSQL, named after the index:

```csharp
public static class MongoExceptionHandler
```

```csharp
private const int DuplicateKey = 11000;
```

```csharp
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
```

```csharp
public static Task Run(Func<Task> write)
{
    return Run(async () =>
    {
        await write();

        return true;
    });
}
```

```csharp
private static string IndexOf(string message)
{
    var start = message.IndexOf("index: ", StringComparison.Ordinal);

    return start < 0 ? "unique index" : message[(start + 7)..].Split(' ')[0];
}
```

Inside a transaction a duplicate may come back as a command error rather than a write error, hence both branches.

## Indexes

File `Persistence/Mongo/MongoIndexes.cs`. Unique indexes are the rules the PostgreSQL configurations declare with `IsUnique`; with tenancy each starts with `tenantId`:

```csharp
public class MongoIndexes(IMongoDatabase database)
```

```csharp
public async Task EnsureAsync()
{
    await UniqueAsync("categories", "name");
    await UniqueAsync("customers", "email");
    await UniqueAsync("orders", "orderNumber");
    await UniqueAsync("products", "sku");
    await database.GetCollection<BsonDocument>(PersistenceConstants.OutboxCollection).Indexes.CreateOneAsync(
        new CreateIndexModel<BsonDocument>(Builders<BsonDocument>.IndexKeys.Ascending("eventId").Ascending("nextAttemptAt")));
}
```

```csharp
private Task UniqueAsync(string collectionName, params string[] fields)
{
    var keys = Builders<BsonDocument>.IndexKeys.Combine(fields.Select(field => Builders<BsonDocument>.IndexKeys.Ascending(field)));

    return database.GetCollection<BsonDocument>(collectionName).Indexes.CreateOneAsync(
        new CreateIndexModel<BsonDocument>(keys, new CreateIndexOptions { Unique = true, Name = $"ux_{collectionName}_{string.Join("_", fields)}" }));
}
```

Creating an index that already exists with the same keys and options does nothing, so `EnsureAsync` is safe to repeat. Changing an index's options needs a drop by hand. A unique index cannot be built over documents that already break it: the Api then fails to start, naming the duplicate.

File `Persistence/Mongo/MongoIndexesService.cs`, a hosted service that ensures them at start in Development only:

```csharp
public class MongoIndexesService(MongoIndexes mongoIndexes, IHostEnvironment hostEnvironment) : IHostedService
```

```csharp
public Task StartAsync(CancellationToken cancellationToken)
{
    return hostEnvironment.IsDevelopment() ? mongoIndexes.EnsureAsync() : Task.CompletedTask;
}
```

```csharp
public Task StopAsync(CancellationToken cancellationToken)
{
    return Task.CompletedTask;
}
```

In deployment the Console runs them once: `dotnet run -- ensure-indexes`, with no `--user` (it runs no command). `ConsoleConstants` gains `public const string EnsureIndexesCommand = "ensure-indexes";`, and `Program.cs` handles it after the `dev-token` branch, before parsing a command (`using {Name}.Infrastructure.Persistence.Mongo;`):

```csharp
if (args[0] == ConsoleConstants.EnsureIndexesCommand)
{
    await host.Services.GetRequiredService<MongoIndexes>().EnsureAsync();

    return 0;
}
```

## The outbox on MongoDB

`OutboxMessage` and `OutboxWorker` are the classes of `outbox.md` with three changes: `OutboxMessage` gains `public DateTime? ClaimedAt { get; private set; }`; the worker deletes a handled message with `await outboxRepository.DeleteAsync(message);` in place of `outboxRepository.Remove(message);`; and a claim that hits a write conflict means another worker holds the document, so the worker rolls back and moves on without counting an attempt:

```csharp
OutboxMessage message;

try
{
    message = await claim(outboxRepository);
}
catch (MongoException exception) when (exception.HasErrorLabel(OutboxConstants.TransientTransactionErrorLabel))
{
    await unitOfWork.RollbackTransactionAsync();

    return false;
}
```

File `Messaging/Outbox/OutboxConstants.cs`: `public static class OutboxConstants` with `public const string TransientTransactionErrorLabel = "TransientTransactionError";`. Without the catch, the claim right after a commit (`after-command`) can race the background worker for the same document, and the command that already committed reports a failure.

The message is class-mapped by the conventions and lives in `outbox_messages`. `OutboxRepository` writes and claims with the scope's session. This is the `events.transport` = `database` version (one document per registered consumer):

```csharp
public class OutboxRepository(IMongoDatabase database, MongoSession mongoSession, EventConsumerRegistry eventConsumerRegistry)
```

```csharp
private readonly IMongoCollection<OutboxMessage> _messages = database.GetCollection<OutboxMessage>(PersistenceConstants.OutboxCollection);
```

```csharp
public Task AddAsync(IEnumerable<EventBase> events)
{
    var outboxMessages = events
        .SelectMany(@event => eventConsumerRegistry.For(@event.GetType()).Select(eventConsumerEndpoint => OutboxMessage.From(@event, eventConsumerEndpoint)))
        .ToList();

    return outboxMessages.Count == 0 ? Task.CompletedTask : _messages.InsertManyAsync(mongoSession.Handle, outboxMessages);
}
```

```csharp
public Task<OutboxMessage> ClaimNextAsync()
{
    return ClaimAsync(Builders<OutboxMessage>.Filter.Empty);
}
```

```csharp
public Task<OutboxMessage> ClaimAsync(Guid eventId)
{
    return ClaimAsync(Builders<OutboxMessage>.Filter.Eq(message => message.EventId, eventId) & Builders<OutboxMessage>.Filter.Eq(message => message.Attempts, 0));
}
```

```csharp
public Task DeleteAsync(OutboxMessage message)
{
    return _messages.DeleteOneAsync(mongoSession.Handle, Builders<OutboxMessage>.Filter.Eq(candidate => candidate.Id, message.Id));
}
```

```csharp
public Task RecordFailureAsync(OutboxMessage message, Exception exception)
{
    return _messages.UpdateOneAsync(
        Builders<OutboxMessage>.Filter.Eq(candidate => candidate.Id, message.Id),
        Builders<OutboxMessage>.Update
            .Inc(candidate => candidate.Attempts, 1)
            .Set(candidate => candidate.NextAttemptAt, DateTime.UtcNow.AddSeconds(Math.Pow(2, message.Attempts + 1)))
            .Set(candidate => candidate.LastError, exception.Message));
}
```

```csharp
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
```

- The claim writes `ClaimedAt` so that the document is locked by the worker's transaction. A second worker that picks the same document gets a transient write conflict instead of PostgreSQL's `skip locked`; the catch above handles it.
- `RecordFailureAsync` runs outside the rolled-back session, so the attempt is counted even though the consumer's work was undone.
- With `events.transport` = `rabbitmq` there is one document per event and no consumer or attempt limit, as on `rabbitmq.md`. Not built yet.

## Tests

`test/{Name}.Tests/Infrastructure/Persistence/Mongo/MongoClassMapsTests.cs`, the traps the class maps exist for: the driver's default Guid representation has changed across versions, so the bytes are pinned, and the lines must survive a round trip:

```csharp
public class MongoClassMapsTests
```

```csharp
[Fact]
public void ToBsonDocument_Should_StoreTheIdAsAStandardUuid()
{
    MongoClassMaps.Register();
    var product = FakeProduct.Create("SKU-1", 10);

    var id = product.ToBsonDocument()["_id"].AsBsonBinaryData;

    Assert.Equal(BsonBinarySubType.UuidStandard, id.SubType);
    Assert.Equal(product.Id.ToByteArray(bigEndian: true), id.Bytes);
}
```

```csharp
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
```

```csharp
[Fact]
public void Deserialize_Should_ReadBackAnOrderWithItsLinesFromADocumentWithAVersion()
{
    MongoClassMaps.Register();
    var order = FakeOrder.Create().WithLine("SKU-1", quantity: 2);
    var document = order.ToBsonDocument();
    document[PersistenceConstants.VersionElement] = 3L;

    var read = BsonSerializer.Deserialize<Order>(document);

    Assert.Equal([("SKU-1", 2)], read.Lines.Select(line => (line.Sku, line.Quantity)));
}
```

`test/{Name}.Tests/Infrastructure/Persistence/Mongo/MongoRepositoryRulesTests.cs`, the architecture test for tenancy: no repository reaches the collection except through the base.

```csharp
public class MongoRepositoryRulesTests
```

```csharp
[Fact]
public void Repositories_Should_QueryOnlyThroughTheBase()
{
    var mongoFolder = Path.Combine(SolutionFolder.Find(), "src", "{Name}.Infrastructure", "Persistence", "Mongo");

    var offenders = Directory.GetFiles(mongoFolder, "*Repository.cs", SearchOption.AllDirectories)
        .Where(file => Path.GetFileName(file) != "MongoRepository.cs")
        .Where(file => File.ReadAllText(file).Contains("GetCollection<"));

    Assert.Empty(offenders);
}
```

`SolutionFolder.Find()`, file `test/{Name}.Tests/TestSupport/SolutionFolder.cs`, walks up from `AppContext.BaseDirectory` to the folder holding `{Name}.slnx`:

```csharp
public static class SolutionFolder
```

```csharp
public static string Find()
{
    var folder = new DirectoryInfo(AppContext.BaseDirectory);

    while (folder is not null && !File.Exists(Path.Combine(folder.FullName, "{Name}.slnx")))
    {
        folder = folder.Parent;
    }

    return folder?.FullName ?? throw new DirectoryNotFoundException("No folder above the test binaries holds {Name}.slnx.");
}
```

`OutboxRepository` is outside the rule: the outbox is not tenant-owned. With `queries` = `split`, `test/{Name}.Tests/Infrastructure/Persistence/Mongo/ProductReaderTests.cs` renders the filter and the sort:

```csharp
[Fact]
public void Filtered_Should_MatchTheNameIgnoringCaseWithTheTextEscapedAndTheCategory()
{
    MongoClassMaps.Register();

    var filter = ProductReader.Filtered(new ProductListFilter(1, 10, ProductSort.SkuAscending, "a.b", 3))
        .Render(new RenderArgs<Product>(BsonSerializer.LookupSerializer<Product>(), BsonSerializer.SerializerRegistry));

    Assert.Equal("a\\.b", filter["name"].AsBsonRegularExpression.Pattern);
    Assert.Equal("i", filter["name"].AsBsonRegularExpression.Options);
    Assert.Equal(3, filter["categoryId"].AsInt32);
}
```

### Integration tests

With `testing` at `integration` or above, the integration project of `testing-levels.md` runs against a MongoDB replica set: `MongoFixture` and `MongoCollection` replace `PostgresFixture` and `PostgresCollection`, the variable is `MORTIS_TEST_MONGO` (a connection string, or `skip`), and `OutboxRows` is not written. The fixture starts `mongo:7` with Testcontainers' replica set and gives each test class its own database:

```csharp
public async Task InitializeAsync()
{
    ServerConnectionString = Environment.GetEnvironmentVariable(IntegrationConstants.MongoVariable);

    if (ServerConnectionString == IntegrationConstants.SkipValue)
    {
        Unavailable = $"{IntegrationConstants.MongoVariable} is {IntegrationConstants.SkipValue}.";
        return;
    }

    if (!string.IsNullOrEmpty(ServerConnectionString))
    {
        return;
    }

    try
    {
        _container = new MongoDbBuilder(IntegrationConstants.MongoImage).WithReplicaSet().Build();
        await _container.StartAsync();
        ServerConnectionString = _container.GetConnectionString();
    }
    catch (Exception exception) when (exception is DockerUnavailableException or HttpRequestException or TimeoutException or ArgumentException)
    {
        Unavailable = $"No MongoDB: set {IntegrationConstants.MongoVariable} or start Docker ({exception.GetType().Name}).";
    }
}
```

```csharp
public string CreateDatabase(string testClass)
{
    var mongoUrl = new MongoUrlBuilder(ServerConnectionString) { DatabaseName = $"{IntegrationConstants.DatabasePrefix}{testClass.ToLowerInvariant()}_{Guid.CreateVersion7():N}" };

    if (mongoUrl.Username is not null)
    {
        mongoUrl.AuthenticationSource = IntegrationConstants.AuthenticationDatabase;
    }

    return mongoUrl.ToMongoUrl().Url;
}
```

- The container's connection string carries credentials and no database; renaming the database moves the authentication source with it, so it is set back to `admin`. `MongoUrlBuilder.ToString()` masks the credentials: use `ToMongoUrl().Url`.
- `IntegrationConstants` holds `MongoVariable = "MORTIS_TEST_MONGO"`, `MongoImage = "mongo:7"`, `AuthenticationDatabase = "admin"`, `MongoCollection = "mongo"`, `DatabasePrefix` and `SkipValue`.
- `IntegrationServices.CountAsync<TDocument>(collectionName, predicate)` counts documents in place of rows, after `MongoIndexes.EnsureAsync()`; a test that relies on a unique index ensures the indexes first.
- `ProductRepositoryTests` and `UnitOfWorkTests` keep their PostgreSQL meaning (a duplicate sku is a `DuplicateException` and one document; a rolled-back create leaves no document):

```csharp
[Collection(IntegrationConstants.MongoCollection)]
public class ProductRepositoryTests(MongoFixture mongoFixture)
```

```csharp
[SkippableFact]
public async Task Send_WithADuplicateSku_Should_ThrowDuplicateAndKeepOneDocument()
{
    Skip.If(mongoFixture.Unavailable is not null, mongoFixture.Unavailable);
    await using var serviceProvider = IntegrationServices.Create(mongoFixture.CreateDatabase(nameof(ProductRepositoryTests)));

    await serviceProvider.GetRequiredService<MongoIndexes>().EnsureAsync();
    await serviceProvider.Send(new CreateProductCommand { Sku = "SKU-1", Name = "Bolt", Stock = 1 });
    await Assert.ThrowsAsync<DuplicateException>(() => serviceProvider.Send(new CreateProductCommand { Sku = "SKU-1", Name = "Bolt again", Stock = 1 }));

    Assert.Equal(1, await serviceProvider.CountAsync<Product>("products", product => product.Sku == "SKU-1"));
}
```

```csharp
[Collection(IntegrationConstants.MongoCollection)]
public class UnitOfWorkTests(MongoFixture mongoFixture)
```

```csharp
[SkippableFact]
public async Task RollbackTransactionAsync_Should_LeaveNoDocument()
{
    Skip.If(mongoFixture.Unavailable is not null, mongoFixture.Unavailable);
    await using var serviceProvider = IntegrationServices.Create(mongoFixture.CreateDatabase(nameof(UnitOfWorkTests)));

    using (var scope = serviceProvider.CreateScope())
    {
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        await unitOfWork.BeginTransactionAsync(nameof(UnitOfWorkTests));
        await scope.ServiceProvider.GetRequiredService<IProductRepository>().CreateAsync(new Product(Guid.CreateVersion7(), "SKU-RB", "Rolled back", 1));
        await unitOfWork.RollbackTransactionAsync();
    }

    Assert.Equal(0, await serviceProvider.CountAsync<Product>("products", product => product.Sku == "SKU-RB"));
}
```

- `OutboxClaimTests` inserts two due documents (the class map's element names; `_id` and `eventId` standard UUIDs) and claims in two open transactions: the second claim either takes the other document or fails with the `TransientTransactionError` label, never both on one:

```csharp
[Collection(IntegrationConstants.MongoCollection)]
public class OutboxClaimTests(MongoFixture mongoFixture)
```

```csharp
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
```

- The EF tests of `worked-slice-tests.md` "Persistence tests" (`OrderRepositoryTests`, `ProductRepositoryTests` of the unit project, the configuration tests) are not written with MongoDB.

The rest is proven by `scripts/conformance/database-mongo.sh` against a running replica set.

## Tenancy

Not built yet. With `tenancy.mode` = `interface`, `MongoRepository` takes `TenantScope` and `Find` scopes every filter; there is no global filter to forget, which is why every query goes through the base:

```csharp
protected IFindFluent<BsonDocument, BsonDocument> Find(FilterDefinition<BsonDocument> filter)
{
    var scoped = typeof(IMultiTenant).IsAssignableFrom(typeof(TAggregate)) && !tenantScope.Bypassed
        ? filter & Builders<BsonDocument>.Filter.Eq("tenantId", new BsonBinaryData(tenantScope.Required(typeof(TAggregate).Name), GuidRepresentation.Standard))
        : filter;

    return unitOfWork.Session is null ? _documents.Find(scoped) : _documents.Find(unitOfWork.Session, scoped);
}
```

`CreateAsync` stamps the tenant on the document before the insert (`document["tenantId"] = ...`) and `UpdateAsync` and `DeleteAsync` add the same tenant condition to their filters. Every unique index starts with `tenantId`. With `tenancy.key` = `text` the value is the string itself, not a `BsonBinaryData`.

## Namespaces and usings

| File | Namespace | Usings |
|---|---|---|
| `src/{Name}.Infrastructure/Messaging/Outbox/OutboxConstants.cs` | `{Name}.Infrastructure.Messaging.Outbox` | none |
| `src/{Name}.Infrastructure/Persistence/Mongo/Categories/CategoryReader.cs` | `{Name}.Infrastructure.Persistence.Mongo.Categories` | MongoDB.Driver, {Name}.Domain.Aggregates.Categories |
| `src/{Name}.Infrastructure/Persistence/Mongo/Categories/CategoryRepository.cs` | `{Name}.Infrastructure.Persistence.Mongo.Categories` | MongoDB.Bson, MongoDB.Driver, {Name}.Domain.Aggregates.Categories, {Name}.Shared.Domain |
| `src/{Name}.Infrastructure/Persistence/Mongo/ConcurrencyConflictException.cs` | `{Name}.Infrastructure.Persistence.Mongo` | none |
| `src/{Name}.Infrastructure/Persistence/Mongo/Customers/CustomerReader.cs` | `{Name}.Infrastructure.Persistence.Mongo.Customers` | MongoDB.Driver, {Name}.Domain.Aggregates.Customers |
| `src/{Name}.Infrastructure/Persistence/Mongo/Customers/CustomerRepository.cs` | `{Name}.Infrastructure.Persistence.Mongo.Customers` | MongoDB.Bson, MongoDB.Driver, {Name}.Domain.Aggregates.Customers, {Name}.Shared.Domain |
| `src/{Name}.Infrastructure/Persistence/Mongo/MongoClassMaps.cs` | `{Name}.Infrastructure.Persistence.Mongo` | MongoDB.Bson, MongoDB.Bson.Serialization, MongoDB.Bson.Serialization.Conventions, MongoDB.Bson.Serialization.Serializers, System.Reflection, {Name}.Domain.Aggregates.Orders |
| `src/{Name}.Infrastructure/Persistence/Mongo/MongoExceptionHandler.cs` | `{Name}.Infrastructure.Persistence.Mongo` | MongoDB.Driver, {Name}.Shared.Exceptions |
| `src/{Name}.Infrastructure/Persistence/Mongo/MongoIndexes.cs` | `{Name}.Infrastructure.Persistence.Mongo` | MongoDB.Bson, MongoDB.Driver |
| `src/{Name}.Infrastructure/Persistence/Mongo/MongoIndexesService.cs` | `{Name}.Infrastructure.Persistence.Mongo` | Microsoft.Extensions.Hosting |
| `src/{Name}.Infrastructure/Persistence/Mongo/MongoModule.cs` | `{Name}.Infrastructure.Persistence.Mongo` | Microsoft.Extensions.Configuration, Microsoft.Extensions.DependencyInjection, MongoDB.Driver, {Name}.Domain.Aggregates.Categories, {Name}.Domain.Aggregates.Customers, {Name}.Domain.Aggregates.Orders, {Name}.Domain.Aggregates.Products, {Name}.Infrastructure.Persistence.Mongo.Categories, {Name}.Infrastructure.Persistence.Mongo.Customers, {Name}.Infrastructure.Persistence.Mongo.Orders, {Name}.Infrastructure.Persistence.Mongo.Products, {Name}.Shared.Persistence |
| `src/{Name}.Infrastructure/Persistence/Mongo/MongoRepository.cs` | `{Name}.Infrastructure.Persistence.Mongo` | MongoDB.Bson, MongoDB.Bson.Serialization, MongoDB.Driver, {Name}.Shared.Domain, {Name}.Shared.Exceptions, {Name}.Shared.Persistence |
| `src/{Name}.Infrastructure/Persistence/Mongo/MongoSession.cs` | `{Name}.Infrastructure.Persistence.Mongo` | MongoDB.Driver |
| `src/{Name}.Infrastructure/Persistence/Mongo/MongoUnitOfWork.cs` | `{Name}.Infrastructure.Persistence.Mongo` | MongoDB.Driver, {Name}.Infrastructure.Messaging.Outbox, {Name}.Shared.Domain, {Name}.Shared.Persistence |
| `src/{Name}.Infrastructure/Persistence/Mongo/Orders/OrderReader.cs` | `{Name}.Infrastructure.Persistence.Mongo.Orders` | MongoDB.Driver, {Name}.Domain.Aggregates.Orders |
| `src/{Name}.Infrastructure/Persistence/Mongo/Orders/OrderRepository.cs` | `{Name}.Infrastructure.Persistence.Mongo.Orders` | MongoDB.Bson, MongoDB.Driver, {Name}.Domain.Aggregates.Orders, {Name}.Shared.Domain |
| `src/{Name}.Infrastructure/Persistence/Mongo/Products/ProductReader.cs` | `{Name}.Infrastructure.Persistence.Mongo.Products` | MongoDB.Bson, MongoDB.Driver, System.Text.RegularExpressions, {Name}.Domain.Aggregates.Products, {Name}.Shared.Application |
| `src/{Name}.Infrastructure/Persistence/Mongo/Products/ProductRepository.cs` | `{Name}.Infrastructure.Persistence.Mongo.Products` | MongoDB.Bson, MongoDB.Driver, {Name}.Domain.Aggregates.Products, {Name}.Shared.Domain |
| `test/{Name}.Tests/Infrastructure/Persistence/Mongo/MongoClassMapsTests.cs` | `{Name}.Tests.Infrastructure.Persistence.Mongo` | MongoDB.Bson, MongoDB.Bson.Serialization, {Name}.Domain.Aggregates.Orders, {Name}.Infrastructure.Persistence, {Name}.Infrastructure.Persistence.Mongo, {Name}.Tests.TestSupport.Domain.Orders, {Name}.Tests.TestSupport.Domain.Products |
| `test/{Name}.Tests/Infrastructure/Persistence/Mongo/MongoRepositoryRulesTests.cs` | `{Name}.Tests.Infrastructure.Persistence.Mongo` | {Name}.Tests.TestSupport |
| `test/{Name}.Tests/Infrastructure/Persistence/Mongo/ProductReaderTests.cs` | `{Name}.Tests.Infrastructure.Persistence.Mongo` | MongoDB.Bson.Serialization, MongoDB.Driver, {Name}.Domain.Aggregates.Products, {Name}.Infrastructure.Persistence.Mongo, {Name}.Infrastructure.Persistence.Mongo.Products |
| `test/{Name}.Tests/TestSupport/SolutionFolder.cs` | `{Name}.Tests.TestSupport` | none |

## Decision record

`docs/adr/0017-aggregates-as-documents-in-mongodb.md`, in `project-docs-adr.md`.
