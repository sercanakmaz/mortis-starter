# Phase 1, step 14 — The query side (knowledge only)

Needs: phase 1 steps 03. Size: medium. Options: `queries-split`, `queries-read-models` (`queries`: `commands` is the core, `split` the first, `read-models` both). No C# is written, built or generated in this step.

## Goal

Today a read is a `Get*Command`. Offer a real query side for projects that want one, and projected read models for those that need fast lists.

## Design

`split`:
```
interface IQuery<TResult>
QueryHandler<TQuery, TResult>
IQueryDispatcher.Ask(query) -> TResult
```
- A query has no transaction, raises no event and never calls a domain service that writes. It may have a validator.
- A query handler reads through a read-only port declared in Application (`IProductReader`), implemented in Infrastructure with no tracking and a projection straight to the DTO. It does not load an aggregate to map it.
- Paging, sorting and filtering types (`PageRequest`, `PagedResult<T>`, a closed set of filter operators) live in `Shared`.
- The example's `Get*Command`s become `Get*Query`s and a `ListProductsQuery` appears (in `example-domain.md`).
- Hosts send queries through `IQueryDispatcher`; the Console runs them by name like commands.

`read-models`:
- A read model is a table (or collection) shaped for one screen, owned by Infrastructure, updated by an event handler. A sync handler keeps it consistent with the write; an async one makes it eventually consistent and needs an outbox (rule R10).
- A Console command rebuilds a read model from the aggregates.
- Example: `OrderSummary` (order number, customer name, total, line count), updated by sync handlers.

## Deliverables

Knowledge only. Nothing here is compiled or run in this phase.

1. **Layer 1** (`skills/mortis-architect/references/architecture/` and `references/options/`): what the option is, its pseudo-code, its rules, when to choose it and what it costs.
2. **Layer 2 for C#** (`skills/mortis-scaffold/references/csharp/`): one page per type or concern, as contract, behaviour rules, a snippet for every member with a real body, and the tests to write with their snippets. Snippets, never whole files.
3. **Entries** in `manifest.json` (every file the option adds, and which existing files it changes), `kernel-shape.txt` and `naming.md`.
4. **Conformance checks** (`skills/mortis-scaffold/scripts/conformance/`): one check per item under "Behaviour to prove in phase 2", selected by `MORTIS.md`. Written now, first run in phase 2.
5. **The decision record** a generated project gets for this option, as an outline in `references/csharp/project-docs.md` (`0010-queries-and-read-models.md`).
6. **A line in `docs/unverified.md`** for every page and check of this step: none of it has been compiled or run yet. Phase 2 removes the lines as it proves them.
7. `tests/check_docs.py` and `tests/check_installable.sh` pass.

## Behaviour to prove in phase 2

Each item becomes a conformance check or a test snippet now. None is run in this phase; phase 2 runs them on the generated project.

- [ ] An architecture test: no query handler depends on `IUnitOfWork`, a domain service or a repository's write methods.
- [ ] `ListProducts` with paging and a filter issues one SQL statement that selects only the DTO's columns (integration test capturing the SQL).
- [ ] `OrderSummary` equals the aggregate after every example scenario, and `rebuild-order-summary` recreates it from empty.
- [ ] A project generated with `queries` = `commands` has no query type and passes.

## Out of scope

GraphQL, OData, a separate read database, full-text search.

## Notes (done)

Knowledge only, as planned (decision 46), though two presets choose these values; the variants keep `commands` until phase 2.

- Pages `queries.md` and `read-models.md`, layer 1 `architecture/queries.md`, ADR `0018` (the plan's `0010` was taken), `queries.sh`, `shape-queries.txt`; every piece is a line in `docs/unverified.md`.
- Changed from the plan: the reader port is in Domain with view records, because Infrastructure must not reference Application; the list's total is a second count statement, so "one SQL statement" became "one statement for the page"; read models live in Infrastructure with their own sync handlers that write only the read model.
- `queries.sh` checks with `commands` that no query type exists; that branch runs today (lean 14, standard 16).
- Manifest entries no variant builds now carry `"unbuilt": true` (MongoDB's too); `build_shape.py` appends every `shape-*.txt`.

