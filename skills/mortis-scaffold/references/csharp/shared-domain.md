# Shared: domain building blocks, C#

Project `{Name}.Shared`, folder `Domain/`, namespace `{Name}.Shared.Domain`. These types know nothing about storage. Layer 1: `building-blocks.md` sections Entity, AggregateRoot, Domain event, Sync and Async event handler, Event dispatcher.

Each section gives the file, the contract, the rules, and a snippet for every member with a real body. Write each file as: `using` lines the members need, the file-scoped namespace, then the type with the members in the order shown.

## Entity

File `Domain/Entity.cs`.

```csharp
public abstract class Entity<TKey>
```

```csharp
public TKey Id { get; protected set; }
```

- The id is set by the subclass's constructor, never by the database.

## AggregateRoot

File `Domain/AggregateRoot.cs`. Needs `using System.ComponentModel.DataAnnotations.Schema;`.

```csharp
public abstract class AggregateRoot<TKey> : Entity<TKey>
```

Members, in this order: a private `List<EventBase> _domainEvents`; the six audit properties; `Version`; `DomainEvents`; the two constructors; `SetUpdatedAudit`; `ClearEvents`; `RaiseEvent`.

```csharp
public DateTime CreatedAt { get; protected set; }
```

The other five audit properties have the same shape: `CreatedUserId` and `CreatedUserName` (`string`), `UpdatedAt` (`DateTime`), `UpdatedUserId` and `UpdatedUserName` (`string`).

```csharp
[NotMapped] public long Version => UpdatedAt.Ticks / TimeSpan.TicksPerMicrosecond;
```

```csharp
[NotMapped] public IReadOnlyCollection<EventBase> DomainEvents => _domainEvents?.AsReadOnly();
```

```csharp
protected AggregateRoot()
{
    var currentDate = DateTime.UtcNow;
    CreatedAt = currentDate;
    UpdatedAt = currentDate;
}
```

```csharp
protected AggregateRoot(AuditUser user)
{
    var currentDate = DateTime.UtcNow;
    CreatedAt = currentDate;
    CreatedUserId = user?.Id;
    CreatedUserName = user?.Email;
    UpdatedAt = currentDate;
    UpdatedUserId = user?.Id;
    UpdatedUserName = user?.Email;
}
```

```csharp
public void SetUpdatedAudit(AuditUser user)
{
    UpdatedAt = DateTime.UtcNow;
    UpdatedUserId = user?.Id;
    UpdatedUserName = user?.Email;

    for (var index = 0; index < (_domainEvents?.Count ?? 0); index++)
    {
        _domainEvents[index] = _domainEvents[index] with { UserId = UpdatedUserId, UserEmail = UpdatedUserName };
    }
}
```

```csharp
public void ClearEvents() => _domainEvents?.Clear();
```

```csharp
protected void RaiseEvent(EventBase @event)
{
    _domainEvents ??= [];
    _domainEvents.Add(@event with { UserId = UpdatedUserId, UserEmail = UpdatedUserName });
}
```

- `RaiseEvent` is protected: only the aggregate raises its events.
- Every timestamp is UTC (`DateTime.Now` is a build error).
- `Version` truncates to microseconds because PostgreSQL stores microseconds; an in-memory value and a stored one must compare equal.
- `SetUpdatedAudit` is called after the operation, so the events the operation raised carry the user.

## AuditUser and SystemUser

File `Domain/AuditUser.cs`:

```csharp
public sealed record AuditUser(string Id, string Email);
```

File `Domain/SystemUser.cs`, the actor of async handlers:

```csharp
public static class SystemUser
```

```csharp
public const string Id = "system";
```

```csharp
public const string Email = "system@localhost";
```

## EventBase and IQueueEvent

File `Domain/EventBase.cs`, both types. The user is stamped by the aggregate; correlation, causation, tenant and language by `EventContext` while a command runs.

```csharp
public abstract record EventBase
```

```csharp
public Guid EventId { get; init; } = Guid.CreateVersion7();
```

```csharp
public DateTime OccurredAt { get; init; } = DateTime.UtcNow;
```

```csharp
public Guid CorrelationId { get; init; }
```

```csharp
public Guid? CausationId { get; init; }
```

```csharp
public string TenantId { get; init; }
```

```csharp
public string Language { get; init; }
```

```csharp
public string UserId { get; init; }
```

```csharp
public string UserEmail { get; init; }
```

```csharp
public interface IQueueEvent;
```

- An event that must leave the transaction (async handlers, publishing) also implements `IQueueEvent`. An event without it is handled only by sync handlers.

## EventContext

File `Domain/EventContext.cs`. One per scope. Holds events taken off aggregates (raised), events whose sync handlers have run (dispatched), and the stack of contexts of the commands running in the scope. Every member locks.

```csharp
public sealed class EventContext
```

Fields: `private readonly Lock _lock = new();`, `private readonly Queue<EventBase> _raisedEvents = new();`, `private readonly Queue<EventBase> _dispatchedEvents = new();`, `private readonly Stack<CommandContext> _commandContexts = new();`. Needs `using {Name}.Shared.Application;`.

```csharp
public int RaisedCount
{
    get
    {
        lock (_lock)
        {
            return _raisedEvents.Count;
        }
    }
}
```

```csharp
public void EnterCommand(CommandContext commandContext)
{
    lock (_lock)
    {
        _commandContexts.Push(commandContext);
    }
}
```

