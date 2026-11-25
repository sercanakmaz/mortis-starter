# 0007 — One context on the command

- Status: accepted
- Date: 2026-11-20
- Context: The user was two `[JsonIgnore]` properties on every writing command (0002, 0003). Correlation, causation, tenant and language follow the same path from the host to the events; two more properties per concern on every command does not scale, and an ambient correlation id hid where it came from.

## Decision

A writing command implements `IContextCommand` and declares one property, `Context`, of type `CommandContext` (user, correlation id, causation id, tenant, language). The host fills it; the pipeline completes it (a correlation id when there was none) and tells `EventContext` which command is running, so every event raised until it ends carries its context; a nested command gets its own and the outer one is restored after. An event handler builds its command's context with `ContextOf(@event)`.

## What varies

Which facts about a call travel with it. A new one is a property of `CommandContext`, not of every command.

## Options

| Option | Cost | Why it was not chosen |
|---|---|---|
| Two properties per concern on every command | A line per concern per command | Grows with every option |
| An ambient context service | Nothing per command | A hidden dependency, wrong inside event consumers, the problem 0003 removed |
| **One `Context` property, stamped on events by `EventContext`** | `CommandContext`, a stack in `EventContext` | n/a |

## Consequences

- Gained: one line per command; correlation and causation on every event without the handlers doing anything.
- Lost or owed: `EventContext` knows the running command; a command sent outside the pipeline is not stamped.
- Cost of going back: moderate.

## How it is enforced

`ContextCommandTests`, `CommandContextTests`, `EventContextTests`, `CommandPipelineTests`, `verify.sh` (one context line per command), and the conformance check that reads correlation and causation from the `stock-flow` log.
