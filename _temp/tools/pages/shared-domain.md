# Shared: domain building blocks, C#

Project `{Name}.Shared`, folder `Domain/`, namespace `{Name}.Shared.Domain`. These types know nothing about storage. Layer 1: `building-blocks.md` sections Entity, AggregateRoot, Domain event, Sync and Event consumer, Event dispatcher.

Each section gives the file, the contract, the rules, and a snippet for every member with a real body. Write each file as: `using` lines the members need, the file-scoped namespace, then the type with the members in the order shown.

## Entity

File `Domain/Entity.cs`.

@@decl src/Boilerplate.Shared/Domain/Entity.cs::Entity@@

@@member src/Boilerplate.Shared/Domain/Entity.cs::Entity::Id@@

- The id is set by the subclass's constructor, never by the database.

## AggregateRoot

File `Domain/AggregateRoot.cs`. Needs `using System.ComponentModel.DataAnnotations.Schema;`.

@@decl src/Boilerplate.Shared/Domain/AggregateRoot.cs::AggregateRoot@@

Members, in this order: a private `List<EventBase> _domainEvents`; the six audit properties; `Version`; `DomainEvents`; the two constructors; `SetUpdatedAudit`; `ClearEvents`; `RaiseEvent`.

@@member src/Boilerplate.Shared/Domain/AggregateRoot.cs::AggregateRoot::CreatedAt@@

The other five audit properties have the same shape: `CreatedUserId` and `CreatedUserName` (`string`), `UpdatedAt` (`DateTime`), `UpdatedUserId` and `UpdatedUserName` (`string`).

@@member src/Boilerplate.Shared/Domain/AggregateRoot.cs::AggregateRoot::Version@@

@@member src/Boilerplate.Shared/Domain/AggregateRoot.cs::AggregateRoot::DomainEvents@@

@@member src/Boilerplate.Shared/Domain/AggregateRoot.cs::AggregateRoot::AggregateRoot#1@@

@@member src/Boilerplate.Shared/Domain/AggregateRoot.cs::AggregateRoot::AggregateRoot#2@@

@@member src/Boilerplate.Shared/Domain/AggregateRoot.cs::AggregateRoot::SetUpdatedAudit@@

@@member src/Boilerplate.Shared/Domain/AggregateRoot.cs::AggregateRoot::ClearEvents@@

@@member src/Boilerplate.Shared/Domain/AggregateRoot.cs::AggregateRoot::RaiseEvent@@

- `RaiseEvent` is protected: only the aggregate raises its events.
- Every timestamp is UTC (`DateTime.Now` is a build error).
- `Version` truncates to microseconds because PostgreSQL stores microseconds; an in-memory value and a stored one must compare equal.
- `SetUpdatedAudit` is called after the operation, so the events the operation raised carry the user.

## AuditUser and SystemUser

File `Domain/AuditUser.cs`:

@@decl src/Boilerplate.Shared/Domain/AuditUser.cs::AuditUser@@

File `Domain/SystemUser.cs`, the actor of event consumers:

@@decl src/Boilerplate.Shared/Domain/SystemUser.cs::SystemUser@@

@@member src/Boilerplate.Shared/Domain/SystemUser.cs::SystemUser::Id@@

@@member src/Boilerplate.Shared/Domain/SystemUser.cs::SystemUser::Email@@

## EventBase and IQueueEvent

File `Domain/EventBase.cs`, both types. The user is stamped by the aggregate; correlation, causation, tenant and language by `EventContext` while a command runs.

@@decl src/Boilerplate.Shared/Domain/EventBase.cs::EventBase@@

@@member src/Boilerplate.Shared/Domain/EventBase.cs::EventBase::EventId@@

@@member src/Boilerplate.Shared/Domain/EventBase.cs::EventBase::OccurredAt@@

@@member src/Boilerplate.Shared/Domain/EventBase.cs::EventBase::CorrelationId@@

@@member src/Boilerplate.Shared/Domain/EventBase.cs::EventBase::CausationId@@

@@member src/Boilerplate.Shared/Domain/EventBase.cs::EventBase::TenantId@@

@@member src/Boilerplate.Shared/Domain/EventBase.cs::EventBase::Language@@

@@member src/Boilerplate.Shared/Domain/EventBase.cs::EventBase::UserId@@

@@member src/Boilerplate.Shared/Domain/EventBase.cs::EventBase::UserEmail@@

@@decl src/Boilerplate.Shared/Domain/EventBase.cs::IQueueEvent@@

