# 0030 — Bounded contexts are folders that talk through events and ids

- Status: accepted
- Date: <the date the project was generated>
- Context: Groups of aggregates have stopped sharing a vocabulary. Without a boundary, one group reaches into another's types and the two can no longer change apart.

## Decision

Code lives under `Contexts/{Context}` in every project. A context refers to another only through the other's events and ids; a reaction to another context's event is a handler or consumer in the reacting context. One process and one database.

## What varies

The contexts and which aggregates belong to each.

## Options

| Option | Cost | Why it was not chosen |
|---|---|---|
| One flat model | Nothing | Everything couples to everything |
| A service per context now | Deployments, network, data copies | The split is not proven yet |
| **Folders with a rule test** | A path segment and a test | n/a |

## Consequences

- Gained: contexts change apart and can become services later by moving folders.
- Lost or owed: a type two contexts both need is duplicated or becomes an event; wire names follow the new namespaces.
- Cost of going back: medium.

## How it is enforced

`ContextRulesTests`, the review skill's body-reference check, and `contexts.sh`.