```csharp
public void LeaveCommand()
{
    lock (_lock)
    {
        _commandContexts.TryPop(out _);
    }
}
```

```csharp
public void AddRaised(EventBase @event)
{
    lock (_lock)
    {
        _raisedEvents.Enqueue(_commandContexts.TryPeek(out var commandContext)
            ? @event with
            {
                CorrelationId = commandContext.CorrelationId,
                CausationId = commandContext.CausationId,
                TenantId = commandContext.TenantId,
                Language = commandContext.Language
            }
            : @event);
    }
}
```

- The context is a stack because a sync handler sends a command inside the running one: its events carry its own context, with the triggering event as their cause.

```csharp
public EventBase TakeRaised()
{
    lock (_lock)
    {
        return _raisedEvents.TryDequeue(out var @event) ? @event : null;
    }
}
```

`AddDispatched` and `TakeDispatched` are the same two members on `_dispatchedEvents`.

```csharp
public void Clear()
{
    lock (_lock)
    {
        _raisedEvents.Clear();
        _dispatchedEvents.Clear();
    }
}
```

## Sync and async event handler bases

File `Domain/BaseSyncEventHandler.cs` holds `ISyncEventHandler` and `BaseSyncEventHandler<TEvent>`:

```csharp
public interface ISyncEventHandler
```

```csharp
int Sequence { get; }
```

```csharp
Task Handle(EventBase @event);
```

```csharp
public abstract class BaseSyncEventHandler<TEvent> : ISyncEventHandler where TEvent : EventBase
```

```csharp
public virtual int Sequence => 0;
```

```csharp
protected abstract Task OnHandle(TEvent @event);
```

```csharp
public Task Handle(EventBase @event)
{
    return OnHandle((TEvent)@event);
}
```

```csharp
protected static CommandContext ContextOf(EventBase @event)
{
    return CommandContext.FromEvent(@event, new AuditUser(@event.UserId, @event.UserEmail));
}
```

File `Domain/BaseAsyncEventHandler.cs` is the same with `IAsyncEventHandler` and `BaseAsyncEventHandler<TEvent>`, one more constraint, and the system user in `ContextOf`:

```csharp
public abstract class BaseAsyncEventHandler<TEvent> : IAsyncEventHandler where TEvent : EventBase, IQueueEvent
```

```csharp
protected static CommandContext ContextOf(EventBase @event)
{
    return CommandContext.FromEvent(@event, new AuditUser(SystemUser.Id, SystemUser.Email));
}
```

Both files need `using {Name}.Shared.Application;`.

- Handlers run in ascending `Sequence`; equal values keep registration order.
- A handler's body is one awaited `commandDispatcher.Send(...)` (see `application.md`).

## EventHandlerExecutor

File `Domain/EventHandlerExecutor.cs`. Runs the sync handlers of every raised event, including events raised by those handlers, then moves each event to dispatched. Called by the unit of work before `COMMIT`.

```csharp
public sealed class EventHandlerExecutor(IServiceProvider serviceProvider, EventContext eventContext)
```

```csharp
public async Task ExecuteRaisedAsync()
{
    while (eventContext.TakeRaised() is { } @event)
    {
        var eventHandlers = serviceProvider
            .GetServices(typeof(BaseSyncEventHandler<>).MakeGenericType(@event.GetType()))
            .Cast<ISyncEventHandler>()
            .OrderBy(eventHandler => eventHandler.Sequence)
            .ToList();

        foreach (var eventHandler in eventHandlers)
        {
            await eventHandler.Handle(@event);
        }

        eventContext.AddDispatched(@event);
    }
}
```

- A handler that throws stops the loop; the exception reaches the pipeline, which rolls back.

## Tests

The kernel tests for these types are `AggregateRootTests`, `EventContextTests` and `EventHandlerExecutorTests`, in `testing-kernel.md`.

## Namespaces and usings

Every file of this page, with its namespace and the `using` lines it needs (`ImplicitUsings` covers `System`, `System.Linq`, `System.Threading.Tasks` and the like). A file the table does not list follows the namespace rule in `naming.md`.

| File | Namespace | Usings |
|---|---|---|
| `src/{Name}.Shared/Domain/AggregateRoot.cs` | `{Name}.Shared.Domain` | System.ComponentModel.DataAnnotations.Schema |
| `src/{Name}.Shared/Domain/AuditUser.cs` | `{Name}.Shared.Domain` | none |
| `src/{Name}.Shared/Domain/BaseAsyncEventHandler.cs` | `{Name}.Shared.Domain` | {Name}.Shared.Application |
| `src/{Name}.Shared/Domain/BaseSyncEventHandler.cs` | `{Name}.Shared.Domain` | {Name}.Shared.Application |
| `src/{Name}.Shared/Domain/Entity.cs` | `{Name}.Shared.Domain` | none |
| `src/{Name}.Shared/Domain/EventBase.cs` | `{Name}.Shared.Domain` | none |
| `src/{Name}.Shared/Domain/EventContext.cs` | `{Name}.Shared.Domain` | {Name}.Shared.Application |
| `src/{Name}.Shared/Domain/EventHandlerExecutor.cs` | `{Name}.Shared.Domain` | Microsoft.Extensions.DependencyInjection |
| `src/{Name}.Shared/Domain/SystemUser.cs` | `{Name}.Shared.Domain` | none |
