# Unverified

What phase 1 has not proven. Since decision 37 every page's code is compiled, tested and verified (`verify.sh`, conformance on PostgreSQL) in the three reference solutions in `_temp/variants/`; rows that were only about compiling and running that code were closed then. What remains: anything no variant exercises, and whether a model generates the variants' code from the skills alone (phase 2). One row per open item.

| Step | Item | What awaits proof |
|---|---|---|
| 01 | `skills/mortis-architect/references/architecture/` (8 files) | A fresh model places things correctly (reader test, below); the pseudo-code agrees with the C# pages once they exist |
| 01 | `tests/check_architecture.py`, `tests/fixtures/` | Runs against the lean variant's Shared types while `_temp/variants` exists |
| 02 | `references/options/*.md`, `presets.md`, `interview.md`, `SKILL.md` | The interactive interview (questions asked in rounds) has never run; only the unattended path was exercised |
| 02 | `scripts/*.py`, `references/schema.json` | Unit-tested and run on four prompts; the rules say what combinations are refused, but whether the *scaffold* can build every accepted combination is proven only in phase 2 |
| 03 | `project-docs*.md`, generated `AGENTS.md`, `docs/architecture.md`, the ADRs | Present in the variants; never read back by a model working in a generated project |
| 03 | `verify.sh` on a small model's output | Ran in full on the three variants (hand-kept); on a model's generated project only once, in the old pilot |
| 03 | `scripts/conformance/*.sh` | Pass on the three variants; no deliberate break has been shown to turn each check red |
| 08 | Two workers on one table; a crash mid-transaction | No check; safe by construction (`skip locked`, one transaction per row), unproven |
| 08 | The outbox's raw SQL with `naming.database` = `as-code` | Written for `snake_case` only; no variant uses `as-code` |
| 09 | MassTransit's Newtonsoft serializer reading what we publish, and enums or nested types in a message | Checks 1 to 4 pass against MassTransit 8.5.12; our example events have no enum or nested type |
| 09 | `events.transport` = `rabbitmq` with `hosts.worker` = `in-api` | No variant builds it; the Api would then host the receive endpoint |
| 11 | `Auth:Authority` (keys from an OpenID Connect authority's metadata) | Only the development signing key is exercised; no identity server runs in the checks |
| 11 | Tenancy with `events.transport` = `in-process` or `database` | The pages say where `EventPublisher` and `OutboxWorker` enter the tenant; no variant builds it |
| 11 | `tenancy.key` = `text`; `tenancy.resolver` = `header` or `subdomain` | Page notes only; no variant builds them |
| 11 | `TenantScope.Bypass` used by a real job | Unit-tested; no example job crosses tenants |
| 12 | `languages.mode` = `messages` without `data`, and `ErrorCatalogue` with messages (catalogued without languages) | Page snippets only; no variant builds them |
| 12 | Translated validation field messages; searching by a translated field | Not built; the pages describe the JSON path and the per-language index |
| 13 | `infrastructure-mongo.md`: every snippet (`MongoModule`, `MongoClassMaps`, `MongoUnitOfWork`, `MongoRepository`, `MongoExceptionHandler`, `ConcurrencyConflictException`, `MongoIndexes`, `MongoIndexesService`, the MongoDB `OutboxRepository`, the repositories, the tenancy filter, the tests) | Written by hand against MongoDB.Driver 3.14.1; never compiled |
| 13 | `conformance/database-mongo.sh` and its wiring in `verify.sh` | Never run; needs a replica set and `mongosh` |
| 13 | `shape-mongo.txt` lines in `kernel-shape.txt`; the `database` gates in `manifest.json`; ADR `0017` | No MongoDB project generated yet |
| 13 | Assumptions to check first: `AutoMap` finds the Domain's non-public constructors and `protected set` properties; a duplicate inside a transaction surfaces as `MongoCommandException` 11000; the claim's write conflict carries the `TransientTransactionError` label | From the driver's documentation, not from a run |
| 14 | `queries.md` and `read-models.md`: every snippet (query types, dispatcher, readers, the example's queries, the read model, its projection SQL and handlers, the rebuild, the tests) | Written by hand; never compiled |
| 14 | `conformance/queries.sh` for `split` and `read-models`; ADR `0018`; `shape-queries.txt` | Never run; the presets that choose them still build `commands` |
| 15 | `permissions.md`: every snippet (`IAuthorizedCommand`, the permission constants, the pipeline check, `PermissionsOf`, permissions on the context and events, the Console's `--permissions`, the tests); ADR `0019`; `conformance/permissions.sh` | Written by hand; never compiled or run. No kernel-shape lines yet, since `auth` = `jwt` is built without them |
| 16 | `api-conventions.md`, `idempotency.md`, `observability.md`: every snippet (error body for binding and routing failures, `Optional<T>` and its converter, the customer `PATCH` slice, the idempotency filter and store, telemetry, the command and handler spans, the trace parent on the outbox row and in the envelope, health checks, the request log, the tests) | Written by hand; never compiled. Whether System.Text.Json treats a member named like a `[JsonIgnore]` member as unknown under `Disallow` |
| 16 | `conformance/api-conventions.sh`, `idempotency.sh`, `observability.sh`; ADRs `0020` to `0022`; `shape-api.txt`; rule R14 | Never run; the standard and advanced presets choose these add-ons but their variants do not build them |
| 10 | `testing-levels.md`: every snippet (validator and scenario tests, `ScenarioHost`, the integration project and its fixtures, the architecture rules, the event-contract golden file, the coverage gate); `options/testing.md`'s level pseudo-code | Written by hand; never compiled. Whether `Xunit.SkippableFact` reports skips as skipped under `dotnet test`'s console logger |
| 10 | `conformance/testing.sh` (wired for `testing` above `unit`); ADR `0023` | Never run; every variant is generated at `unit` though the standard and advanced presets choose `integration` and `architecture` |
| 18 | `soft-delete.md`, `caching.md`, `scheduler.md`, `storage.md`, `secrets.md`, `loop-guard.md`, `contexts.md`, `events-project.md`: every snippet; `architecture/advanced-addons.md` | Written by hand; never compiled. Whether EF Core 10's named query filters combine with the tenant filter as written; whether HybridCache's tag removal reaches the local cache of the process that removed |
| 18 | `conformance/soft-delete.sh`, `caching.sh`, `scheduler.sh`, `storage.sh`, `secrets.sh`, `loop-guard.sh`, `contexts.sh`, `events-project.sh` (wired by `addons`); ADRs `0024` to `0031`; `shape-advanced.txt`; R14 widened to `scheduler` and `secrets` | Never run; the advanced preset chooses most of these add-ons but its variant builds none |
| 17 | `mortis-develop`: `SKILL.md`, 14 recipes, `tdd-order.md`, `canonical-shapes.md`, `where-else.md`, `csharp/recipes.md`, `new_aggregate.sh`; five evals | Never used by a model on a generated project; the evals are written, not run. `check_docs.py` checks the recipes' tags against the schema and their pages exist |
| 17 | `mortis-review`: `SKILL.md`, `checklist.md`, `findings-format.md`, five evals | The judgement checklist has never been applied by a model; the evals are written, not run |
| 17 | `review.sh` / `review.py` (14 checks) | Run in phase 1 on the three variants (no finding) and on 14 fixtures (exactly the planted finding each, `tests/check_review.py`); never on a model's change. Heuristic checks (C03, C09, C10) may miss shapes the fixtures do not plant |
| 37 | Values in no preset: `int` or `long` default keys, `naming.database` = `as-code`, `hosts.worker` = `separate` with `after-command`, `in-api` with `background` | Page snippets only; no variant builds them |
| 37 | Whether a model reading only the skills generates code equal to the variants | Phase 2 |
