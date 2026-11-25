# 0008 — Each aggregate chooses its key; an integer key is assigned by the database

- Status: accepted
- Date: 2026-11-22
- Context: Most aggregates get a time-ordered `Guid` made in the program, so the id is known before the insert and can be sent by the client. Some need a small integer the database assigns (a person-readable number, an existing system). Such an id does not exist until the insert, which changes how the aggregate raises its first event.

## Decision

`MORTIS.md` `keys.default` gives new aggregates their key type; an aggregate may differ. A `Guid` key is assigned in the constructor and mapped with `ValueGeneratedNever()`. An `int` or `long` key is mapped with `IdentityAggregateConfiguration` (`ValueGeneratedOnAdd()`); the aggregate's constructor raises nothing; the command handler calls `CreateAsync` (which flushes, so the id exists), then the aggregate's `MarkCreated()` raises `Created` with the real id, then `UpdateAsync` hands the event over, all in one transaction. Its create command has no `Id`.

## What varies

Who assigns the id, and so when it is known.

## Options

| Option | Cost | Why it was not chosen |
|---|---|---|
| `Guid` everywhere | None; larger ids | Some aggregates need an integer |
| Integer key with `Created` raised in the constructor | None in code | The event carries `0` |
| **Integer key, `Created` raised by `MarkCreated` after the insert** | One more operation and one more call in the handler | n/a |

## Consequences

- Gained: integer keys where they are needed, with correct events.
- Lost or owed: statement order in the handler is behaviour; the client cannot choose such an id.
- Cost of going back: moderate (the column type and every reference change).

## How it is enforced

`CategoryTests`, `CreateCategoryCommandHandlerTests`, `CategoryConfigurationTests`, and the conformance checks on `POST /categories`.
