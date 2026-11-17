# Infrastructure: PostgreSQL with EF Core, C#

Project `{Name}.Infrastructure`. Applies when `database` is `postgres`. Layer 1: `building-blocks.md` sections Repository interface and Unit of work; `request-lifecycle.md` "The commit order".

## Rules

- **Writes flush immediately.** Each repository write ends with `SaveChangesAsync`, so a later read in the same command sees it and a database error surfaces at the line that caused it.
- **Tracking.** `FindByIdAsync` is a no-tracking read; `FindByIdTrackingAsync` is the read you change and save. `UpdateAsync` refuses an aggregate that is not tracked.
- **Ids come from the program**, `ValueGeneratedNever()`, for every `Guid` key and every child entity: with database-generated keys EF treats a new child that already has an id as an existing row. **An aggregate with an `int` or `long` key** is the one exception: the database assigns it (`IdentityAggregateConfiguration`), so its id exists only after `CreateAsync`, which flushes at once.
- **Child entities** are mapped with `HasMany(...).WithOne()` plus `Navigation(...).AutoInclude()`: an aggregate always loads whole. No `DbSet` for a child.
- **Value objects** are mapped with `ComplexProperty(...)` and `IsRequired()`.
- **References to other aggregates** are foreign keys with an explicit delete behaviour, `Restrict` unless the child belongs to the parent.
- **Uniqueness** is a unique index (or an alternate key). The database refuses the second row and `DatabaseExceptionHandler` turns it into `DuplicateException`. A handler never checks "exists?" first.
- **Concurrency.** An aggregate whose row is written by concurrent async handlers maps PostgreSQL's `xmin` as its row version.
- **One scope, one unit of work.**
- **No views, functions or triggers** in the database.

## AppDbContext

File `Persistence/AppDbContext.cs`:

@@decl src/Boilerplate.Infrastructure/Persistence/AppDbContext.cs::AppDbContext@@

@@member src/Boilerplate.Infrastructure/Persistence/AppDbContext.cs::AppDbContext::OnModelCreating@@

## PersistenceConstants

File `Persistence/PersistenceConstants.cs`. `{database}` is `{Name}` in lower case with dots removed.

@@decl src/Boilerplate.Infrastructure/Persistence/PersistenceConstants.cs::PersistenceConstants@@

@@member src/Boilerplate.Infrastructure/Persistence/PersistenceConstants.cs::PersistenceConstants::ConnectionStringName@@

@@member src/Boilerplate.Infrastructure/Persistence/PersistenceConstants.cs::PersistenceConstants::ConnectionStringVariable@@

@@member src/Boilerplate.Infrastructure/Persistence/PersistenceConstants.cs::PersistenceConstants::DesignTimeConnectionString@@

@@member src/Boilerplate.Infrastructure/Persistence/PersistenceConstants.cs::PersistenceConstants::UserIdMaxLength@@

@@member src/Boilerplate.Infrastructure/Persistence/PersistenceConstants.cs::PersistenceConstants::UserNameMaxLength@@

@@member src/Boilerplate.Infrastructure/Persistence/PersistenceConstants.cs::PersistenceConstants::RowVersionColumn@@

## EfRepository

File `Persistence/EfRepository.cs`, the base of every repository:

@@decl src/Boilerplate.Infrastructure/Persistence/EfRepository.cs::EfRepository@@

@@member src/Boilerplate.Infrastructure/Persistence/EfRepository.cs::EfRepository::DbSet@@

@@member src/Boilerplate.Infrastructure/Persistence/EfRepository.cs::EfRepository::CreateAsync@@

@@member src/Boilerplate.Infrastructure/Persistence/EfRepository.cs::EfRepository::UpdateAsync@@

@@member src/Boilerplate.Infrastructure/Persistence/EfRepository.cs::EfRepository::DeleteAsync@@

@@member src/Boilerplate.Infrastructure/Persistence/EfRepository.cs::EfRepository::FindByIdAsync@@

