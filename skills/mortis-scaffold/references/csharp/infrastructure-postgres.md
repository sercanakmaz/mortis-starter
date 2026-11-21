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
- **Concurrency.** An aggregate whose row is written by concurrent event consumers maps PostgreSQL's `xmin` as its row version.
- **One scope, one unit of work.**
- **No views, functions or triggers** in the database.

## AppDbContext

File `Persistence/AppDbContext.cs`:

```csharp
public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
```

```csharp
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
}
```

## PersistenceConstants

File `Persistence/PersistenceConstants.cs`. `{database}` is `{Name}` in lower case with dots removed.

```csharp
public static class PersistenceConstants
```

```csharp
public const string ConnectionStringName = "Default";
```

```csharp
public const string ConnectionStringVariable = "ConnectionStrings__Default";
```

```csharp
public const string DesignTimeConnectionString = "Host=localhost;Port=5432;Database={database};Username=postgres;Password=postgres";
```

```csharp
public const int UserIdMaxLength = 64;
```

```csharp
public const int UserNameMaxLength = 256;
```

```csharp
public const string RowVersionColumn = "xmin";
```

## EfRepository

File `Persistence/EfRepository.cs`, the base of every repository:

```csharp
public abstract class EfRepository<TAggregate, TKey>(AppDbContext context, IUnitOfWork unitOfWork, EventContext eventContext) : IRepository<TAggregate, TKey> where TAggregate : AggregateRoot<TKey>
```

```csharp
protected readonly DbSet<TAggregate> DbSet = context.Set<TAggregate>();
```

```csharp
public virtual Task CreateAsync(TAggregate aggregate)
{
    EnsureTransaction();

    DbSet.Add(aggregate);

    RaiseEvents(aggregate);

    return unitOfWork.SaveChangesAsync();
}
```

```csharp
public virtual Task UpdateAsync(TAggregate aggregate)
{
    EnsureTransaction();

    if (context.Entry(aggregate).State == EntityState.Detached)
    {
        throw new InvalidOperationException($"{typeof(TAggregate).Name} is not tracked. Load it with FindByIdTrackingAsync before UpdateAsync.");
    }

    RaiseEvents(aggregate);

    return unitOfWork.SaveChangesAsync();
}
```

```csharp
public virtual Task DeleteAsync(TAggregate aggregate)
{
    EnsureTransaction();

    DbSet.Remove(aggregate);

    RaiseEvents(aggregate);

    return unitOfWork.SaveChangesAsync();
}
```

```csharp
public virtual Task<TAggregate> FindByIdAsync(TKey id)
{
    return DbSet.AsNoTracking().FirstOrDefaultAsync(aggregate => aggregate.Id.Equals(id));
}
```

```csharp
public virtual Task<TAggregate> FindByIdTrackingAsync(TKey id)
{
    return DbSet.FirstOrDefaultAsync(aggregate => aggregate.Id.Equals(id));
}
```

```csharp
protected IQueryable<TAggregate> Query()
{
    return DbSet.AsNoTracking();
}
```

```csharp
protected void RaiseEvents(TAggregate aggregate)
{
    foreach (var @event in aggregate.DomainEvents ?? [])
    {
        eventContext.AddRaised(@event);
    }

    aggregate.ClearEvents();
}
```

```csharp
protected void EnsureTransaction()
{
    if (!unitOfWork.HasTransaction)
    {
        throw new TransactionRequiredException(typeof(TAggregate).Name);
    }
}
```

- Events are taken off the aggregate at the start of a write. An event raised after the write is never handled.

## UnitOfWork

File `Persistence/UnitOfWork.cs`. Needs `using Microsoft.EntityFrameworkCore.Storage;`.

```csharp
public class UnitOfWork(
    AppDbContext context,
    EventContext eventContext,
    EventHandlerExecutor eventHandlerExecutor,
    EventPublisher eventPublisher,
    ILogger<UnitOfWork> logger) : IUnitOfWork
```

Fields: `private readonly List<Func<Task>> _committedActions = [];`, `private IDbContextTransaction _transaction;`, `private string _transactionKey;`.

```csharp
public bool HasTransaction => _transaction is not null;
```

```csharp
public async Task<int> SaveChangesAsync()
{
    try
    {
        return await context.SaveChangesAsync();
    }
    catch (DbUpdateException exception)
    {
        DatabaseExceptionHandler.Handle(exception);

        throw;
    }
}
```

