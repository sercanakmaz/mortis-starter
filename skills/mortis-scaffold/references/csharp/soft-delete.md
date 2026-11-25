# Soft delete, C#

Applies when `addons` contains `soft-delete`. Projects `{Name}.Shared` (folder `Domain/`), `{Name}.Infrastructure` (folder `Persistence/`) and, with the example, the product slice. Layer 1: `options/addons.md`, `architecture/advanced-addons.md`.

**Built** in the advanced reference solution (decision 52): compiled, tested and verified there.

## How it fits together

```
aggregate : ISoftDeletable    IsDeleted, DeletedAt, DeletedUserId; Delete(user) and Restore(user) raise Deleted / Restored
AppDbContext                  a named query filter "SoftDelete": !IsDeleted, next to the tenant filter
natural keys                  a key no other table references becomes a unique index filtered on "is_deleted = false", so a deleted row frees it;
                              a referenced one (the example's SKU, referenced by order lines) stays an alternate key and stays taken
restore                       the repository finds the row with the SoftDelete filter ignored (the tenant filter stays)
DELETE /products/{id}         DeleteProductCommand -> 204;  POST /products/{id}/restore -> RestoreProductCommand -> 200
```

- Nothing is ever removed by a command; a row leaves only through a retention job the project writes itself.
- EF Core 10's named filters let the soft-delete filter be ignored alone: `IgnoreQueryFilters([SoftDeleteFilter])` keeps the tenant filter on. Never call `IgnoreQueryFilters()` without a name.
- A foreign key needs a real unique constraint, not a filtered index, so a natural key that another table references cannot be freed. Restoring a row whose freed key was reused meanwhile violates the filtered unique index: the duplicate translation of `failures.md` answers 409 `duplicate`.
- A deleted row is invisible to every command that loads by id: deleting it again or renaming it is a 404.
- Raw SQL and bulk updates skip the filter, like the tenant filter; add `is_deleted = false` by hand there.
- With MongoDB, `MongoRepository` adds `{ is_deleted: false }` to every filter of an `ISoftDeletable` collection and the unique index gets `partialFilterExpression: { is_deleted: false }`.

## ISoftDeletable

File `src/{Name}.Shared/Domain/ISoftDeletable.cs`:

```csharp
public interface ISoftDeletable
{
    bool IsDeleted { get; }
    DateTime? DeletedAt { get; }
    string DeletedUserId { get; }
}
```

The example's `Product` implements it:

```csharp
    public bool IsDeleted { get; private set; }
    public DateTime? DeletedAt { get; private set; }
    public string DeletedUserId { get; private set; }

    public void Delete(AuditUser user)
    {
        if (IsDeleted)
        {
            return;
        }

        IsDeleted = true;
        DeletedAt = DateTime.UtcNow;
        DeletedUserId = user?.Id;

        RaiseEvent(new Deleted(Id.ToString(), Sku));
    }

    public void Restore(AuditUser user)
    {
        if (!IsDeleted)
        {
            return;
        }

        IsDeleted = false;
        DeletedAt = null;
        DeletedUserId = null;
        SetUpdatedAudit(user);

        RaiseEvent(new Restored(Id.ToString(), Sku));
    }
```

`Events/Deleted.cs` and `Events/Restored.cs` are `public record Deleted(string Id, string Sku) : EventBase;` and the same for `Restored`.

## The filter and the index

`AppDbContext`, with tenancy (without it the tenant filter loop is absent and the soft-delete filter may stay unnamed):

