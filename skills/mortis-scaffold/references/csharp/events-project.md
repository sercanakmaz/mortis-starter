# Events project, C#

Applies when `addons` contains `events-project`. A new project `src/{Name}.Contracts`; `{Name}.Domain` references it. Layer 1: `options/addons.md`, `architecture/advanced-addons.md`.

**Not built or run yet** (decision 50): hand-written snippets; no variant builds this add-on. Phase 2 compiles and proves this page. `docs/unverified.md` lists it.

## How it fits together

```
src/{Name}.Contracts                the queue events (IQueueEvent) and nothing else; references {Name}.Shared only
  {Plural}/{Event}.cs               public record LineAdded(string Id, string LineId, string Sku, int Quantity) : EventBase, IQueueEvent;
src/{Name}.Domain                   references Contracts; aggregates raise contract events like their own
in-process events                   (not IQueueEvent) stay in Domain/Aggregates/{Plural}/Events
another service                     references the Contracts project or its NuGet package, never Domain
wire names                          from the namespace, or pinned with [MessageName("...")]
```

- Only events that leave the process move: an event no other process reads stays in Domain, so the contracts package does not grow with internals.
- A contract event's members are the published contract. Renaming or removing one is a breaking change for every consumer; with `testing` = `architecture` the golden file of `EventContractTests` (`testing-levels.md`) makes it a reviewed change.
- `{Name}.Shared` comes with the contracts because `EventBase` is there; it references only `FluentValidation` and the dependency-injection abstractions, so a consumer pulls little. A consumer in another language reads the JSON envelope (`docs/specs/messaging-wire-format.md`) and needs neither.
- The project is packable (`IsPackable` true, `PackageId` `{Name}.Contracts`); publishing the package is the project's own pipeline.

## MessageName

File `src/{Name}.Shared/Domain/MessageNameAttribute.cs`:

```csharp
[AttributeUsage(AttributeTargets.Class)]
public sealed class MessageNameAttribute(string name) : Attribute
{
    public string Name { get; } = name;
}
```

`MessageNames.Exchange` (`rabbitmq.md`) reads it first:

```csharp
    public static string Exchange(Type messageType)
    {
        if (messageType.GetCustomAttribute<MessageNameAttribute>() is { } messageName)
        {
            return messageName.Name;
        }

        var name = messageType.IsNested ? $"{messageType.DeclaringType!.Name}+{messageType.Name}" : messageType.Name;

        return $"{messageType.Namespace}:{name}";
    }
```

A project that turns this add-on on after its first release pins each moved event to its old name (`[MessageName("{Name}.Domain.Aggregates.Orders.Events:LineAdded")]`), so running consumers keep receiving it.

## The project

`src/{Name}.Contracts/{Name}.Contracts.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <IsPackable>true</IsPackable>
    <PackageId>{Name}.Contracts</PackageId>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="../{Name}.Shared/{Name}.Shared.csproj" />
  </ItemGroup>
</Project>
```

`{Name}.Domain.csproj` gains `<ProjectReference Include="../{Name}.Contracts/{Name}.Contracts.csproj" />`; `{Name}.slnx` lists the project. In the example the queue events `LineAdded` and `LineCancelled` move to `src/{Name}.Contracts/Orders/`, namespace `{Name}.Contracts.Orders`; `Order` raises them with a `using {Name}.Contracts.Orders;`. The consumers in Application already reference Domain, which brings Contracts.

## Tests

With `testing` = `architecture`, `LayerRulesTests` (`testing-levels.md`) gains the row `[InlineData(typeof(ContractsMarker), new[] { nameof(Shared) })]`, where `ContractsMarker` is an empty `public static class` in the Contracts project's root namespace, and Domain's row allows `Contracts`. At every level, `ContractsRulesTests`:

```csharp
public class ContractsRulesTests
{
    [Fact]
    public void ContractsProject_Should_HoldOnlyQueueEvents()
    {
        var offenders = typeof(ContractsMarker).Assembly.GetTypes()
            .Where(type => type != typeof(ContractsMarker) && !typeof(IQueueEvent).IsAssignableFrom(type) && !type.IsDefined(typeof(CompilerGeneratedAttribute)))
            .Select(type => type.Name);

        Assert.Empty(offenders);
    }

    [Fact]
    public void EveryQueueEvent_Should_LiveInTheContractsProject()
    {
        var offenders = typeof(DomainModule).Assembly.GetTypes().Where(type => typeof(IQueueEvent).IsAssignableFrom(type) && !type.IsInterface).Select(type => type.Name);

        Assert.Empty(offenders);
    }
}
```

`scripts/conformance/events-project.sh` builds a throwaway consumer against the contracts alone.

## Files this add-on changes

`{Name}.slnx`, `{Name}.Domain.csproj`, `MessageNames.cs` (with RabbitMQ), the moved events and the files that `using` their old namespace (the aggregate, the consumers, their tests), `docs/specs/messaging-wire-format.md` of the project (the wire names).

## Namespaces and usings

| File | Namespace | Usings |
|---|---|---|
| `MessageNameAttribute.cs` | `{Name}.Shared.Domain` | none |
| `ContractsMarker.cs`, the moved events | `{Name}.Contracts`, `{Name}.Contracts.{Plural}` | `{Name}.Shared.Domain` |
| `ContractsRulesTests.cs` | `{Name}.Tests.Architecture` | `{Name}.Contracts`, `{Name}.Domain`, `{Name}.Shared.Domain`, `System.Runtime.CompilerServices` |

## Decision record

`docs/adr/0031-event-contracts-project.md`, in `project-docs-adr.md`.