```csharp
public async Task BeginTransactionAsync(string transactionKey)
{
    if (_transaction is not null) return;

    _transaction = await context.Database.BeginTransactionAsync();
    _transactionKey = transactionKey;
}
```

```csharp
public async Task CommitTransactionAsync(string transactionKey)
{
    if (_transaction is null || transactionKey != _transactionKey) return;

    await SaveChangesAsync();

    await eventHandlerExecutor.ExecuteRaisedAsync();

    var queueEvents = new List<EventBase>();

    while (eventContext.TakeDispatched() is { } @event)
    {
        if (@event is IQueueEvent) queueEvents.Add(@event);
    }

    await _transaction.CommitAsync();
    await _transaction.DisposeAsync();

    _transaction = null;
    _transactionKey = null;

    foreach (var queueEvent in queueEvents)
    {
        try
        {
            await eventPublisher.PublishAsync(queueEvent);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Publishing {Event} {EventId} failed after the transaction was committed", queueEvent.GetType().Name, queueEvent.EventId);
        }
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

    if (_transaction is null) return;

    await _transaction.RollbackAsync();
    await _transaction.DisposeAsync();

    _transaction = null;
    _transactionKey = null;

    context.ChangeTracker.Clear();
}
```

```csharp
public void OnCommitted(Func<Task> action)
{
    _committedActions.Add(action);
}
```

- Commit order: save, sync handlers (`EventHandlerExecutor`), collect the queue events, `COMMIT`, publish each queue event with `EventPublisher`, after-commit actions. A failure after `COMMIT` is logged and never undoes the commit.
- This is the `events.outbox` = `none` version. With an outbox the constructor and `CommitTransactionAsync` differ: the rows are written before `COMMIT` (`outbox.md` "The kernel files that change"). Generate only the chosen one.
- Sync handlers run before the events are collected, so an event a sync handler raises is delivered too.
- Only the caller that began the transaction (same key) commits it.

## DatabaseExceptionHandler

File `Persistence/DatabaseExceptionHandler.cs`, only when `exceptions` is `coded` or `catalogued` (`failures.md`). Needs `using Npgsql;`.

```csharp
public static class DatabaseExceptionHandler
```

```csharp
public static void Handle(DbUpdateException exception)
{
    if (exception.InnerException is not PostgresException postgresException) return;

    switch (postgresException.SqlState)
    {
        case PostgresErrorCodes.UniqueViolation:
            throw new DuplicateException(postgresException.ConstraintName, exception);
        case PostgresErrorCodes.ForeignKeyViolation:
            throw new ReferenceViolationException(postgresException.ConstraintName, exception);
        case PostgresErrorCodes.CheckViolation:
            throw new InvalidParameterException(postgresException.ConstraintName, "a check constraint rejected the value", exception);
        case PostgresErrorCodes.NotNullViolation:
            throw new InvalidParameterException(postgresException.ColumnName, "a value is required", exception);
    }
}
```

## ConfigurationExtensions

File `Persistence/ConfigurationExtensions.cs`. Every aggregate and child configuration starts with one of these.

```csharp
public static class ConfigurationExtensions
```

```csharp
public static EntityTypeBuilder<TAggregate> AggregateConfiguration<TAggregate, TKey>(this EntityTypeBuilder<TAggregate> builder) where TAggregate : AggregateRoot<TKey>
{
    builder.HasKey(aggregate => aggregate.Id);
    builder.Property(aggregate => aggregate.Id).ValueGeneratedNever();
    builder.Property(aggregate => aggregate.CreatedUserId).HasMaxLength(PersistenceConstants.UserIdMaxLength);
    builder.Property(aggregate => aggregate.CreatedUserName).HasMaxLength(PersistenceConstants.UserNameMaxLength);
    builder.Property(aggregate => aggregate.UpdatedUserId).HasMaxLength(PersistenceConstants.UserIdMaxLength);
    builder.Property(aggregate => aggregate.UpdatedUserName).HasMaxLength(PersistenceConstants.UserNameMaxLength);

    return builder;
}
```

```csharp
public static EntityTypeBuilder<TAggregate> IdentityAggregateConfiguration<TAggregate, TKey>(this EntityTypeBuilder<TAggregate> builder) where TAggregate : AggregateRoot<TKey>
{
    builder.AggregateConfiguration<TAggregate, TKey>();
    builder.Property(aggregate => aggregate.Id).ValueGeneratedOnAdd();

    return builder;
}
```