```csharp
public class AppDbContext(DbContextOptions<AppDbContext> options, TenantScope tenantScope) : DbContext(options)
{
    private static readonly MethodInfo FilterByTenantMethod = typeof(AppDbContext).GetMethod(nameof(FilterByTenant), BindingFlags.NonPublic | BindingFlags.Instance);
    private static readonly MethodInfo FilterDeletedMethod = typeof(AppDbContext).GetMethod(nameof(FilterDeleted), BindingFlags.NonPublic | BindingFlags.Static);

    private bool TenantBypassed => tenantScope.Bypassed;
    private Guid CurrentTenantId => tenantScope.Required(PersistenceConstants.TenantOwnedData);

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        foreach (var entityType in modelBuilder.Model.GetEntityTypes().Where(entityType => typeof(IMultiTenant).IsAssignableFrom(entityType.ClrType)).ToList())
        {
            FilterByTenantMethod.MakeGenericMethod(entityType.ClrType).Invoke(this, [modelBuilder]);
        }

        foreach (var entityType in modelBuilder.Model.GetEntityTypes().Where(entityType => typeof(ISoftDeletable).IsAssignableFrom(entityType.ClrType)).ToList())
        {
            FilterDeletedMethod.MakeGenericMethod(entityType.ClrType).Invoke(null, [modelBuilder]);
        }
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        foreach (var entry in ChangeTracker.Entries<IMultiTenant>().ToList())
        {
            var tenantId = tenantScope.Required(entry.Metadata.ClrType.Name);

            if (entry.State == EntityState.Added && entry.Entity.TenantId == Guid.Empty)
            {
                entry.Property(nameof(IMultiTenant.TenantId)).CurrentValue = tenantId;
            }
            else if (entry.State != EntityState.Unchanged && entry.Entity.TenantId != tenantId && !tenantScope.Bypassed)
            {
                throw new ForbiddenException($"Writing another tenant's {entry.Metadata.ClrType.Name}");
            }
        }

        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    private void FilterByTenant<TEntity>(ModelBuilder modelBuilder) where TEntity : class, IMultiTenant
    {
        modelBuilder.Entity<TEntity>().Property(entity => entity.TenantId).IsRequired();
        modelBuilder.Entity<TEntity>().HasQueryFilter(PersistenceConstants.TenantFilter, entity => TenantBypassed || entity.TenantId == CurrentTenantId);
    }

    private static void FilterDeleted<TEntity>(ModelBuilder modelBuilder) where TEntity : class, ISoftDeletable
    {
        modelBuilder.Entity<TEntity>().HasQueryFilter(PersistenceConstants.SoftDeleteFilter, entity => !entity.IsDeleted);
        modelBuilder.Entity<TEntity>().HasIndex(entity => entity.IsDeleted);
    }
}
```

With soft delete the tenant filter is named too (`PersistenceConstants.TenantFilter`), because EF Core 10 allows one unnamed filter or any number of named ones. `PersistenceConstants` gains `public const string TenantFilter = "Tenant";` and `public const string SoftDeleteFilter = "SoftDelete";`.

An aggregate whose natural key no other table references replaces that alternate key with a filtered unique index, for example `builder.HasIndex(entity => new { entity.TenantId, entity.Code }).IsUnique().HasFilter("is_deleted = false");`. The example's `ProductConfiguration` keeps `HasAlternateKey(product => new { product.TenantId, product.Sku })`: order lines reference it.

## Restore

`IProductRepository` gains `Task<Product> FindByIdIncludingDeletedTrackingAsync(Guid id);`, in `ProductRepository` (using `{Name}.Infrastructure.Persistence` is already the namespace):

```csharp
    public Task<Product> FindByIdIncludingDeletedTrackingAsync(Guid id)
    {
        return DbSet.IgnoreQueryFilters([PersistenceConstants.SoftDeleteFilter]).SingleOrDefaultAsync(product => product.Id == id);
    }
```

File `src/{Name}.Application/Products/DeleteProductCommand.cs`:

```csharp
public record DeleteProductCommand(Guid Id) : ICommand<ProductDto>, IContextCommand, ITransactionalCommand
{
    [JsonIgnore] public CommandContext Context { get; set; }
}

public class DeleteProductCommandHandler(
    IProductRepository productRepository,
    CommandPipeline<DeleteProductCommand> commandPipeline) : BaseCommandHandler<DeleteProductCommand, ProductDto>(commandPipeline)
{
    protected override async Task<CommandResult<ProductDto>> OnHandle(DeleteProductCommand command)
    {
        var product = await productRepository.FindByIdTrackingAsync(command.Id)
            ?? throw new NotFoundException(nameof(Product));

        product.Delete(command.Context.User);

        await productRepository.UpdateAsync(product);

        return CommandResult.FromContent(ProductDto.Map(product, command.Context.Language));
    }
}
```

`RestoreProductCommand.cs` is the same with `FindByIdIncludingDeletedTrackingAsync` and `product.Restore(command.Context.User)`. Both are registered in `ApplicationModule` (`services.AddCommandHandler<DeleteProductCommand, ProductDto, DeleteProductCommandHandler>();` and the same for restore). `ProductsController` binds them from the route, so `ContextCommandFilter` gives them the caller's context:

```csharp
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete([FromRoute] DeleteProductCommand command)
    {
        await commandDispatcher.Send(command);

        return NoContent();
    }

    [HttpPost("{id:guid}/restore")]
    public async Task<IActionResult> Restore([FromRoute] RestoreProductCommand command)
    {
        var commandResult = await commandDispatcher.Send(command);

        return Ok(commandResult.Content);
    }
```

A command built inside the action (`new DeleteProductCommand(id)`) would have no context: no user and, with tenancy, no tenant. A deleted product's stock reactions: `DecreaseProductStockCommand` finds nothing and fails with `not_found`, so an order for a deleted product is refused by the reaction, as for an unknown SKU.

## Tests

`ProductTests` gains:

```csharp
[Fact]
public void Delete_Should_MarkTheProductDeletedAndRaiseDeletedOnce()
{
    var product = FakeProduct.Create("SKU-1", 10);
    product.ClearEvents();

    product.Delete(FakeAuditUser.Create());
    product.Delete(FakeAuditUser.Create());

    Assert.Equal((true, FakeAuditUser.Id), (product.IsDeleted, product.DeletedUserId));
    Assert.NotNull(product.DeletedAt);
    Assert.IsType<Deleted>(Assert.Single(product.DomainEvents));
}
```

```csharp
[Fact]
public void Restore_Should_ClearTheDeletionAndRaiseRestored()
{
    var product = FakeProduct.Create("SKU-1", 10);
    product.Delete(FakeAuditUser.Create());
    product.ClearEvents();

    product.Restore(FakeAuditUser.Create());

    Assert.Equal((false, null, null), (product.IsDeleted, product.DeletedAt, product.DeletedUserId));
    Assert.IsType<Restored>(Assert.Single(product.DomainEvents));
}
```

`ProductRepositoryBuilder` gains `WithFindByIdIncludingDeletedTrackingAsync`, like `WithFindByIdTrackingAsync`. `test/{Name}.Tests/Application/Products/DeleteProductCommandHandlerTests.cs`:

```csharp
public class DeleteProductCommandHandlerTests
```

```csharp
[Fact]
public async Task Handle_Should_MarkTheProductDeletedByTheCaller()
{
    var product = FakeProduct.Create("SKU-1", 10);
    var commandHandler = new DeleteProductCommandHandler(ProductRepositoryBuilder.Create().WithFindByIdTrackingAsync(product).WithUpdateAsync().Build(), CommandPipelineBuilder<DeleteProductCommand>.Create().Build());

    await commandHandler.Handle(new DeleteProductCommand(product.Id) { Context = new CommandContext { User = new AuditUser("user-1", "user-1@localhost") } });

    Assert.Equal((true, "user-1"), (product.IsDeleted, product.DeletedUserId));
}
```

```csharp
[Fact]
public async Task Handle_WithAnUnknownOrDeletedProduct_Should_ThrowNotFound()
{
    var commandHandler = new DeleteProductCommandHandler(ProductRepositoryBuilder.Create().WithFindByIdTrackingAsync(null).Build(), CommandPipelineBuilder<DeleteProductCommand>.Create().Build());

    await Assert.ThrowsAsync<NotFoundException>(() => commandHandler.Handle(new DeleteProductCommand(Guid.CreateVersion7())));
}
```