@@member src/Boilerplate.Infrastructure/Persistence/EfRepository.cs::EfRepository::FindByIdTrackingAsync@@

@@member src/Boilerplate.Infrastructure/Persistence/EfRepository.cs::EfRepository::Query@@

@@member src/Boilerplate.Infrastructure/Persistence/EfRepository.cs::EfRepository::RaiseEvents@@

@@member src/Boilerplate.Infrastructure/Persistence/EfRepository.cs::EfRepository::EnsureTransaction@@

- Events are taken off the aggregate at the start of a write. An event raised after the write is never handled.

## UnitOfWork

File `Persistence/UnitOfWork.cs`. Needs `using Microsoft.EntityFrameworkCore.Storage;`.

@@decl src/Boilerplate.Infrastructure/Persistence/UnitOfWork.cs::UnitOfWork@@

Fields: `private readonly List<Func<Task>> _committedActions = [];`, `private IDbContextTransaction _transaction;`, `private string _transactionKey;`.

@@member src/Boilerplate.Infrastructure/Persistence/UnitOfWork.cs::UnitOfWork::HasTransaction@@

@@member src/Boilerplate.Infrastructure/Persistence/UnitOfWork.cs::UnitOfWork::SaveChangesAsync@@

@@member src/Boilerplate.Infrastructure/Persistence/UnitOfWork.cs::UnitOfWork::BeginTransactionAsync@@

@@member src/Boilerplate.Infrastructure/Persistence/UnitOfWork.cs::UnitOfWork::CommitTransactionAsync@@

@@member src/Boilerplate.Infrastructure/Persistence/UnitOfWork.cs::UnitOfWork::RollbackTransactionAsync@@

@@member src/Boilerplate.Infrastructure/Persistence/UnitOfWork.cs::UnitOfWork::OnCommitted@@

- Commit order: save, sync handlers (`EventHandlerExecutor`), `COMMIT`, queue events to every `IEventDispatcher`, after-commit actions. A dispatcher's failure is logged and never undoes the commit.
- Only the caller that began the transaction (same key) commits it.

## DatabaseExceptionHandler

File `Persistence/DatabaseExceptionHandler.cs`, only when `exceptions` is `coded` or `catalogued` (`failures.md`). Needs `using Npgsql;`.

@@decl src/Boilerplate.Infrastructure/Persistence/DatabaseExceptionHandler.cs::DatabaseExceptionHandler@@

@@member src/Boilerplate.Infrastructure/Persistence/DatabaseExceptionHandler.cs::DatabaseExceptionHandler::Handle@@

## ConfigurationExtensions

File `Persistence/ConfigurationExtensions.cs`. Every aggregate and child configuration starts with one of these.

@@decl src/Boilerplate.Infrastructure/Persistence/ConfigurationExtensions.cs::ConfigurationExtensions@@

@@member src/Boilerplate.Infrastructure/Persistence/ConfigurationExtensions.cs::ConfigurationExtensions::AggregateConfiguration@@

@@member src/Boilerplate.Infrastructure/Persistence/ConfigurationExtensions.cs::ConfigurationExtensions::IdentityAggregateConfiguration@@

@@member src/Boilerplate.Infrastructure/Persistence/ConfigurationExtensions.cs::ConfigurationExtensions::ChildEntityConfiguration@@

## DesignTimeDbContextFactory

File `Persistence/DesignTimeDbContextFactory.cs`, used only by `dotnet ef`:

@@decl src/Boilerplate.Infrastructure/Persistence/DesignTimeDbContextFactory.cs::DesignTimeDbContextFactory@@

@@member src/Boilerplate.Infrastructure/Persistence/DesignTimeDbContextFactory.cs::DesignTimeDbContextFactory::CreateDbContext@@

## Registration

File `Persistence/PersistenceModule.cs`: one line per repository, nothing else.

