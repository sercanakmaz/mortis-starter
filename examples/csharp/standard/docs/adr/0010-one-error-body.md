# 0010 — One error body, the same at every level

- Status: accepted
- Date: 2026-11-22
- Context: Clients branch on errors. Moving a project from a simple to a fuller error handling must not change what a client receives for the same failure.

## Decision

This project uses the `coded` level. Every failure answers `{ code, message, errors }` with a fixed status: 404 not found, 422 validation (with `errors` per field), 409 business rule, 500 unexpected (with a reference, never the exception's message). At `coded` and above, a unique index answers 409 `duplicate` and a foreign key 400 `reference_violation`; at `minimal` both are unexpected. At `catalogued`, every code is declared once with its default message in `ErrorCatalogue`. 4xx is logged as a warning, 5xx as an error.

## What varies

How much a failure says, never its shape or its status.

## Options

| Option | Cost | Why it was not chosen |
|---|---|---|
| A body and status per level | Nothing extra | Moving up a level breaks clients |
| Problem details (RFC 9457) | Framework support | One shape across the Api and the Console was simpler |
| **One body, fixed statuses, levels add detail** | The table in `docs/architecture.md` | n/a |

## Consequences

- Gained: clients are never broken by a change of level; unexpected failures leak nothing and can be found by reference.
- Lost or owed: at `minimal` a duplicate is a 500 until the level moves up.
- Cost of going back: low.

## How it is enforced

`ExceptionHandlerTests`, `ErrorCatalogueTests` at `catalogued`, `verify.sh` (rule codes are constants), and the conformance table of failures.
