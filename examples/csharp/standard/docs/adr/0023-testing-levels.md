# 0023 — How much testing structure the project starts with

- Status: accepted
- Date: 2026-11-22
- Context: The core ships unit tests with fakes for every class. Some failures only show when the pieces are wired together, when a real database answers, or when the code drifts from its own rules over months.

## Decision

The project starts at testing level `integration`. `handlers` adds a validator test per command and scenario tests through the real container, dispatcher and pipeline, built with `ValidateOnBuild`. `integration` adds `Acme.IntegrationTests`: repositories, the unit of work, the outbox and the broker against real PostgreSQL and RabbitMQ in containers (or `MORTIS_TEST_POSTGRES` / `MORTIS_TEST_RABBITMQ`), one database per test class; without either the tests are skipped, never passed. `architecture` adds reflection rules (layer references, every command and sync handler registered, no private method in a `*Service`), a golden file pinning every queue event's wire name and JSON shape, and an 80% line-coverage gate. Each level includes the ones before it.

## What varies

The level, and the coverage threshold.

## Options

| Option | Cost | Why it was not chosen |
|---|---|---|
| Unit tests only, whatever the project | Nothing | Wiring, SQL and contract drift are found in production |
| Every level for every project | Containers and a coverage gate in every CI | Too heavy for a prototype |
| **A level chosen per project, each including the ones before** | The chosen level's tests to keep | n/a |

## Consequences

- Gained: a missing registration fails a test, not the first request; the real SQL is exercised; a changed event contract is a reviewed change to one file.
- Lost or owed: Docker or a test database in CI from `integration`; the golden file is updated deliberately (`MORTIS_UPDATE_CONTRACTS=1`) when a contract changes on purpose.
- Cost of going back: low.

## How it is enforced

The level's own tests, and `testing.sh` (no integration project at `unit`; skipped, not passed, without a database; each architecture rule fails on a deliberately broken copy).
