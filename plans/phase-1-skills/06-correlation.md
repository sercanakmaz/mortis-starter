# Phase 1, step 06 — Correlation and causation (knowledge only)

Needs: phase 1 steps 04. Size: small. Options: `correlation`. No C# is written, built or generated in this step.

## Goal

One id follows a request through every command, event, log line and outgoing message; a second id says what caused each step. Step 04 put both on the context; this step connects them to the outside.

## Design

- Api: a middleware reads `X-Correlation-Id` (a valid `Guid`; anything else is ignored and a new one is generated), puts it on the context of every command in the request, opens a logging scope with it, and returns it in the response header.
- Console: generates one per run and prints it with the result.
- Logging: `LoggingCommandHandlerMiddleware` and the event dispatchers log `CorrelationId`, `CausationId`, `EventId`, command and event type as structured properties.
- Events: already stamped by step 04. This module adds the checks.
- Outgoing messages (steps 08 and 09) read the ids from the event; nothing to do here except the document.

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

- [ ] The id sent in the header is the id on the response and on every log line of the request and of the handlers it caused (conformance).
- [ ] A malformed header produces a new id, not an error (test).
- [ ] A chain command → event → handler → command → event has one correlation id and each event's causation id is the previous event's id or none for the first (test).

## Out of scope

OpenTelemetry trace context (step 16).

## Notes (done)

- Step 04 already made correlation and causation core: every command and event carries them. What is always on now: the command log lines (`LoggingCommandHandlerMiddleware`) and the async failure line show correlation and causation, the published-event line shows both (step 04), and the Console prints `correlation <id>` for its run.
- What the `correlation` add-on adds: `CorrelationIdMiddleware` (moved out of the kernel; it reads and returns `X-Correlation-Id`, hands the id to the filter through `HttpContext.Items`, opens a logging scope), one line in the Api's `Program.cs`, its tests, ADR 0009. Without it, `ContextCommandFilter` makes a new id per request. Page: `references/csharp/correlation.md`.
- `manifest.json` entries for an add-on carry `"when": {"addons": "correlation"}`; `structure.py` reads a `when` on a list key as "contains". `verify.sh` runs `conformance/addon-correlation.sh` when the add-on is on: the header comes back, a malformed header gets a new id, and with the example three log checks (the command, the published event, the command an event consumer sent, with its causation).
- The chain test asked for here (command, event, handler, command, event under one correlation, each causation the previous event) is covered by step 04's `EventContextTests` and `CommandPipelineTests` plus the `stock-flow` conformance checks; no new unit test was added.
- Found, not done: the Console prints its correlation id on stdout before the scenario output; the conformance parsers skip it because they look for the first line that starts with `{`.