```csharp
public static EntityTypeBuilder<TEntity> ChildEntityConfiguration<TEntity, TKey>(this EntityTypeBuilder<TEntity> builder) where TEntity : Entity<TKey>
{
    builder.HasKey(entity => entity.Id);
    builder.Property(entity => entity.Id).ValueGeneratedNever();

    return builder;
}
```

## DesignTimeDbContextFactory

File `Persistence/DesignTimeDbContextFactory.cs`, used only by `dotnet ef`:

```csharp
public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
```

```csharp
public AppDbContext CreateDbContext(string[] args)
{
    var connectionString = Environment.GetEnvironmentVariable(PersistenceConstants.ConnectionStringVariable) ?? PersistenceConstants.DesignTimeConnectionString;

    var options = new DbContextOptionsBuilder<AppDbContext>()
        .UseNpgsql(connectionString)
        .UseSnakeCaseNamingConvention()
        .Options;

    return new AppDbContext(options);
}
```

## Registration

File `Persistence/PersistenceModule.cs`: one line per repository, nothing else.

```csharp
public static class PersistenceModule
```

```csharp
public static IServiceCollection AddPersistenceModule(this IServiceCollection services)
{
    services.AddScoped<I{Aggregate}Repository, {Aggregate}Repository>();

    return services;
}
```

Written first with an empty body (only `return services;`), and a line added per repository.

File `InfrastructureModule.cs` at the project root:

```csharp
public static class InfrastructureModule
```

```csharp
public static IServiceCollection AddInfrastructureModule(this IServiceCollection services, IConfiguration configuration)
{
    services.AddDbContext<AppDbContext>(options => options
        .UseNpgsql(configuration.GetConnectionString(PersistenceConstants.ConnectionStringName))
        .UseSnakeCaseNamingConvention());

    services.AddScoped<IUnitOfWork, UnitOfWork>();
    services.AddScoped<ICommandHandlerMiddleware, LoggingCommandHandlerMiddleware>();
    services.AddSingleton<EventPublisher>();
    services.AddPersistenceModule();
    services.AddEventConsumerRegistry();

    return services;
}
```

When `naming.database` is `as-code`, both this file and `DesignTimeDbContextFactory` leave out `.UseSnakeCaseNamingConvention()`.

## Logging

The logging middleware, the JSON line formatter and the log fields are on `logging.md`.

## Per aggregate: repository

File `Persistence/{Plural}/{Aggregate}Repository.cs`. Inherits everything; adds only the queries the domain's interface declares.

```csharp
public class {Aggregate}Repository(AppDbContext context, IUnitOfWork unitOfWork, EventContext eventContext) : EfRepository<{Aggregate}, Guid>(context, unitOfWork, eventContext), I{Aggregate}Repository
```

A tracking query by another key, as the product repository has it:

```csharp
public Task<Product> FindBySkuTrackingAsync(string sku)
{
    return DbSet.FirstOrDefaultAsync(product => product.Sku == sku);
}
```

A read query uses `Query()` (no tracking); a query whose result will be changed uses `DbSet`.

## Per aggregate: mapping

File `Persistence/{Plural}/{Aggregate}Configuration.cs`. One `IEntityTypeConfiguration<T>` per aggregate and per child entity, in the same file. The order inside `Configure` is: the base configuration, properties, indexes, relations, navigations, row version, table.

```csharp
public void Configure(EntityTypeBuilder<Order> builder)
{
    builder.AggregateConfiguration<Order, Guid>();

    builder.Property(order => order.OrderNumber).HasMaxLength(OrderConstants.OrderNumberMaxLength).IsRequired();
    builder.Property(order => order.Status).HasConversion<string>().HasMaxLength(32);

    builder.HasIndex(order => order.OrderNumber).IsUnique();

    builder.HasOne<Customer>()
        .WithMany()
        .HasForeignKey(order => order.CustomerId)
        .OnDelete(DeleteBehavior.Restrict);

    builder.HasMany(order => order.Lines)
        .WithOne()
        .HasForeignKey(orderLine => orderLine.OrderId)
        .OnDelete(DeleteBehavior.Cascade);

    builder.Navigation(order => order.Lines).AutoInclude();

    builder.ToTable("orders");
}
```

A child entity with a value object:

