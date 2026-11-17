# 0003 — The user is data on the command, never an ambient service

- Status: accepted; the user properties replaced by one context in 0007
- Date: <the date the project was generated>
- Context: An `IUserContext` service was read deep inside services to stamp audit columns. It was an invisible dependency, it had no value inside an async event handler or the Console, and a test had to fake it.

## Decision

A command that writes implements `IUserCommand`. The presentation layer fills it: `UserCommandFilter` in the Api (claims `NameIdentifier` and `Email`, overwriting anything the client sent) and `CommandScenario` / `.AsConsoleUser()` in the Console (`console`). The command handler passes `command.ToAuditUser()` as the first argument of the service's write methods, and the service passes it to the aggregate. An event carries the user of the change that raised it: `AggregateRoot` stamps `EventBase.UserId` and `UserEmail` when it raises the event. A sync handler passes `@event.UserId` on to the next command; an async handler sends it as `SystemUser`.

## What varies

Who the actor is (a person, the console, the system). The domain must not look it up.

## Options

| Option | Cost | Why it was not chosen |
|---|---|---|
| An ambient `IUserContext` | One service, no signature changes | Hidden dependency; wrong or empty in async handlers and the Console |
| Read the claims in each command handler | Nothing in the kernel | Every handler repeats it and can forget it |
| **The user as data on the command, stamped on events** | `AuditUser` first argument of write methods, two properties per writing command | n/a |

## Consequences

- Gained: the actor is visible in every signature and every test; the same command works from the Api, the Console and an event handler.
- Lost or owed: authentication is not configured, so an Api write has an empty audit user until claims exist.
- Cost of going back: high; every write signature carries the user.

## How it is enforced

`UserCommandFilterTests`, `CommandScenarioTests`, the aggregate tests for stamping, and rule 20 in `AGENTS.md`.