`RestoreProductCommandHandlerTests.cs`:

```csharp
public class RestoreProductCommandHandlerTests
```

```csharp
[Fact]
public async Task Handle_Should_FindTheProductAmongTheDeletedAndRestoreIt()
{
    var product = FakeProduct.Create("SKU-1", 10);
    product.Delete(new AuditUser("user-1", "user-1@localhost"));
    var commandHandler = new RestoreProductCommandHandler(ProductRepositoryBuilder.Create().WithFindByIdIncludingDeletedTrackingAsync(product).WithUpdateAsync().Build(), CommandPipelineBuilder<RestoreProductCommand>.Create().Build());

    var commandResult = await commandHandler.Handle(new RestoreProductCommand(product.Id) { Context = new CommandContext { User = new AuditUser("user-2", "user-2@localhost") } });

    Assert.False(product.IsDeleted);
    Assert.Equal(("SKU-1", "user-2"), (commandResult.Content.Sku, product.UpdatedUserId));
}
```

```csharp
[Fact]
public async Task Handle_WithAnUnknownProduct_Should_ThrowNotFound()
{
    var commandHandler = new RestoreProductCommandHandler(ProductRepositoryBuilder.Create().WithFindByIdIncludingDeletedTrackingAsync(null).Build(), CommandPipelineBuilder<RestoreProductCommand>.Create().Build());

    await Assert.ThrowsAsync<NotFoundException>(() => commandHandler.Handle(new RestoreProductCommand(Guid.CreateVersion7())));
}
```

`ProductConfigurationTests` gains (using `{Name}.Infrastructure.Persistence`):

```csharp
[Fact]
public void Model_Should_FilterDeletedProductsByANamedFilterNextToTheTenantFilter()
{
    var entityType = AppDbContextBuilder.Create().Build().Model.FindEntityType(typeof(Product));

    var filterNames = entityType.GetDeclaredQueryFilters().Select(queryFilter => queryFilter.Key);

    Assert.Equal([PersistenceConstants.TenantFilter, PersistenceConstants.SoftDeleteFilter], filterNames.Order(StringComparer.Ordinal).Reverse());
}
```

`scripts/conformance/soft-delete.sh` proves the rest over HTTP against PostgreSQL. With an architecture level, regenerate the event-contract golden file only if `Deleted` or `Restored` is made a queue event.

## Files this add-on changes

`Product.cs`, `IProductRepository.cs`, `ProductRepository.cs`, `ProductsController.cs`, `ApplicationModule.cs`, `AppDbContext.cs`, `PersistenceConstants.cs`; regenerate the `Initial` migration for the three columns and the index.

## Namespaces and usings

| File | Namespace | Usings |
|---|---|---|
| `ISoftDeletable.cs` | `{Name}.Shared.Domain` | none |
| `Deleted.cs`, `Restored.cs` | `{Name}.Domain.Aggregates.Products.Events` | `{Name}.Shared.Domain` |
| `DeleteProductCommandHandlerTests.cs`, `RestoreProductCommandHandlerTests.cs` | `{Name}.Tests.Application.Products` | `{Name}.Application.Products`, `{Name}.Shared.Application`, `{Name}.Shared.Domain`, `{Name}.Shared.Exceptions`, `{Name}.Tests.TestSupport.Domain.Products`, `{Name}.Tests.TestSupport.Shared.Application` |
| `DeleteProductCommand.cs`, `RestoreProductCommand.cs` | `{Name}.Application.Products` | `System.Text.Json.Serialization`, `{Name}.Domain.Aggregates.Products`, `{Name}.Shared.Application`, `{Name}.Shared.Exceptions` |

## Decision record

`docs/adr/0024-soft-delete.md`, in `project-docs-adr.md`.