```csharp
public void Configure(EntityTypeBuilder<OrderLine> builder)
{
    builder.ChildEntityConfiguration<OrderLine, Guid>();

    builder.Property(orderLine => orderLine.Sku).HasMaxLength(OrderConstants.SkuMaxLength).IsRequired();
    builder.Property(orderLine => orderLine.CancellationReason).HasMaxLength(OrderConstants.ReasonMaxLength);

    builder.HasOne<Product>()
        .WithMany()
        .HasForeignKey(orderLine => orderLine.Sku)
        .HasPrincipalKey(product => product.Sku)
        .OnDelete(DeleteBehavior.Restrict);

    builder.ComplexProperty(orderLine => orderLine.Price, price =>
    {
        price.IsRequired();
        price.Property(money => money.Amount).HasColumnName("price_amount").HasPrecision(18, 4);
        price.Property(money => money.Currency).HasColumnName("price_currency").HasMaxLength(OrderConstants.CurrencyLength).IsRequired();
    });

    builder.ToTable("order_lines");
}
```

An aggregate written by concurrent event consumers, with a check constraint:

```csharp
public void Configure(EntityTypeBuilder<Product> builder)
{
    builder.AggregateConfiguration<Product, Guid>();

    builder.Property(product => product.Sku).HasMaxLength(ProductConstants.SkuMaxLength).IsRequired();
    builder.Property(product => product.Name).HasMaxLength(ProductConstants.NameMaxLength).IsRequired();
    builder.Property(product => product.Stock).IsRequired();

    builder.HasAlternateKey(product => product.Sku);

    builder.HasOne<Category>()
        .WithMany()
        .HasForeignKey(product => product.CategoryId)
        .OnDelete(DeleteBehavior.Restrict);

    builder.Property<uint>(PersistenceConstants.RowVersionColumn).IsRowVersion();

    builder.ToTable("products", table => table.HasCheckConstraint("ck_products_stock_not_negative", "stock >= 0"));
}
```

- Every string has a maximum length from the aggregate's constants.
- Table names are explicit and follow `naming.database`.

## Migrations

Never written by hand. After the mappings exist, from the solution root:

```bash
dotnet tool install --global dotnet-ef --version 10.0.15
dotnet ef migrations add Initial --project src/{Name}.Infrastructure --output-dir Persistence/Migrations
```

This writes `*_Initial.cs`, `*_Initial.Designer.cs` and `AppDbContextModelSnapshot.cs`. A project with neither an aggregate nor an outbox has no migration; with an outbox the table is in `Initial`. Hosts never migrate; `dotnet ef database update --project src/{Name}.Infrastructure` does.

## Tests

`{Aggregate}ConfigurationTests` and `{Aggregate}RepositoryTests` per aggregate, in `worked-slice.md`.

## Namespaces and usings

Every file of this page, with its namespace and the `using` lines it needs (`ImplicitUsings` covers `System`, `System.Linq`, `System.Threading.Tasks` and the like). A file the table does not list follows the namespace rule in `naming.md`.

