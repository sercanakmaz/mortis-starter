# 0006 — Command handlers are the use cases; there is no service per aggregate

- Status: accepted
- Date: 2026-11-17
- Context: A service per aggregate (`OrderService`) collected every operation of the aggregate, so each command handler only forwarded one call. The use case was split across two classes, and a change that touched two aggregates had nowhere to live except a service that reached into another aggregate.

## Decision

A command handler is the use case. It loads the aggregates the action needs through their repositories, calls their operations, stamps the user, saves, and maps the result. It may change several aggregates when the action is about all of them; a change that is a consequence of the action is an event handler in the reacting aggregate's folder. A domain service exists only for logic that fits no single aggregate: it needs several entities, or another system behind an anti-corruption port. It reads and decides; the handler saves.

## What varies

Whether a second change is the intent of the action or a consequence of it. The test: without the second change, does the command still do what its name says?

## Options

| Option | Cost | Why it was not chosen |
|---|---|---|
| A service per aggregate, handlers call one method | One more class per aggregate and a forwarding handler | The use case is in two places; cross-aggregate actions have no home |
| **Handlers are use cases, domain services only when needed** | Handlers hold the load-call-save steps | n/a |

## Consequences

- Gained: one place per use case; cross-aggregate actions that must be atomic have a home; domain services are small and named for their concept.
- Lost or owed: reviewers must apply the intent-or-consequence test; a handler that grows a line per new side effect is the smell to watch.
- Cost of going back: moderate.

## How it is enforced

`AGENTS.md` rules 8 to 10, and the `mortis-review` skill.
