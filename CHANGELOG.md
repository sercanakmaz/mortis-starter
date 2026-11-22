# Changelog

All notable changes to the skills and the template. Newest first.

## 2026-11

### Added
- Phase 1 step 01: the language-neutral architecture layer in `skills/mortis-architect/references/architecture/` (layers, 18 building blocks, request lifecycle, events, 50-row placement guide, 10 scenarios, notation, glossary), `tests/check_architecture.py` and placement fixtures. Ledger `docs/unverified.md` and `docs/generation-log.md` created. The pilot generation after step 03 is switched on.

- Phase 1 step 02: the `mortis-architect` skill: interview, `MORTIS.md` schema (25 keys, rules R1 to R12), shared parser, validator, preset writer, 15 option pages, four evals; 26 validator tests.

- Architecture layer: explicit cardinality (one command can raise many events, one event can have many handlers), scenario 11, and a decision cheat sheet with a flow diagram and common pitfalls in the placement guide.

- Architecture layer, decision 28: command handlers are use cases that may change several aggregates when the action is about all of them; domain services hold logic that fits no single aggregate; new anti-corruption layer block; the cheat sheet decides between the command handler and an event handler for a second aggregate.

- Decisions 28 to 30: use cases may change several aggregates; standard preset has no translated messages; one repository is one service with one `MORTIS.md`.

- Phase 1 step 03: `mortis-scaffold` rebuilt as knowledge and checks: 20 C# pages with member snippets, file manifest, kernel shape, naming table, `verify.sh` with structure, house-rule, shape and conformance checks, `tests/check_installable.sh`, `tests/check_docs.py` rewritten. The example domain is specified in roles (`example-domain.md`). Old per-layer references removed.

- Pilot generation of the lean preset (mid-size model): 11 of 12 checks, the failure a skill defect (fixed). Procedure reordered, usings tables on every page, worked-slice order fixed (`docs/pilot.md`).

- Phase 1 step 04: one `Context` on the command (`CommandContext`, `IContextCommand`); events stamped with correlation, causation, tenant and language by `EventContext`; ADR 0007; a house rule and three conformance checks.

- Decision 31: domain services are pure by default, may read through domain interfaces (narrow read ports preferred), never write; `verify.sh` enforces the no-write rule in the Domain project.

- Phase 1 step 05: integer keys assigned by the database (`IdentityAggregateConfiguration`, `MarkCreated` after the insert); a `Category` aggregate in the example; ADR 0008.

- Phase 1 step 06: the `correlation` add-on (header in and out, logging scope, ADR 0009); correlation and causation on every command log line; the Console prints its run's id.

- Phase 1 step 07: three error levels with one body and fixed statuses (decision 32); `ErrorReference` for unexpected failures; `ErrorCatalogue` at `catalogued`; a `Failure` block in layer 1; ADR 0010.

- Phase 1 step 18 (decision 50), knowledge only: the eight advanced add-ons of the schema. Architecture page `advanced-addons.md`; C# pages `soft-delete.md`, `caching.md`, `scheduler.md`, `storage.md`, `secrets.md`, `loop-guard.md`, `contexts.md`, `events-project.md`; ADRs `0024` to `0031`; a conformance script each, wired by `addons` (not run); `shape-advanced.txt`; R14 widened to `scheduler` and `secrets` with MongoDB; `lang/csharp.sh` honours `MORTIS_LOG`.

- Phase 1 step 10 (decision 49), knowledge only, written after step 16: testing levels. Page `testing-levels.md` (validator and scenario tests with `ValidateOnBuild` at `handlers`; `{Name}.IntegrationTests` against PostgreSQL and RabbitMQ in containers or from `MORTIS_TEST_*`, skipped without them, at `integration`; layer, registration and service rules, the queue-event contract golden file and an 80% coverage gate at `architecture`); `options/testing.md` with the levels' pseudo-code; ADR `0023`; `testing.sh` (wired when `testing` is not `unit`, not run). The core's handler tests stay at `unit`.

- Phase 1 step 16 (decision 48), knowledge only: API conventions and observability. Architecture page `http-and-operations.md`; C# pages `api-conventions.md` (one error body for binding and routing failures, unknown members refused with a per-command opt-out, `Optional<T>` for `PATCH`, the customer `PATCH` slice, no versioning), `idempotency.md` (`Idempotency-Key` on `POST`, a table of keys with their responses) and `observability.md` (live and ready health, OpenTelemetry spans linked across the outbox and the bus by `traceparent`, metrics, trace ids on log lines, request logging); ADRs `0020` to `0022`; `api-conventions.sh`, `idempotency.sh`, `observability.sh` (not run); rule R14 (idempotency with MongoDB, a note); `shape_for.py` matches list keys such as `addons`; the manifest lists the existing files each unbuilt option changes (`option_changes`).

