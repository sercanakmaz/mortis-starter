# Phase 1, step 13 — MongoDB as the database (knowledge only)

Needs: phase 1 steps 08, 11. Size: large. Option: `database` = `mongo`. No C# is written, built or generated in this step.

## Goal

`database` = `mongo` gives the same solution with the same Domain and Application code. Only Infrastructure differs.

## Design

- **An aggregate is one document.** Child entities and value objects are embedded; there is no mapping for them beyond class maps. Class maps live in Infrastructure (`Persistence/Mongo/Maps/`); the Domain carries no driver attribute.
- **Repository.** `MongoRepository<TAggregate, TKey>` implements the same `IRepository`. `FindByIdTrackingAsync` remembers the loaded version; `UpdateAsync` replaces the document with a filter on id and version and throws the same concurrency exception the EF repository does when nothing matched.
- **Concurrency.** A `Version` number on the document root, incremented on every update.
- **Unit of work.** A client session with a multi-document transaction. This needs a replica set: the compose file starts a single-node replica set and the README says a standalone server is not supported.
- **Outbox and inbox.** Collections `outbox_messages` and `inbox_messages`, written in the same session. Claiming rows uses `findOneAndUpdate` on `status` and `locked_until`.
- **Keys.** `Guid` version 7 stored as a standard UUID. Integer keys are not offered with MongoDB; rule added to the schema.
- **Indexes instead of migrations.** `Persistence/Mongo/Indexes/` declares them; a hosted step ensures them at start in development and a Console command `ensure-indexes` does it in deployment. Unique indexes start with `tenantId` when tenancy is on.
- **Tenancy.** The filter is added to every query built by the repository base; there is no global filter to forget, so repositories must build queries only through the base. An architecture test enforces it.
- **Errors.** Duplicate key (11000) maps to the same `DuplicateException`.
- References between aggregates are ids, as with PostgreSQL; there is no foreign key, so the "does it exist" check is in the domain service where the rule needs it.

## Deliverables

Knowledge only. Nothing here is compiled or run in this phase.

1. **Layer 1** (`skills/mortis-architect/references/architecture/` and `references/options/`): what the option is, its pseudo-code, its rules, when to choose it and what it costs.
2. **Layer 2 for C#** (`skills/mortis-scaffold/references/csharp/`): one page per type or concern, as contract, behaviour rules, a snippet for every member with a real body, and the tests to write with their snippets. Snippets, never whole files.
3. **Entries** in `manifest.json` (every file the option adds, and which existing files it changes), `kernel-shape.txt` and `naming.md`.
4. **Conformance checks** (`skills/mortis-scaffold/scripts/conformance/`): one check per item under "Behaviour to prove in phase 2", selected by `MORTIS.md`. Written now, first run in phase 2.
5. **The decision record** a generated project gets for this option, as an outline in `references/csharp/project-docs.md`.
6. **A line in `docs/unverified.md`** for every page and check of this step: none of it has been compiled or run yet. Phase 2 removes the lines as it proves them.
7. `tests/check_docs.py` and `tests/check_installable.sh` pass.

## Behaviour to prove in phase 2

Each item becomes a conformance check or a test snippet now. None is run in this phase; phase 2 runs them on the generated project.

- [ ] The example's Console scenarios (`order-flow`, `stock-flow`, `blocked-customer`) produce the same results on MongoDB as on PostgreSQL (the conformance suite reads the documents back).
- [ ] A rolled-back command leaves no document and no outbox entry.
- [ ] Two concurrent stock updates: one succeeds, the other is retried and succeeds, the final stock is right.
- [ ] The Domain and Application projects of two projects generated with `postgres` and with `mongo` are identical file for file.
- [ ] `keys.default` = `int` with `mongo` is refused by the validator.

## Out of scope

Sharding, change streams as a relay, time-series collections, a migration tool for document shape changes (describe the "version field plus upgrade on read" approach in the reference only).

## Traps

- The .NET driver's default Guid representation has changed across versions; set the standard representation explicitly and pin it with a test that reads raw bytes.
- A transaction on a standalone server fails at the first write, not at connect. The conformance suite must check the replica set first and say so plainly.
