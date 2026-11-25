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

@@data extra:standard-a/src/Boilerplate.Api/appsettings.json json@@

`docker-compose.yml` starts a single-node replica set and initiates it in the health check:

@@data extra:standard-a/docker-compose.yml yaml@@

The project README says that a standalone server is not supported, and how to start the compose file.

## MongoModule

File `Persistence/Mongo/MongoModule.cs`, called by `AddInfrastructureModule`. Written at step 6 without the example's lines; the repository, reader and class-map lines of the example's aggregates are added at step 10 with them in place of `AddDbContext`, the `IUnitOfWork` registration and `AddPersistenceModule` (`services.AddMongoModule(configuration);`). It registers the readers too when `queries` is `split` or `read-models`:

@@members extra:standard-a/src/Boilerplate.Infrastructure/Persistence/Mongo/MongoModule.cs::MongoModule@@

`PersistenceConstants` loses `DesignTimeConnectionString` and `RowVersionColumn` and gains the element and collection names:

```csharp
public const string VersionElement = "version";
public const string CountersCollection = "counters";
public const string OutboxCollection = "outbox_messages";
```

## MongoClassMaps

File `Persistence/Mongo/MongoClassMaps.cs`. Conventions for every class, then maps only where a class needs more. Registered once per process, before the first client is used:

@@decl extra:standard-a/src/Boilerplate.Infrastructure/Persistence/Mongo/MongoClassMaps.cs::MongoClassMaps@@

```csharp
private const string OrderLinesField = "_lines";
private static readonly Lazy<bool> Registered = new(RegisterAll);
```

@@member extra:standard-a/src/Boilerplate.Infrastructure/Persistence/Mongo/MongoClassMaps.cs::MongoClassMaps::Register@@

@@member extra:standard-a/src/Boilerplate.Infrastructure/Persistence/Mongo/MongoClassMaps.cs::MongoClassMaps::RegisterAll@@

@@member extra:standard-a/src/Boilerplate.Infrastructure/Persistence/Mongo/MongoClassMaps.cs::MongoClassMaps::WithLines@@

- `AutoMap` maps properties with any setter (`protected set` included) and finds the non-public parameterless constructor the Domain already has for EF; get-only members (`DomainEvents`, `Version`, `Lines`, `TotalAmount`) are left out.
- **A `readonly` collection field is written but never read back by the driver.** `Order._lines` is `readonly`, so the map gives `Order` a creator that receives the lines and sets the field by reflection (`WithLines`); without it every order loads with no lines. Every aggregate with a `readonly` child collection gets the same creator. A creator is not inherited: deserialize as the aggregate type, never as its fake.
- An immutable value object (`Money`) is mapped through its constructor by the driver's immutable-type convention.
- With `languages.mode` = `data`, `LocalizedText` is mapped as an embedded document of its texts: `BsonClassMap.RegisterClassMap<LocalizedText>(map => map.MapField("_texts").SetElementName("texts"))` and a creator from the dictionary; search a translated field by `name.texts.tr`. Not built yet.
- `IgnoreExtraElements` lets the aggregate be read from a document that also holds `version`.
- A shape change of a stored document: add a `schemaVersion` element and upgrade old documents when they are read (an `ISupportInitialize` or a custom serializer); there is no migration tool.

## MongoSession

File `Persistence/Mongo/MongoSession.cs`, scoped. The unit of work, the repositories and `OutboxRepository` all write in the scope's one session; holding it here, not in the unit of work, keeps `MongoUnitOfWork` → `OutboxRepository` → session free of a dependency cycle (the container refuses `MongoUnitOfWork` ↔ `OutboxRepository`). The scope disposes it:

@@members extra:standard-a/src/Boilerplate.Infrastructure/Persistence/Mongo/MongoSession.cs::MongoSession@@

## MongoUnitOfWork

File `Persistence/Mongo/MongoUnitOfWork.cs`. The same contract as the EF `UnitOfWork` (`infrastructure-postgres.md`), with the version map the repositories need. Writes reach the server as they happen, inside the session, so `SaveChangesAsync` has nothing to flush. This is the `events.outbox` = `after-command` version; the other publishing models change the lines between `ExecuteRaisedAsync` and the commit exactly as on `outbox.md` and `events-inprocess.md`:

@@members extra:standard-a/src/Boilerplate.Infrastructure/Persistence/Mongo/MongoUnitOfWork.cs::MongoUnitOfWork@@

## MongoRepository

File `Persistence/Mongo/MongoRepository.cs`. Every query of a repository goes through `Find`, `FindOneTrackingAsync` or `FindManyAsync` of this base, never through the collection directly, so that one place adds the tenant filter when tenancy is on (an architecture test checks it):

