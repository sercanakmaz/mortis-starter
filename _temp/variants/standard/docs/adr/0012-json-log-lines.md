# 0012 — Every log line is one flat JSON object, CorrelationId first

- Status: accepted
- Date: 2026-11-20
- Context: Log lines are read by machines first (search, alerts, the conformance suite) and must tie every line of one request, command or event together. Text lines need a parser per format; nested JSON needs a query per level.

## Decision

Every host logs through `JsonLineFormatter`: one JSON object per line, no nesting. `CorrelationId` is the first field, then `Timestamp`, `Level`, `Service`, `Version`, `Category`, `Message`, then every field of the open logging scopes and of the message template, then `Exception`. The command pipeline opens a scope with `CorrelationId`, `CausationId`, `TenantId`, `UserId` and `Command`; publishing or handling an event opens one with the event's fields and `Event`, `EventId`. Messages are templates, never built strings. The Console writes its log lines to stderr, so stdout holds only the result.

## What varies

Nothing per project: every environment logs the same way.

## Options

| Option | Cost | Why it was not chosen |
|---|---|---|
| Text lines | Nothing | Every reader needs its own parser |
| The built-in JSON console formatter | Nothing | Scope fields are nested in a `Scopes` list, not at the top level |
| A logging library with enrichers and sinks | A dependency to pin and teach | More than one flat line needs |
| **One small formatter class** | `JsonLineFormatter`, `LogFields` | n/a |

## Consequences

- Gained: any line can be found by `CorrelationId` with one field query; the conformance suite reads fields, not text.
- Lost or owed: lines are harder to read by eye in a terminal.
- Cost of going back: low.

## How it is enforced

`JsonLineFormatterTests`, and the log checks of `example-console.sh`.
