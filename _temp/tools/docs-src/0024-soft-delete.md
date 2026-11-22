# 0024 — Deleting marks a row; it never removes it

- Status: accepted
- Date: <the date the project was generated>
- Context: Deleted data must stay for audit and undo, yet disappear from every read and free its natural key.

## Decision

A soft-deletable aggregate implements `ISoftDeletable` and raises `Deleted` and `Restored`. A named EF Core query filter hides deleted rows; ignoring it by name keeps the tenant filter. Natural unique keys are unique indexes filtered on `is_deleted = false`, so a deleted key can be reused; a restore that collides answers 409 `duplicate`.

## What varies

Which aggregates are soft-deletable.

## Options

| Option | Cost | Why it was not chosen |
|---|---|---|
| Hard delete | Nothing | Audit and undo are lost |
| An archive table | A copy per aggregate | Two schemas to keep in step |
| **A flag, a named filter, filtered unique indexes** | Raw SQL must add the filter by hand | n/a |

## Consequences

- Gained: deletes are reversible and audited; a deleted key is free again.
- Lost or owed: tables keep growing until the project writes a retention job; raw SQL and bulk updates add `is_deleted = false` by hand.
- Cost of going back: medium (rows deleted while on stay as rows).

## How it is enforced

The domain and handler tests, `ProductConfigurationTests` (the filter and the filtered index), and `soft-delete.sh` (a deleted product is invisible, its SKU can be reused, a restore brings it back).
