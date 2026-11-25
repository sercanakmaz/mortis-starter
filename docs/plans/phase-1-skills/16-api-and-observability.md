# Phase 1, step 16 — API conventions and observability (knowledge only)

Needs: phase 1 steps 06. Size: medium. Options: `api-conventions`, `observability`. No C# is written, built or generated in this step.

## Goal

The things every real service ends up adding to its HTTP surface and its operations, done once and consistently.

## Design

`api-conventions`:
- One response shape for lists (`items`, `page`, `pageSize`, `total`) and the existing error body for every failure, including model-binding and unknown-route errors.
- **Unknown members are refused**: a request body with a property the command does not have returns 400 naming it (a silent typo in a request is otherwise accepted and ignored). Opt out per command.
- Partial update rule: `PUT` replaces, `PATCH` changes only the members present; the command distinguishes "absent" from "null" with an `Optional<T>`.
- API versioning by URL segment (`/v1/`), OpenAPI document per version, enum values as strings.
- Idempotency for `POST`: an `Idempotency-Key` header stored with the response for a retention period (this is the `idempotent-commands` add-on; it ships here because it is HTTP-shaped).

`observability`:
- Health endpoints: `/health/live` (process) and `/health/ready` (database, broker when on, outbox lag under a threshold).
- OpenTelemetry: a span per command, per event handler, per outbox publish and per consumed message; the trace context travels in the outbox row and the bus envelope headers so one trace covers a request and everything it caused; metrics for command duration, outbox pending count and age, consumer failures. Exporter chosen by settings (OTLP); none by default.
- Structured request logging with the correlation id, without bodies by default.

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

- [ ] A body with an unknown property returns 400 with the property name; the same request to an opted-out command succeeds.
- [ ] `PATCH` with `{"name": null}` clears the name and `{}` leaves it (tests).
- [ ] Repeating a `POST` with the same idempotency key returns the first response and creates one row.
- [ ] `/health/ready` turns unhealthy when PostgreSQL is stopped and when the outbox has rows older than the threshold.
- [ ] One trace id links the HTTP request span, the command span, the outbox publish span and the event consumer span (integration test with an in-memory exporter), with every transport.

## Out of scope

Dashboards, alert rules, log shipping, rate limiting.

## Notes (done)

Knowledge only (decision 48), with the developer's answers: unknown members refused with a per-command opt-out; `PUT` replaces and `PATCH` with `Optional<T>`; **no API versioning** (the plan's `/v1/` dropped; one OpenAPI document).

- Written, not built: architecture page `http-and-operations.md`; pages `api-conventions.md`, `idempotency.md`, `observability.md`; ADRs `0020`, `0021`, `0022`; `api-conventions.sh`, `idempotency.sh`, `observability.sh`, wired into `verify.sh` by `addons` (no variant turns them on yet).
- The list shape reuses the query side's `PagedResult` (`size`, not `pageSize`); with `queries` = `commands` the api-conventions add-on adds `PageRequest` and `PagedResult` for list commands.
- Idempotency is its own add-on (`idempotent-commands`), PostgreSQL only: rule R14 notes it with MongoDB. Only successes are stored; a failure frees the key.
- The trace context travels on the outbox row (`trace_parent`) and in the envelope headers (`traceparent`), not on the event. The cross-process proof is the shared `TraceId` of the API's and the consumer's log lines (`observability.sh`); `TraceLinkTests` proves it in-process with the in-memory exporter.
- Readiness is proved by starting the API with an unreachable database instead of stopping PostgreSQL, and by inserting an old outbox row the worker will not claim.
- `shape_for.py` now matches list keys (`[addons=observability]`); the manifest gained `option_changes`, the existing files each unbuilt option changes.
