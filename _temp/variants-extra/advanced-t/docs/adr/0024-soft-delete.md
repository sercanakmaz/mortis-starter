# 0024 — Deleting marks a row; it never removes it

- Status: accepted
- Date: 2026-11-20
- Context: Deleted data must stay for audit and undo, yet disappear from every read and free its natural key.

## Decision

A soft-deletable aggregate implements `ISoftDeletable` and raises `Deleted` and `Restored`. A named EF Core query filter hides deleted rows; ignoring it by name keeps the tenant filter. A natural key no other table references becomes a unique index filtered on `is_deleted = false`, so a deleted key can be reused and a restore that collides answers 409 `duplicate`; a natural key another table references (the example's SKU) stays an alternate key, because a foreign key needs a real unique constraint, and stays taken.

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

The domain and handler tests, `ProductConfigurationTests` (the named filters), and `soft-delete.sh` (a deleted product is invisible, its referenced SKU stays taken, a restore brings it back).
