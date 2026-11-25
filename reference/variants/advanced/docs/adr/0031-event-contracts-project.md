# 0031 — Queue events live in a contracts project other services reference

- Status: accepted
- Date: 2026-11-20
- Context: Another service consumes this service's events and must not depend on its domain.

## Decision

The events that leave the process (`IQueueEvent`) live in `Boilerplate.Contracts`, which references only `Boilerplate.Shared` and is packable; the domain references it and raises them. Wire names come from the namespace or are pinned with `[MessageName]`.

## What varies

Which events are published.

## Options

| Option | Cost | Why it was not chosen |
|---|---|---|
| Consumers reference the domain | Nothing | They couple to every internal type |
| Consumers copy the event types | No package | Copies drift silently |
| **A contracts project** | A package to version | n/a |

## Consequences

- Gained: consumers build against the contracts alone; the published surface is visible in one project.
- Lost or owed: renaming a contract member is a breaking change; the package must be versioned and published.
- Cost of going back: low.

## How it is enforced

`ContractsRulesTests`, `EventContractTests` at `architecture`, and `events-project.sh` (a consumer builds against the contracts only).
