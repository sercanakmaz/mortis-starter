# Soft delete, C#

Applies when `addons` contains `soft-delete`. Projects `{Name}.Shared` (folder `Domain/`), `{Name}.Infrastructure` (folder `Persistence/`) and, with the example, the product slice. Layer 1: `options/addons.md`, `architecture/advanced-addons.md`.

**Not built or run yet** (decision 50): hand-written snippets; no variant builds this add-on. Phase 2 compiles and proves this page. `docs/unverified.md` lists it.

## How it fits together

```
aggregate : ISoftDeletable    IsDeleted, DeletedAt, DeletedUserId; Delete(user) and Restore(user) raise Deleted / Restored
AppDbContext                  a named query filter "SoftDelete": !IsDeleted, next to the tenant filter
unique indexes                filtered: "is_deleted = false", so a deleted row frees its key (a SKU can be reused)
restore                       the repository finds the row with the SoftDelete filter ignored (the tenant filter stays)
DELETE /products/{id}         DeleteProductCommand -> 204;  POST /products/{id}/restore -> RestoreProductCommand -> 200
```

- Nothing is ever removed by a command; a row leaves only through a retention job the project writes itself.
- EF Core 10's named filters let the soft-delete filter be ignored alone: `IgnoreQueryFilters([SoftDeleteFilter])` keeps the tenant filter on. Never call `IgnoreQueryFilters()` without a name.
- Restoring a row whose key was reused meanwhile violates the filtered unique index: the duplicate translation of `failures.md` answers 409 `duplicate`.
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

`AppDbContext.OnModelCreating`, next to the tenant filter loop:

```csharp
        foreach (var entityType in modelBuilder.Model.GetEntityTypes().Where(entityType => typeof(ISoftDeletable).IsAssignableFrom(entityType.ClrType)).ToList())
        {
            GetType().GetMethod(nameof(FilterDeleted), BindingFlags.NonPublic | BindingFlags.Static)!.MakeGenericMethod(entityType.ClrType).Invoke(null, [modelBuilder]);
        }
```

```csharp
    private static void FilterDeleted<TEntity>(ModelBuilder modelBuilder) where TEntity : class, ISoftDeletable
    {
        modelBuilder.Entity<TEntity>().HasQueryFilter(PersistenceConstants.SoftDeleteFilter, entity => !entity.IsDeleted);
        modelBuilder.Entity<TEntity>().HasIndex(entity => entity.IsDeleted);
    }
```

With soft delete the tenant filter is named too (`PersistenceConstants.TenantFilter`), because EF Core 10 allows one unnamed filter or any number of named ones. `PersistenceConstants` gains `SoftDeleteFilter = "SoftDelete"`, `TenantFilter = "Tenant"` and `NotDeletedSql = "is_deleted = false"`.

`ProductConfiguration` replaces the alternate key on the SKU with a filtered unique index:

```csharp
        builder.HasIndex(product => new { product.TenantId, product.Sku }).IsUnique().HasFilter(PersistenceConstants.NotDeletedSql);
```

(without tenancy, `product => product.Sku`). An aggregate whose key another table references keeps that reference to the primary key; only natural keys become filtered indexes.

## Restore

`IProductRepository` gains `Task<Product> FindByIdIncludingDeletedTrackingAsync(Guid id);`, in `ProductRepository`:

```csharp
    public Task<Product> FindByIdIncludingDeletedTrackingAsync(Guid id)
    {
        return context.Set<Product>().IgnoreQueryFilters([PersistenceConstants.SoftDeleteFilter]).SingleOrDefaultAsync(product => product.Id == id);
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

        return CommandResult.FromContent(ProductDto.Map(product));
    }
}
```

`RestoreProductCommand.cs` is the same with `FindByIdIncludingDeletedTrackingAsync` and `product.Restore(command.Context.User)`. Both are registered in `ApplicationModule`. `ProductsController`:

```csharp
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete([FromRoute] Guid id)
    {
        await commandDispatcher.Send(new DeleteProductCommand(id));

        return NoContent();
    }

    [HttpPost("{id:guid}/restore")]
    public async Task<IActionResult> Restore([FromRoute] Guid id)
    {
        var commandResult = await commandDispatcher.Send(new RestoreProductCommand(id));

        return Ok(commandResult.Content);
    }
```

A deleted product's stock reactions: `DecreaseProductStockCommand` finds nothing and fails with `not_found`, so an order for a deleted product is refused by the reaction, as for an unknown SKU.

## Tests

`ProductTests` gains: `Delete` sets the three members and raises `Deleted` once (a second `Delete` raises nothing); `Restore` clears them and raises `Restored`. `DeleteProductCommandHandlerTests` and `RestoreProductCommandHandlerTests` in the style of `BlockCustomerCommandHandlerTests`. `ProductConfigurationTests` gains: the SKU index is unique and filtered on `is_deleted = false`, and the entity has the `SoftDelete` filter. At `testing` = `integration` and above, `SoftDeleteTests` in the integration project proves the filter and the reuse against PostgreSQL. `scripts/conformance/soft-delete.sh` proves it over HTTP.

## Files this add-on changes

`Product.cs`, `IProductRepository.cs`, `ProductRepository.cs`, `ProductConfiguration.cs`, `ProductsController.cs`, `ApplicationModule.cs`, `AppDbContext.cs`, `PersistenceConstants.cs`; a migration adds the three columns, the index and the filtered unique index.

## Namespaces and usings

| File | Namespace | Usings |
|---|---|---|
| `ISoftDeletable.cs` | `{Name}.Shared.Domain` | none |
| `Deleted.cs`, `Restored.cs` | `{Name}.Domain.Aggregates.Products.Events` | `{Name}.Shared.Domain` |
| `DeleteProductCommand.cs`, `RestoreProductCommand.cs` | `{Name}.Application.Products` | `System.Text.Json.Serialization`, `{Name}.Domain.Aggregates.Products`, `{Name}.Shared.Application`, `{Name}.Shared.Exceptions` |

## Decision record

`docs/adr/0024-soft-delete.md`, in `project-docs-adr.md`.
