# Multi-tenancy by interface, C#

Applies when `tenancy.mode` is `interface`; it needs `auth` = `jwt`, because the tenant is the signed-in user's `tenant_id` claim (`authentication.md`). This page is the `tenancy.key` = `guid` code; `text` is at the end. Layer 1: `options/tenancy.md`, `architecture/tenancy.md`.

One database shared by all tenants. A type becomes tenant-owned by implementing `IMultiTenant`; everything else stays global and visible to every tenant. No database or schema per tenant.

## How it fits together

```
Api request     ContextCommandFilter: CommandContext.TenantId := the tenant claim; tenantScope.Enter(it)
Console run     the token's tenant claim (no token: no tenant); tenantScope.Enter(it)
event consumer  new DI scope; tenantScope.Enter(event.TenantId) BEFORE any other service is resolved
command         TenantCommandHandlerMiddleware: tenantScope.Enter(command.Context.TenantId); another tenant than the scope's -> 403
read            every IMultiTenant type has a query filter: tenant_id = the scope's tenant; no tenant -> 403 tenant_required, before any SQL
write           SaveChanges: a new IMultiTenant row gets the scope's tenant; changing or deleting another tenant's row -> 403
```

- `TenantScope` is the one ambient value of the design: one per dependency-injection scope, set by the host or the consumer, read by `AppDbContext`. Domain and Application never read it; they carry the tenant on the context and the events.
- A tenant-owned query without a tenant fails; it never returns every tenant's rows.
- Every unique index on a tenant-owned table starts with `tenant_id`, and a foreign key between tenant-owned tables includes it, so a row can never point at another tenant's row.
- A child entity of a tenant-owned aggregate is tenant-owned too and gets the same tenant.
- An event carries the tenant of the command that raised it (`EventContext`); the outbox row stores it in `tenant_id`, and the RabbitMQ envelope in the `Mortis-Tenant-Id` header.
- `TenantScope.Bypass(reason)` lifts the filter for a system job (cleanup, a report across tenants), logs a warning, and ends when disposed. It is for Infrastructure and the Worker only. `IgnoreQueryFilters()` is never used.
- Raw SQL and bulk updates (`ExecuteUpdate`, `FromSql`) skip the filter: write the `tenant_id` condition yourself, or keep them to global tables (the outbox).

## IMultiTenant

File `src/{Name}.Shared/Domain/IMultiTenant.cs`:

@@decl src/Boilerplate.Shared/Domain/IMultiTenant.cs::IMultiTenant@@

@@members src/Boilerplate.Shared/Domain/IMultiTenant.cs::IMultiTenant@@

A tenant-owned type implements it with a property the database sets, never a constructor argument:

```csharp
public class Product : AggregateRoot<Guid>, IMultiTenant
{
    public Guid TenantId { get; private set; }
```

In the example (`example-domain.md`, "when tenancy is on"): `Product`, `Customer`, `Order` and its `OrderLine` are tenant-owned; `Category` stays global.

## TenantRequiredException

File `src/{Name}.Shared/Exceptions/TenantRequiredException.cs`, answered 403 with code `tenant_required` (`failures.md`):

@@decl src/Boilerplate.Shared/Exceptions/TenantRequiredException.cs::TenantRequiredException@@

With `exceptions` = `catalogued`, `ErrorCatalogue` gets `tenant_required` (with its message `"The record belongs to a tenant, and the caller has none."`, or in the message files with `languages`).

## TenantScope

File `src/{Name}.Infrastructure/Tenancy/TenantScope.cs`, registered scoped by `AddInfrastructureModule`:

@@decl src/Boilerplate.Infrastructure/Tenancy/TenantScope.cs::TenantScope@@

@@members src/Boilerplate.Infrastructure/Tenancy/TenantScope.cs::TenantScope@@

- A tenant that is not a valid id, or a second tenant in the same scope, is refused with 403 `forbidden`.

## TenantCommandHandlerMiddleware

File `src/{Name}.Infrastructure/Tenancy/TenantCommandHandlerMiddleware.cs`, registered after the logging middleware. It makes the command's own context the source of truth inside the pipeline:

@@decl src/Boilerplate.Infrastructure/Tenancy/TenantCommandHandlerMiddleware.cs::TenantCommandHandlerMiddleware@@

@@members src/Boilerplate.Infrastructure/Tenancy/TenantCommandHandlerMiddleware.cs::TenantCommandHandlerMiddleware@@

`AddInfrastructureModule` gains:

```csharp
        services.AddScoped<ICommandHandlerMiddleware, TenantCommandHandlerMiddleware>();
        services.AddScoped<TenantScope>();
```

## AppDbContext

`AppDbContext` (`infrastructure-postgres.md`) takes the `TenantScope`. It filters every `IMultiTenant` type and stamps new rows:

@@decl variant:advanced/src/Boilerplate.Infrastructure/Persistence/AppDbContext.cs::AppDbContext@@

@@members variant:advanced/src/Boilerplate.Infrastructure/Persistence/AppDbContext.cs::AppDbContext@@

- The filter reads `CurrentTenantId` when the query runs, so a query without a tenant throws `TenantRequiredException` before EF sends SQL.
- `PersistenceConstants` gains `public const string TenantOwnedData = "Tenant-owned data";`, the name in that message.
- `DesignTimeDbContextFactory` passes `new TenantScope(NullLogger<TenantScope>.Instance)`.

## Keys and foreign keys

Each unique index and alternate key of a tenant-owned type starts with `TenantId`; a foreign key to another tenant-owned type names both columns:

```csharp
builder.HasAlternateKey(product => new { product.TenantId, product.Sku });
```

```csharp
builder.HasIndex(customer => new { customer.TenantId, customer.Email }).IsUnique();
builder.HasAlternateKey(customer => new { customer.TenantId, customer.Id });
```

```csharp
builder.HasIndex(order => new { order.TenantId, order.OrderNumber }).IsUnique();

builder.HasOne<Customer>()
    .WithMany()
    .HasForeignKey(order => new { order.TenantId, order.CustomerId })
    .HasPrincipalKey(customer => new { customer.TenantId, customer.Id })
    .OnDelete(DeleteBehavior.Restrict);
```

```csharp
builder.HasOne<Product>()
    .WithMany()
    .HasForeignKey(orderLine => new { orderLine.TenantId, orderLine.Sku })
    .HasPrincipalKey(product => new { product.TenantId, product.Sku })
    .OnDelete(DeleteBehavior.Restrict);
```

A foreign key from a tenant-owned type to a global one (`Product.CategoryId`) stays a single column.

## The hosts and the consumers

- Api: `ContextCommandFilter` sets `TenantId` and enters the scope (`authentication.md`), for reads and writes alike, since queries carry no context.
- Console: `Program.cs` enters the scope with the token's tenant before running the command (`authentication.md`).
- RabbitMQ consumers: `ReceiveEndpointService` enters the event's tenant first thing in the consumer's scope:

@@member variant:advanced/src/Boilerplate.Infrastructure/Messaging/RabbitMq/ReceiveEndpointService.cs::ReceiveEndpointService::ConsumeAsync@@

- With `events.transport` = `in-process` or `database`, `EventPublisher` and `OutboxWorker` add the same line after creating each consumer's scope.
- The outbox row has `TenantId` (`OutboxSettings.TenantIdMaxLength` = 64), copied from the event in `OutboxMessage.From`; the outbox table itself is not tenant-owned, so the worker sees every tenant's rows.

## Tests

`test/{Name}.Tests/Infrastructure/Tenancy/TenantScopeTests.cs`, with `TenantScopeBuilder` in `TestSupport/Infrastructure/Tenancy/`:

@@members test/Boilerplate.Tests/Infrastructure/Tenancy/TenantScopeTests.cs::TenantScopeTests@@

@@members test/Boilerplate.Tests/TestSupport/Infrastructure/Tenancy/TenantScopeBuilder.cs::TenantScopeBuilder@@

`test/{Name}.Tests/Infrastructure/Tenancy/TenantCommandHandlerMiddlewareTests.cs`:

@@members test/Boilerplate.Tests/Infrastructure/Tenancy/TenantCommandHandlerMiddlewareTests.cs::TenantCommandHandlerMiddlewareTests@@

`test/{Name}.Tests/Infrastructure/Persistence/AppDbContextTests.cs`:

@@members test/Boilerplate.Tests/Infrastructure/Persistence/AppDbContextTests.cs::AppDbContextTests@@

- `AppDbContextBuilder` takes a `TenantScope` (`WithTenantScope`); the configuration tests expect the tenant-first keys.
- `scripts/conformance/tenancy.sh` proves the rest against PostgreSQL: two tenants, the same sku in each, 404 across tenants, 403 without a tenant, a global category for everyone, the cross-tenant foreign key, consumers with interleaved tenants, and the tenant-first unique indexes.

## tenancy.key = text

The same code with `string` in place of `Guid`: `IMultiTenant.TenantId` is a `string` with a maximum length, `TenantScope` keeps the claim as it is (no parsing), and `Required` returns `string`. The tenant id is whatever the identity provider puts in the claim, so validate its format there.

## Decision record

`docs/adr/0015-multi-tenancy-by-interface.md`, in `project-docs-adr.md`.

## Namespaces and usings

Every file of this page, with its namespace and the `using` lines it needs (`ImplicitUsings` covers `System`, `System.Linq`, `System.Threading.Tasks` and the like). A file the table does not list follows the namespace rule in `naming.md`.

@@usings tenancy.md@@