@@members extra:standard-a/src/Boilerplate.Infrastructure/Persistence/Mongo/MongoRepository.cs::MongoRepository@@

- An integer key comes from the `counters` collection inside the same transaction (the example's `Category`): `keys.default` cannot be `int` or `long` with MongoDB (R12), but an aggregate whose key the Domain fixes as an integer still works. Two concurrent creates of the same type touch the same counter; the second gets a transient write conflict and is retried by the caller.
- `ConcurrencyConflictException`, file `Persistence/Mongo/ConcurrencyConflictException.cs`: `public class ConcurrencyConflictException(string name) : Exception($"{name} was changed by someone else since it was loaded.");`. It is not an `ExceptionBase`: like EF's `DbUpdateConcurrencyException` it reaches the caller as a 500, and event consumers retry it by their `Retry(n)`. So does a write conflict between two open transactions on one document (a `MongoException` labelled `TransientTransactionError`).

A repository adds its own lookups through the base; one without lookups is a single line:

@@members extra:standard-a/src/Boilerplate.Infrastructure/Persistence/Mongo/Products/ProductRepository.cs::ProductRepository@@

```csharp
public class OrderRepository(IMongoDatabase database, MongoUnitOfWork unitOfWork, EventContext eventContext) : MongoRepository<Order, Guid>(database, unitOfWork, eventContext, "orders"), IOrderRepository;
```

Collections are named after the aggregate in snake case and plural: `products`, `customers`, `orders`, `categories`. Repositories live in `Persistence/Mongo/{Plural}/`.

## Readers

With `queries` = `split` or `read-models`, each reader of `queries.md` is written for MongoDB in `Persistence/Mongo/{Plural}/{Aggregate}Reader.cs`. A reader reads the typed collection (the class maps make it work) and maps to the view in memory; it is not tenant-scoped, so with tenancy it filters by the tenant itself:

@@decl extra:standard-a/src/Boilerplate.Infrastructure/Persistence/Mongo/Products/ProductReader.cs::ProductReader@@

```csharp
private readonly IMongoCollection<Product> _products = database.GetCollection<Product>("products");
```

@@member extra:standard-a/src/Boilerplate.Infrastructure/Persistence/Mongo/Products/ProductReader.cs::ProductReader::FindAsync@@

@@member extra:standard-a/src/Boilerplate.Infrastructure/Persistence/Mongo/Products/ProductReader.cs::ProductReader::ListAsync@@

@@member extra:standard-a/src/Boilerplate.Infrastructure/Persistence/Mongo/Products/ProductReader.cs::ProductReader::Filtered@@

@@member extra:standard-a/src/Boilerplate.Infrastructure/Persistence/Mongo/Products/ProductReader.cs::ProductReader::SortOf@@

The views are those of `queries.md`; the two it does not show:

```csharp
public record CategoryView(int Id, string Name);
public record CustomerView(Guid Id, string Name, string Email, bool IsBlocked, int CompletedOrderCount, string Note);
```

`Filtered` and `SortOf` are public so that a unit test renders them without a server. The name filter is a case-insensitive regular expression of the escaped text (`Regex.Escape`), PostgreSQL's `ILIKE`. The other readers find one document and map it:

@@member extra:standard-a/src/Boilerplate.Infrastructure/Persistence/Mongo/Orders/OrderReader.cs::OrderReader::FindAsync@@

## MongoExceptionHandler

File `Persistence/Mongo/MongoExceptionHandler.cs`. A duplicate key (11000) becomes the same `DuplicateException` as with PostgreSQL, named after the index:

@@members extra:standard-a/src/Boilerplate.Infrastructure/Persistence/Mongo/MongoExceptionHandler.cs::MongoExceptionHandler@@

Inside a transaction a duplicate may come back as a command error rather than a write error, hence both branches.

## Indexes

File `Persistence/Mongo/MongoIndexes.cs`. Unique indexes are the rules the PostgreSQL configurations declare with `IsUnique`; with tenancy each starts with `tenantId`:

@@members extra:standard-a/src/Boilerplate.Infrastructure/Persistence/Mongo/MongoIndexes.cs::MongoIndexes@@

Creating an index that already exists with the same keys and options does nothing, so `EnsureAsync` is safe to repeat. Changing an index's options needs a drop by hand. A unique index cannot be built over documents that already break it: the Api then fails to start, naming the duplicate.

File `Persistence/Mongo/MongoIndexesService.cs`, a hosted service that ensures them at start in Development only:

@@members extra:standard-a/src/Boilerplate.Infrastructure/Persistence/Mongo/MongoIndexesService.cs::MongoIndexesService@@

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

@@members extra:standard-a/src/Boilerplate.Infrastructure/Messaging/Outbox/OutboxRepository.cs::OutboxRepository@@

- The claim writes `ClaimedAt` so that the document is locked by the worker's transaction. A second worker that picks the same document gets a transient write conflict instead of PostgreSQL's `skip locked`; the catch above handles it.
- `RecordFailureAsync` runs outside the rolled-back session, so the attempt is counted even though the consumer's work was undone.
- With `events.transport` = `rabbitmq` there is one document per event and no consumer or attempt limit, as on `rabbitmq.md`. Not built yet.

## Tests

`test/{Name}.Tests/Infrastructure/Persistence/Mongo/MongoClassMapsTests.cs`, the traps the class maps exist for: the driver's default Guid representation has changed across versions, so the bytes are pinned, and the lines must survive a round trip:

@@members extra:standard-a/test/Boilerplate.Tests/Infrastructure/Persistence/Mongo/MongoClassMapsTests.cs::MongoClassMapsTests@@

`test/{Name}.Tests/Infrastructure/Persistence/Mongo/MongoRepositoryRulesTests.cs`, the architecture test for tenancy: no repository reaches the collection except through the base.

@@members extra:standard-a/test/Boilerplate.Tests/Infrastructure/Persistence/Mongo/MongoRepositoryRulesTests.cs::MongoRepositoryRulesTests@@

`SolutionFolder.Find()`, file `test/{Name}.Tests/TestSupport/SolutionFolder.cs`, walks up from `AppContext.BaseDirectory` to the folder holding `{Name}.slnx`:

@@members extra:standard-a/test/Boilerplate.Tests/TestSupport/SolutionFolder.cs::SolutionFolder@@

`OutboxRepository` is outside the rule: the outbox is not tenant-owned. With `queries` = `split`, `test/{Name}.Tests/Infrastructure/Persistence/Mongo/ProductReaderTests.cs` renders the filter and the sort:

@@member extra:standard-a/test/Boilerplate.Tests/Infrastructure/Persistence/Mongo/ProductReaderTests.cs::ProductReaderTests::Filtered_Should_MatchTheNameIgnoringCaseWithTheTextEscapedAndTheCategory@@

### Integration tests

With `testing` at `integration` or above, the integration project of `testing-levels.md` runs against a MongoDB replica set: `MongoFixture` and `MongoCollection` replace `PostgresFixture` and `PostgresCollection`, the variable is `MORTIS_TEST_MONGO` (a connection string, or `skip`), and `OutboxRows` is not written. The fixture starts `mongo:7` with Testcontainers' replica set and gives each test class its own database:

@@member extra:standard-a/test/Boilerplate.IntegrationTests/TestSupport/MongoFixture.cs::MongoFixture::InitializeAsync@@

@@member extra:standard-a/test/Boilerplate.IntegrationTests/TestSupport/MongoFixture.cs::MongoFixture::CreateDatabase@@

- The container's connection string carries credentials and no database; renaming the database moves the authentication source with it, so it is set back to `admin`. `MongoUrlBuilder.ToString()` masks the credentials: use `ToMongoUrl().Url`.
- `IntegrationConstants` holds `MongoVariable = "MORTIS_TEST_MONGO"`, `MongoImage = "mongo:7"`, `AuthenticationDatabase = "admin"`, `MongoCollection = "mongo"`, `DatabasePrefix` and `SkipValue`.
- `IntegrationServices.CountAsync<TDocument>(collectionName, predicate)` counts documents in place of rows, after `MongoIndexes.EnsureAsync()`; a test that relies on a unique index ensures the indexes first.
- `ProductRepositoryTests` and `UnitOfWorkTests` keep their PostgreSQL meaning (a duplicate sku is a `DuplicateException` and one document; a rolled-back create leaves no document):

@@members extra:standard-a/test/Boilerplate.IntegrationTests/Persistence/ProductRepositoryTests.cs::ProductRepositoryTests@@

@@members extra:standard-a/test/Boilerplate.IntegrationTests/Persistence/UnitOfWorkTests.cs::UnitOfWorkTests@@

- `OutboxClaimTests` inserts two due documents (the class map's element names; `_id` and `eventId` standard UUIDs) and claims in two open transactions: the second claim either takes the other document or fails with the `TransientTransactionError` label, never both on one:

@@members extra:standard-a/test/Boilerplate.IntegrationTests/Messaging/OutboxClaimTests.cs::OutboxClaimTests@@

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

@@usings infrastructure-mongo.md@@

## Decision record

`docs/adr/0017-aggregates-as-documents-in-mongodb.md`, in `project-docs-adr.md`.
