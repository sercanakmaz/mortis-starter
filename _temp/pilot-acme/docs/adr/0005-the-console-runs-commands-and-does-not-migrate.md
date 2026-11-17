# 0005 — The Console runs commands by name and does not migrate

- Status: accepted
- Date: 2026-11-16
- Context: The Console migrated the database and ran a fixed set of scenarios. Trying a new command meant writing a scenario first, and applying a migration is a deployment concern, not a testing one.

## Decision

`dotnet run --project src/Acme.Console -- <Name> '<json>'` runs the scenario or the command of that name (case-insensitive; a command's name is its type name without `Command`). Which commands the Console can run is decided by one line each in `ScenarioModule`: `AddCommandScenario<TCommand, TContent>()`. The JSON argument may be `@file`. The Console user is `console`, and async handlers are awaited before the process exits. Migrations are applied with `dotnet ef database update`. Exit code 1 means an unknown name, 2 a domain error printed as `{code, message, errors}` on stderr.

## What varies

Which commands a developer wants to try by hand.

## Options

| Option | Cost | Why it was not chosen |
|---|---|---|
| Fixed scenarios plus a migrate step | Nothing new | Every experiment needs code, and migration hides in a test host |
| **A generic command runner plus scenarios** | `CommandScenario`, one registration line per command, a VS Code launch configuration | n/a |

## Consequences

- Gained: any command can be run from VS Code with arguments; a flow that spans several commands stays a scenario.
- Lost or owed: a new database needs `dotnet ef database update` first.
- Cost of going back: low.

## How it is enforced

`CommandScenarioTests` and the Console checks of the conformance suite.
