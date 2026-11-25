# Events project, C#

Applies when `addons` contains `events-project`. A new project `src/{Name}.Contracts`; `{Name}.Domain` references it. Layer 1: `options/addons.md`, `architecture/advanced-addons.md`.

**Built** in the advanced reference solution (decision 52): compiled, tested and verified there.

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

`MessageNames.Exchange` (`rabbitmq.md`) reads it first (usings `System.Reflection`, `{Name}.Shared.Domain`):

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

`{Name}.Domain.csproj` gains `<ProjectReference Include="../{Name}.Contracts/{Name}.Contracts.csproj" />`; `{Name}.slnx` lists the project. `src/{Name}.Contracts/ContractsMarker.cs` is `namespace {Name}.Contracts;` and `public static class ContractsMarker;`. In the example every queue event moves, unchanged but for its namespace: `LineAdded`, `LineCancelled`, `Completed` and `Cancelled` to `src/{Name}.Contracts/Orders/` (namespace `{Name}.Contracts.Orders`), `StockIncreased` and `StockDecreased` to `src/{Name}.Contracts/Products/` (namespace `{Name}.Contracts.Products`). Every file that names one of them gains the matching `using` (the aggregates, the handlers and consumers, `ApplicationModule`, the read-model module, their tests); the in-process events stay where they are. Build after the move and add the `using` each `CS0246` names. The consumers in Application already reference Domain, which brings Contracts.

With an outbox and RabbitMQ, `OutboxWorker` reads a row's event type from the contracts assembly: `message.ToEvent(typeof(ContractsMarker).Assembly)` (using `{Name}.Contracts` in place of `{Name}.Domain`), because only queue events reach the outbox; left on the Domain assembly, every row fails to load and no consumer runs.

The wire names follow the namespace, so they change: in a project generated with this add-on that is its first release, and nothing is pinned. Tests that spell a wire name (`MessageNamesTests`, `MessageEnvelopeTests`) use the new one, for example `{Name}.Contracts.Orders:LineAdded`.

## Tests

With `testing` = `architecture`, `LayerRulesTests` (`testing-levels.md`, using `{Name}.Contracts`) becomes

```csharp
    [InlineData(typeof(SharedModule), new string[0])]
    [InlineData(typeof(ContractsMarker), new[] { nameof(Shared) })]
    [InlineData(typeof(DomainModule), new[] { nameof(Shared), nameof(Contracts) })]
    [InlineData(typeof(ApplicationModule), new[] { nameof(Shared), nameof(Contracts), nameof(Domain) })]
```

and `EventContracts.Describe` scans `typeof(ContractsMarker).Assembly` in place of the Domain assembly (using `{Name}.Contracts` in place of `{Name}.Domain`); regenerate the golden file with `MORTIS_UPDATE_CONTRACTS=1`. With RabbitMQ, `MessageNamesTests` gains a pinned-name test over `test/{Name}.Tests/TestSupport/Infrastructure/Messaging/PinnedProbe.cs`:

```csharp
[MessageName("Old.Namespace:Pinned")]
public record PinnedProbe : EventBase, IQueueEvent;
```

```csharp
    [Fact]
    public void Exchange_WithAMessageName_Should_UseThePinnedName()
    {
        Assert.Equal("Old.Namespace:Pinned", MessageNames.Exchange(typeof(PinnedProbe)));
    }
```

At every level, `test/{Name}.Tests/Architecture/ContractsRulesTests.cs`:

```csharp
public class ContractsRulesTests
{
    [Fact]
    public void ContractsProject_Should_HoldOnlyQueueEvents()
    {
        var offenders = typeof(ContractsMarker).Assembly.GetTypes()
            .Where(type => type != typeof(ContractsMarker) && !typeof(IQueueEvent).IsAssignableFrom(type) && !type.IsDefined(typeof(CompilerGeneratedAttribute), false))
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

`{Name}.slnx`, `{Name}.Domain.csproj`, `MessageNames.cs` and `OutboxWorker.cs` (with RabbitMQ), the moved events and the files that `using` their old namespace (the aggregate, the consumers, their tests), `docs/specs/messaging-wire-format.md` of the project (the wire names).

## Namespaces and usings

| File | Namespace | Usings |
|---|---|---|
| `MessageNameAttribute.cs` | `{Name}.Shared.Domain` | none |
| `ContractsMarker.cs`, the moved events | `{Name}.Contracts`, `{Name}.Contracts.{Plural}` | `{Name}.Shared.Domain` |
| `ContractsRulesTests.cs` | `{Name}.Tests.Architecture` | `{Name}.Contracts`, `{Name}.Domain`, `{Name}.Shared.Domain`, `System.Runtime.CompilerServices` |

## Decision record

`docs/adr/0031-event-contracts-project.md`, in `project-docs-adr.md`.