- An event that must leave the transaction (event consumers, publishing) also implements `IQueueEvent`. An event without it is handled only by sync handlers.

## EventContext

File `Domain/EventContext.cs`. One per scope. Holds events taken off aggregates (raised), events whose sync handlers have run (dispatched), and the stack of contexts of the commands running in the scope. Every member locks.

@@decl src/Boilerplate.Shared/Domain/EventContext.cs::EventContext@@

Fields: `private readonly Lock _lock = new();`, `private readonly Queue<EventBase> _raisedEvents = new();`, `private readonly Queue<EventBase> _dispatchedEvents = new();`, `private readonly Stack<CommandContext> _commandContexts = new();`. Needs `using {Name}.Shared.Application;`.

@@member src/Boilerplate.Shared/Domain/EventContext.cs::EventContext::RaisedCount@@

@@member src/Boilerplate.Shared/Domain/EventContext.cs::EventContext::EnterCommand@@

@@member src/Boilerplate.Shared/Domain/EventContext.cs::EventContext::LeaveCommand@@

@@member src/Boilerplate.Shared/Domain/EventContext.cs::EventContext::AddRaised@@

- The context is a stack because a sync handler sends a command inside the running one: its events carry its own context, with the triggering event as their cause.

@@member src/Boilerplate.Shared/Domain/EventContext.cs::EventContext::TakeRaised@@

`AddDispatched` and `TakeDispatched` are the same two members on `_dispatchedEvents`.

@@member src/Boilerplate.Shared/Domain/EventContext.cs::EventContext::Clear@@

## Sync and event consumer bases

File `Domain/BaseSyncEventHandler.cs` holds `ISyncEventHandler` and `BaseSyncEventHandler<TEvent>`:

@@decl src/Boilerplate.Shared/Domain/BaseSyncEventHandler.cs::ISyncEventHandler@@

@@member src/Boilerplate.Shared/Domain/BaseSyncEventHandler.cs::ISyncEventHandler::Sequence@@

@@member src/Boilerplate.Shared/Domain/BaseSyncEventHandler.cs::ISyncEventHandler::Handle@@

@@decl src/Boilerplate.Shared/Domain/BaseSyncEventHandler.cs::BaseSyncEventHandler@@

@@member src/Boilerplate.Shared/Domain/BaseSyncEventHandler.cs::BaseSyncEventHandler::Sequence@@

@@member src/Boilerplate.Shared/Domain/BaseSyncEventHandler.cs::BaseSyncEventHandler::OnHandle@@

@@member src/Boilerplate.Shared/Domain/BaseSyncEventHandler.cs::BaseSyncEventHandler::Handle@@

@@member src/Boilerplate.Shared/Domain/BaseSyncEventHandler.cs::BaseSyncEventHandler::ContextOf@@

File `Domain/BaseEventConsumer.cs` is the same with `IEventConsumer` and `BaseEventConsumer<TEvent>`, one more constraint, no `Sequence` (consumers run in the order of their `AddEventConsumer` lines, `event-consumers.md`), and the system user in `ContextOf`:

@@decl src/Boilerplate.Shared/Domain/BaseEventConsumer.cs::BaseEventConsumer@@

@@member src/Boilerplate.Shared/Domain/BaseEventConsumer.cs::BaseEventConsumer::ContextOf@@

Both files need `using {Name}.Shared.Application;`.

- Sync handlers run in ascending `Sequence`; equal values keep registration order.
- A handler's body is one awaited `commandDispatcher.Send(...)` (see `application.md`).

## EventHandlerExecutor

File `Domain/EventHandlerExecutor.cs`. Runs the sync handlers of every raised event, including events raised by those handlers, then moves each event to dispatched. Called by the unit of work before `COMMIT`.

@@decl src/Boilerplate.Shared/Domain/EventHandlerExecutor.cs::EventHandlerExecutor@@

@@member src/Boilerplate.Shared/Domain/EventHandlerExecutor.cs::EventHandlerExecutor::ExecuteRaisedAsync@@

- A handler that throws stops the loop; the exception reaches the pipeline, which rolls back.

## Tests

The kernel tests for these types are `AggregateRootTests`, `EventContextTests` and `EventHandlerExecutorTests`, in `testing-kernel.md`.

## Namespaces and usings

Every file of this page, with its namespace and the `using` lines it needs (`ImplicitUsings` covers `System`, `System.Linq`, `System.Threading.Tasks` and the like). A file the table does not list follows the namespace rule in `naming.md`.

@@usings shared-domain.md@@