| File | Namespace | Usings |
|---|---|---|
| `src/{Name}.Infrastructure/InfrastructureModule.cs` | `{Name}.Infrastructure` | Microsoft.EntityFrameworkCore, Microsoft.Extensions.Configuration, Microsoft.Extensions.DependencyInjection, {Name}.Infrastructure.Logging, {Name}.Infrastructure.Messaging, {Name}.Infrastructure.Persistence, {Name}.Shared.Application, {Name}.Shared.Persistence |
| `src/{Name}.Infrastructure/Persistence/AppDbContext.cs` | `{Name}.Infrastructure.Persistence` | Microsoft.EntityFrameworkCore |
| `src/{Name}.Infrastructure/Persistence/Categories/CategoryConfiguration.cs` | `{Name}.Infrastructure.Persistence.Categories` | Microsoft.EntityFrameworkCore, Microsoft.EntityFrameworkCore.Metadata.Builders, {Name}.Domain.Aggregates.Categories |
| `src/{Name}.Infrastructure/Persistence/Categories/CategoryRepository.cs` | `{Name}.Infrastructure.Persistence.Categories` | {Name}.Domain.Aggregates.Categories, {Name}.Shared.Domain, {Name}.Shared.Persistence |
| `src/{Name}.Infrastructure/Persistence/ConfigurationExtensions.cs` | `{Name}.Infrastructure.Persistence` | Microsoft.EntityFrameworkCore, Microsoft.EntityFrameworkCore.Metadata.Builders, {Name}.Shared.Domain |
| `src/{Name}.Infrastructure/Persistence/Customers/CustomerConfiguration.cs` | `{Name}.Infrastructure.Persistence.Customers` | Microsoft.EntityFrameworkCore, Microsoft.EntityFrameworkCore.Metadata.Builders, {Name}.Domain.Aggregates.Customers |
| `src/{Name}.Infrastructure/Persistence/Customers/CustomerRepository.cs` | `{Name}.Infrastructure.Persistence.Customers` | {Name}.Domain.Aggregates.Customers, {Name}.Shared.Domain, {Name}.Shared.Persistence |
| `src/{Name}.Infrastructure/Persistence/DatabaseExceptionHandler.cs` | `{Name}.Infrastructure.Persistence` | Microsoft.EntityFrameworkCore, Npgsql, {Name}.Shared.Exceptions |
| `src/{Name}.Infrastructure/Persistence/DesignTimeDbContextFactory.cs` | `{Name}.Infrastructure.Persistence` | Microsoft.EntityFrameworkCore, Microsoft.EntityFrameworkCore.Design |
| `src/{Name}.Infrastructure/Persistence/EfRepository.cs` | `{Name}.Infrastructure.Persistence` | Microsoft.EntityFrameworkCore, {Name}.Shared.Domain, {Name}.Shared.Exceptions, {Name}.Shared.Persistence |
| `src/{Name}.Infrastructure/Persistence/Migrations/AppDbContextModelSnapshot.cs` | `{Name}.Infrastructure.Persistence.Migration` | Microsoft.EntityFrameworkCore, Microsoft.EntityFrameworkCore.Infrastructure, Microsoft.EntityFrameworkCore.Storage.ValueConversion, Npgsql.EntityFrameworkCore.PostgreSQL.Metadata, System, System.Collections.Generic, {Name}.Infrastructure.Persistence |
| `src/{Name}.Infrastructure/Persistence/Orders/OrderConfiguration.cs` | `{Name}.Infrastructure.Persistence.Orders` | Microsoft.EntityFrameworkCore, Microsoft.EntityFrameworkCore.Metadata.Builders, {Name}.Domain.Aggregates.Customers, {Name}.Domain.Aggregates.Orders, {Name}.Domain.Aggregates.Products |
| `src/{Name}.Infrastructure/Persistence/Orders/OrderRepository.cs` | `{Name}.Infrastructure.Persistence.Orders` | {Name}.Domain.Aggregates.Orders, {Name}.Shared.Domain, {Name}.Shared.Persistence |
| `src/{Name}.Infrastructure/Persistence/PersistenceConstants.cs` | `{Name}.Infrastructure.Persistence` | none |
| `src/{Name}.Infrastructure/Persistence/PersistenceModule.cs` | `{Name}.Infrastructure.Persistence` | Microsoft.Extensions.DependencyInjection, {Name}.Domain.Aggregates.Categories, {Name}.Domain.Aggregates.Customers, {Name}.Domain.Aggregates.Orders, {Name}.Domain.Aggregates.Products, {Name}.Infrastructure.Persistence.Categories, {Name}.Infrastructure.Persistence.Customers, {Name}.Infrastructure.Persistence.Orders, {Name}.Infrastructure.Persistence.Products |
| `src/{Name}.Infrastructure/Persistence/Products/ProductConfiguration.cs` | `{Name}.Infrastructure.Persistence.Products` | Microsoft.EntityFrameworkCore, Microsoft.EntityFrameworkCore.Metadata.Builders, {Name}.Domain.Aggregates.Categories, {Name}.Domain.Aggregates.Products |
| `src/{Name}.Infrastructure/Persistence/Products/ProductRepository.cs` | `{Name}.Infrastructure.Persistence.Products` | Microsoft.EntityFrameworkCore, {Name}.Domain.Aggregates.Products, {Name}.Shared.Domain, {Name}.Shared.Persistence |
| `src/{Name}.Infrastructure/Persistence/UnitOfWork.cs` | `{Name}.Infrastructure.Persistence` | Microsoft.EntityFrameworkCore, Microsoft.EntityFrameworkCore.Storage, Microsoft.Extensions.Logging, {Name}.Infrastructure.Messaging, {Name}.Shared.Domain, {Name}.Shared.Persistence |