@@decl src/Boilerplate.Infrastructure/Persistence/PersistenceModule.cs::PersistenceModule@@

```csharp
public static IServiceCollection AddPersistenceModule(this IServiceCollection services)
{
    services.AddScoped<I{Aggregate}Repository, {Aggregate}Repository>();

    return services;
}
```

Written first with an empty body (only `return services;`), and a line added per repository.

File `InfrastructureModule.cs` at the project root:

@@decl src/Boilerplate.Infrastructure/InfrastructureModule.cs::InfrastructureModule@@

@@member src/Boilerplate.Infrastructure/InfrastructureModule.cs::InfrastructureModule::AddInfrastructureModule@@

When `naming.database` is `as-code`, both this file and `DesignTimeDbContextFactory` leave out `.UseSnakeCaseNamingConvention()`.

## Logging middleware

File `Logging/LoggingCommandHandlerMiddleware.cs`:

@@decl src/Boilerplate.Infrastructure/Logging/LoggingCommandHandlerMiddleware.cs::LoggingCommandHandlerMiddleware@@

@@member src/Boilerplate.Infrastructure/Logging/LoggingCommandHandlerMiddleware.cs::LoggingCommandHandlerMiddleware::Before@@

@@member src/Boilerplate.Infrastructure/Logging/LoggingCommandHandlerMiddleware.cs::LoggingCommandHandlerMiddleware::After@@

## Per aggregate: repository

File `Persistence/{Plural}/{Aggregate}Repository.cs`. Inherits everything; adds only the queries the domain's interface declares.

```csharp
public class {Aggregate}Repository(AppDbContext context, IUnitOfWork unitOfWork, EventContext eventContext) : EfRepository<{Aggregate}, Guid>(context, unitOfWork, eventContext), I{Aggregate}Repository
```

A tracking query by another key, as the product repository has it:

@@member src/Boilerplate.Infrastructure/Persistence/Products/ProductRepository.cs::ProductRepository::FindBySkuTrackingAsync@@

A read query uses `Query()` (no tracking); a query whose result will be changed uses `DbSet`.

## Per aggregate: mapping

File `Persistence/{Plural}/{Aggregate}Configuration.cs`. One `IEntityTypeConfiguration<T>` per aggregate and per child entity, in the same file. The order inside `Configure` is: the base configuration, properties, indexes, relations, navigations, row version, table.

@@member src/Boilerplate.Infrastructure/Persistence/Orders/OrderConfiguration.cs::OrderConfiguration::Configure@@

A child entity with a value object:

@@member src/Boilerplate.Infrastructure/Persistence/Orders/OrderConfiguration.cs::OrderLineConfiguration::Configure@@

An aggregate written by concurrent async handlers, with a check constraint:

@@member src/Boilerplate.Infrastructure/Persistence/Products/ProductConfiguration.cs::ProductConfiguration::Configure@@

- Every string has a maximum length from the aggregate's constants.
- Table names are explicit and follow `naming.database`.

## Migrations

Never written by hand. After the mappings exist, from the solution root:

```bash
dotnet tool install --global dotnet-ef --version 10.0.15
dotnet ef migrations add Initial --project src/{Name}.Infrastructure --output-dir Persistence/Migrations
```

This writes `*_Initial.cs`, `*_Initial.Designer.cs` and `AppDbContextModelSnapshot.cs`. A project without an aggregate has no migration. Hosts never migrate; `dotnet ef database update --project src/{Name}.Infrastructure` does.

## Tests

`{Aggregate}ConfigurationTests` and `{Aggregate}RepositoryTests` per aggregate, in `worked-slice.md`.

## Namespaces and usings

Every file of this page, with its namespace and the `using` lines it needs (`ImplicitUsings` covers `System`, `System.Linq`, `System.Threading.Tasks` and the like). A file the table does not list follows the namespace rule in `naming.md`.

@@usings infrastructure-postgres.md@@