- Phase 1 step 15 (decision 47): no system user (event consumers run as the user whose command raised the event) and a Console without tokens (`--user`, `--email`, `--tenant`, `--language`), built in every variant; permissions written as knowledge (`permissions.md`: every command declares one, checked before validation, the claim asked of the developer), ADR 0019, conformance `permissions.sh` (not run yet).

- Phase 1 step 14 (decision 46), knowledge only: the query side. Pages `queries.md` (queries with the caller's context, reader ports and view records in Domain, typed list queries, `PagedResult`) and `read-models.md` (`OrderSummary` refreshed by sync handlers, `rebuild-order-summary`), layer 1 `architecture/queries.md`, ADR 0018, conformance `queries.sh` (its `commands` branch runs now); `"unbuilt"` manifest entries.

- Phase 1 step 13 (decision 45), knowledge only: MongoDB as the database. Page `infrastructure-mongo.md` with hand-written snippets (repository with a version element, session unit of work on a replica set, outbox collection, indexes at start in Development and by `ensure-indexes`), layer 1 `architecture/documents.md`, ADR 0017, conformance `database-mongo.sh` (not run yet); EF Core files gated by `database` = `postgres` in the manifest and the kernel shape.

- Phase 1 step 12 (decision 44): languages in the advanced variant: the language from the token's claim then `Accept-Language`, failures answered from `messages.<language>.json` by code and parameters, `Product.Name` as a `LocalizedText` in jsonb with `RenameProductCommand`, `Language` on every log line. Page `languages.md`, layer 1 `architecture/languages.md`, ADR 0016, conformance `languages.sh`.

- Phase 1 step 11 (decisions 42, 43; step 10 skipped): multi-tenancy by interface in the advanced variant (`IMultiTenant`, a scoped `TenantScope`, query filters and stamping, 403 `tenant_required`, tenant-first keys, the outbox row's tenant) and, because the tenant is the signed-in user's claim, step 15's sign-in in standard and advanced (JWT bearer validation, claims in the context, Console `--token`, `dev-token` in Development only). Pages `authentication.md` and `tenancy.md`, layer 1 `architecture/tenancy.md`, rule R13, ADRs 0014 and 0015, conformance `auth.sh` and `tenancy.sh`; `build_shape.py` explains shape differences by one or two options.

- Decision 41: async event handlers are renamed event consumers (`BaseEventConsumer<TEvent>`, `*EventConsumer`) and are registered only by an explicit `AddEventConsumer<TConsumer>(endpoint => endpoint.Retry(3))` line in the host that runs them; `Retry(n)` is per consumer in every publishing model and the global retry settings are gone. New page `event-consumers.md` (`EventConsumerEndpoint`, `EventConsumerModule`, `EventConsumerRegistry`); the database outbox row gets `consumer` and `max_attempts`; `ReceiveEndpoint`, the bus's `EventConsumer` class and `AddEventConsumers` are removed.

- Phase 1 step 09 (decisions 38, 39; every async handler is its own consumer, with its own queue on RabbitMQ): our own RabbitMQ bus, wire-compatible with MassTransit 8, in the advanced variant: publisher with confirms, one receive endpoint per handler configured in code, no inbox (handlers must be safe to run twice), `<queue>_error` with the MassTransit fault headers; `rabbitmq.md`, ADR 0013, `transport-rabbitmq.sh`, and interoperability tests against MassTransit 8.5.12 in `_temp/interop/`.

- Decision 37: three complete reference solutions in `_temp/variants/` (lean, standard, advanced), each verified by `verify.sh`, replace the overlay tooling as the source of the C# pages; decision 28 now applied in real code (no `*Service` classes, handler tests written out); `tests/check_variants.py` keeps them in step; the pilot project is removed.

- Phase 1 step 08 (decisions 33 to 36): a project holds only its options' code. Three publishing models: `none` (`EventPublisher`), `background` and `after-command` (one outbox table, a repository and a worker; one transaction per row). The Console is a host that runs one command. Every log line is one flat JSON object with `CorrelationId` first (`logging.md`, ADR 0012). Fixed on the way: the rule-code house rule now skips tests, `ErrorCatalogueTests` no longer warns.
