# Phase 1, step 10 — Testing levels (knowledge only)

Needs: phase 1 steps 08. Size: medium. Options: `tests-handlers`, `tests-integration`, `tests-architecture` (a level: each includes the ones before it; `unit` is the core). No C# is written, built or generated in this step.

## Goal

A project chooses how much testing structure it starts with. The structure is real: each level ships working tests for the core and for every module that is on.

## Design

| Level | Adds | Needs |
|---|---|---|
| `unit` | aggregate, value object, service and kernel tests with fakes and builders | nothing |
| `handlers` | command handler, validator, dispatcher, filter, scenario and mapping-shape tests | nothing |
| `integration` | a second test project `<Name>.IntegrationTests`: repositories, unit of work, outbox, relay and (when on) the bus against real PostgreSQL / RabbitMQ in containers; one fixture per collection; a database per test class | Docker, or connection strings in environment variables |
| `architecture` | layer-dependency tests written with reflection (Domain references nothing but Shared; Application never references Infrastructure; no type named `*Service` has a private method; every command has a registered handler; every event handler is registered); an event-contract test (the wire name and JSON shape of every queue event is pinned in a golden file); a coverage threshold in the test settings | nothing |

- The house rules hold in the integration project too: fixtures and builders in `TestSupport`, test classes hold only tests.
- Every option's reference page from now on lists its tests by level, so a project generated at a lower level simply does not get the higher ones.
- The "every command has a registered handler" test closes the gap ADR 0001 names (a missing registration fails only at the first `Send`).

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

- [ ] A project generated at `unit` has no handler or host test and still passes `verify.sh`.
- [ ] At `integration`, the tests run against containers when Docker is present and against `MORTIS_TEST_POSTGRES` / `MORTIS_TEST_RABBITMQ` when it is not; with neither, they are reported as skipped, not passed.
- [ ] At `architecture`, each rule has a deliberately broken fixture proving the test fails (for example a handler removed from registration).
- [ ] Changing a queue event's property name fails the contract test.

## Out of scope

End-to-end tests through HTTP beyond what the conformance suite does; load tests.
