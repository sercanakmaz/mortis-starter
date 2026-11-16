# 0002 — A command is a plain record that implements interfaces

- Status: accepted
- Date: 2026-11-16
- Context: `BaseCommand<TContent>` and `UserCommand<TContent>` were abstract records. They carried no behaviour a marker could not carry, except two `[JsonIgnore]` properties and `ToAuditUser()`, and they used up the command's one base type.

## Decision

`ICommand<TContent>` says "I am a command and my result is `TContent`". `IUserCommand` (`UserId`, `UserEmail`) says "the host fills the user in". `ITransactionalCommand` says "run me in a transaction". A command implements the ones it needs: `ICommand<ProductDto>, IUserCommand, ITransactionalCommand` when it writes, `ICommand<ProductDto>` when it only reads. `ToAuditUser()` is an extension method on `IUserCommand`.

## What varies

Nothing: this is a placement decision.

## Options

| Option | Cost | Why it was not chosen |
|---|---|---|
| Abstract base records | The two `[JsonIgnore]` properties are written once | A command could not derive from anything else, and reads and writes sat in one hierarchy for no behavioural reason |
| **Interfaces only** | Each writing command declares the two `[JsonIgnore]` properties itself (an attribute on an interface member is not inherited by the implementation) | n/a |
| Interfaces plus one `AuditUser User` property | One line per command instead of two | Not done yet: it also changes the filter, the Console, event stamping and tests. It is the next step if the repetition hurts |

## Consequences

- Gained: no inheritance in the command model; queries and writes differ only in the interfaces they list.
- Lost or owed: two repeated lines in every writing command.
- Cost of going back: moderate; every command declaration changes.

## How it is enforced

`UserCommandTests` (a `userId` in the JSON body is ignored), the compiler, and `ICommandDispatcher.Send` accepting only `ICommand<TContent>`.
