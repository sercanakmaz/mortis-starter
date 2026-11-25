# Bounded contexts, C#

Applies when `addons` contains `contexts`. Every project's folders change; no new runtime type is added. Layer 1: `options/addons.md`, `architecture/advanced-addons.md`.

**Not built or run yet** (decision 50): hand-written snippets; no variant builds this add-on. It is listed in the repository's `TODO.md` until a build compiles and proves it.

## How it fits together

```
src/{Name}.Domain/Contexts/{Context}/Aggregates/{Plural}/...        instead of Domain/Aggregates/{Plural}
src/{Name}.Application/Contexts/{Context}/{Plural}/...
src/{Name}.Infrastructure/Persistence/Contexts/{Context}/{Plural}/...
src/{Name}.Api/Controllers/{Context}/{Plural}Controller.cs
test/{Name}.Tests/... mirrors the same paths
a context talks to another only through events and ids:
  no type of context A's domain or application refers to a type of context B's domain or application
  a reaction in B to an event of A is a handler or consumer in B, typed on A's event (events are the shared language)
```

- One process, one database, one `AppDbContext`: a context is a boundary in the code, not a deployment. Splitting a context into its own service later means moving its folders and turning its in-process reactions into queue events.
- Events are the only types two contexts share. A context that reacts to another's event references that event's namespace and nothing else of it; with `events-project`, it references the contracts project instead.
- Tables keep their names; a context's tables may get a prefix (`sales_orders`) when two contexts would otherwise want the same name.
- The example splits into `Catalog` (`Products`, `Categories`) and `Sales` (`Customers`, `Orders`). `Sales` refers to products only by SKU and reacts with its own commands; `Catalog` decreases stock when it sees `Sales`' `LineAdded`. In code, `DecreaseStockWhenOrderLineAddedEventConsumer` moves to `Application/Contexts/Catalog/Products/` and stays typed on `{Name}.Domain.Contexts.Sales.Aggregates.Orders.Events.LineAdded`.

## Namespaces

Namespaces follow the folders: `{Name}.Domain.Contexts.Sales.Aggregates.Orders`, `{Name}.Application.Contexts.Catalog.Products`, `{Name}.Infrastructure.Persistence.Contexts.Sales.Orders`. With RabbitMQ the wire name of an event comes from its namespace (`MessageNames`), so turning contexts on in a running system changes wire names: do it before the first release, or keep the old names with `events-project`'s explicit names.

## The rule test

File `test/{Name}.Tests/Architecture/ContextRulesTests.cs`, shipped at every testing level when this add-on is on:

```csharp
public class ContextRulesTests
{
    [Fact]
    public void NoContext_Should_ReferAnotherContextsTypesExceptItsEvents()
    {
        var offenders = ContextReferences.Of(typeof(DomainModule).Assembly, typeof(ApplicationModule).Assembly)
            .Where(reference => reference.FromContext != reference.ToContext && !reference.ToNamespace.EndsWith(ContextReferences.EventsSuffix))
            .Select(reference => $"{reference.FromType} -> {reference.ToType}");

        Assert.Empty(offenders);
    }
}
```

File `test/{Name}.Tests/TestSupport/Architecture/ContextReferences.cs`: `Of(params Assembly[] assemblies)` walks every type in a `.Contexts.` namespace and collects the types it refers to through its base type, interfaces, fields, properties, constructor and method parameters, return types and generic arguments; `ContextOf(namespace)` is the segment after `Contexts.`; `EventsSuffix` is `".Events"`. Reflection sees signatures, not method bodies: a reference only inside a method body (`new OtherContextType()`) is caught by the review skill's check, not by this test.

## Files this add-on changes

Every file of the example and the manifest's per-aggregate folders gain a `Contexts/{Context}` segment; `AppDbContext` applies configurations from the assembly as before (`ApplyConfigurationsFromAssembly`), so it does not change. The per-aggregate folder patterns of the manifest are matched with `{Context}` as one more PascalCase segment.

## Namespaces and usings

| File | Namespace | Usings |
|---|---|---|
| `ContextRulesTests.cs` | `{Name}.Tests.Architecture` | `{Name}.Application`, `{Name}.Domain`, `{Name}.Tests.TestSupport.Architecture` |
| `ContextReferences.cs` | `{Name}.Tests.TestSupport.Architecture` | `System.Reflection` |

## Decision record

`docs/adr/0030-bounded-contexts.md`, in `project-docs-adr.md`.
