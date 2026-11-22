# 0005 — The Console runs one command by name and does not migrate

- Status: accepted
- Date: <the date the project was generated>
- Context: A developer needs to try any command by hand without writing code for it, and applying a migration is a deployment concern, not a testing one.

## Decision

`dotnet run --project src/Boilerplate.Console -- <Command> '<json>'` runs the command of that name (case-insensitive; a command's name is its type name without `Command`), found in the Application assembly. The JSON argument may be `@file`. The result is printed on stdout as JSON; the log lines go to stderr. The run names its user with `--user <id>` (required) and `--email`, and its tenant and language with `--tenant` and `--language` where those options are on; the Console takes no token. There are no scenarios: a flow is several runs. Migrations are applied with `dotnet ef database update`. Exit code 1 means no command name, 2 a failure printed as one line `{code, message, errors}` on stderr.

## What varies

Which commands a developer wants to try by hand.

## Options

| Option | Cost | Why it was not chosen |
|---|---|---|
| Fixed scenarios plus a migrate step | Code for every experiment | Every experiment needs code, and migration hides in a test host |
| A registration line per command, plus scenarios | A line per command, scenario classes to keep up | More to maintain than the problem needs |
| **One runner that finds the command by name** | `ConsoleCommandRunner`, a VS Code launch configuration | n/a |

## Consequences

- Gained: any command can be run from VS Code or a script with no Console change; stdout can be piped.
- Lost or owed: a new database needs `dotnet ef database update` first; a multi-step flow is a script of runs.
- Cost of going back: low.

## How it is enforced

`ConsoleCommandRunnerTests` and the Console checks of the conformance suite.
