# Persistence

EF Core and PostgreSQL, all in `src/Infrastructure/Persistence/`. Domain and Application never see EF Core.

## The pieces

| File | What it does |
|---|---|
| `AppDbContext.cs` | The one `DbContext`. Applies every `IEntityTypeConfiguration` in the assembly. Snake_case names are set where it is registered, in `InfrastructureModule.cs`. |
| `EfRepository.cs` | The base class of every repository: `CreateAsync`, `UpdateAsync`, `DeleteAsync`, `FindByIdAsync` (no tracking), `FindByIdTrackingAsync`. Each write drains the aggregate's events into `EventContext` and ends with `SaveChangesAsync`. |
| `UnitOfWork.cs` | One per DI scope. Begin, commit and roll back the transaction, run sync handlers before COMMIT, publish queue events after it, run `OnCommitted` actions. |
| `ConfigurationExtensions.cs` | `AggregateConfiguration` and `ChildEntityConfiguration`: key, `ValueGeneratedNever()`, audit columns and their lengths. |
| `DatabaseExceptionHandler.cs` | Turns PostgreSQL errors into domain exceptions: unique violation → `DuplicateException` (409), foreign key violation → `ReferenceViolationException` (400). |
| `DesignTimeDbContextFactory.cs` | What `dotnet ef` uses; reads `ConnectionStrings__Default`. |
| `PersistenceConstants.cs`, `PersistenceModule.cs` | Column lengths and names; `AddPersistenceModule()` registers the repositories. |

Per aggregate, in `Persistence/<Plural>/`: `<Aggregate>Repository.cs` (extends `EfRepository`, implements the domain interface) and `<Aggregate>Configuration.cs`.

## Behaviour to know

- **Writes flush immediately**, so a later query in the same command sees them and a database error surfaces at the line that caused it. Statement order inside a service is behaviour.
- **`UpdateAsync` refuses an untracked aggregate.** Load with `FindByIdTrackingAsync` first. A write outside a transaction throws `TransactionRequiredException`.
- **Ids come from the domain**, never the database. With generated keys EF would treat a new child that already has an id as an existing row.
- **Children** are mapped with `HasMany(...).WithOne()` and `AutoInclude()`, so an aggregate always loads whole; they have no `DbSet`. **Value objects** use `ComplexProperty(...).IsRequired()`.
- **Foreign keys name their delete behaviour** (`order_lines.sku → products.sku` is `Restrict`). Do not repeat in the service what a foreign key guarantees.
- **Concurrency:** an aggregate that async handlers write at the same time needs a token. `Product` maps PostgreSQL's `xmin` as a row version (`PersistenceConstants.RowVersionColumn`); the async dispatcher retries a `DbUpdateConcurrencyException` up to `MessagingConstants.MaxAttempts` times. Npgsql does not put `xmin` in the migration.
- **A check constraint** (`ck_products_stock_not_negative`) backs a domain rule. It is only visible in EF's design-time model, which is how the configuration test reads it.
- **No views or functions in the database.** A join is SQL in a `*Constants` class run by the repository.

## Migrations

```bash
docker compose up -d postgres
dotnet ef migrations add <Name> --project src/<Name>.Infrastructure --output-dir Persistence/Migrations
dotnet ef database update --project src/<Name>.Infrastructure
```

Build first and pass `--no-build` when the build is already fresh. The Console and the Api never migrate on their own. In the skill's example, the migration is generated rather than hand-written; [template-internals.md](template-internals.md) says how to regenerate it.
