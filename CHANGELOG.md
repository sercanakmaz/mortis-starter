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

- Decision 41: async event handlers are renamed event consumers (`BaseEventConsumer<TEvent>`, `*EventConsumer`) and are registered only by an explicit `AddEventConsumer<TConsumer>(endpoint => endpoint.Retry(3))` line in the host that runs them; `Retry(n)` is per consumer in every publishing model and the global retry settings are gone. New page `event-consumers.md` (`EventConsumerEndpoint`, `EventConsumerModule`, `EventConsumerRegistry`); the database outbox row gets `consumer` and `max_attempts`; `ReceiveEndpoint`, the bus's `EventConsumer` class and `AddEventConsumers` are removed.

- Phase 1 step 09 (decisions 38, 39; every async handler is its own consumer, with its own queue on RabbitMQ): our own RabbitMQ bus, wire-compatible with MassTransit 8, in the advanced variant: publisher with confirms, one receive endpoint per handler configured in code, no inbox (handlers must be safe to run twice), `<queue>_error` with the MassTransit fault headers; `rabbitmq.md`, ADR 0013, `transport-rabbitmq.sh`, and interoperability tests against MassTransit 8.5.12 in `_temp/interop/`.

- Decision 37: three complete reference solutions in `_temp/variants/` (lean, standard, advanced), each verified by `verify.sh`, replace the overlay tooling as the source of the C# pages; decision 28 now applied in real code (no `*Service` classes, handler tests written out); `tests/check_variants.py` keeps them in step; the pilot project is removed.

- Phase 1 step 08 (decisions 33 to 36): a project holds only its options' code. Three publishing models: `none` (`EventPublisher`), `background` and `after-command` (one outbox table, a repository and a worker; one transaction per row). The Console is a host that runs one command. Every log line is one flat JSON object with `CorrelationId` first (`logging.md`, ADR 0012). Fixed on the way: the rule-code house rule now skips tests, `ErrorCatalogueTests` no longer warns.
