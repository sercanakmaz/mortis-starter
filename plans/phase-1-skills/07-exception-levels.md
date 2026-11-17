# Phase 1, step 07 — Exception handling levels (knowledge only)

Needs: phase 1 steps 03. Size: small. Options: `exceptions-minimal`, `exceptions-coded`, `exceptions-catalogued` (a level: exactly one). No C# is written, built or generated in this step.

## Goal

Three depths of error handling a project can choose from, with the same error body on the wire so a project can move up a level without breaking its clients.

## Design

Error body at every level: `{ "code": "...", "message": "...", "errors": [ { "field": "...", "code": "...", "message": "..." } ] }`.

| Level | Types | HTTP mapping | Extras |
|---|---|---|---|
| `minimal` | `NotFoundException`, `ValidationFailedException`, `BusinessRuleException` | 404, 400, 422; everything else 500 | none |
| `coded` (today's behaviour) | adds `ExceptionBase` with a stable `Code`, `DuplicateException`, `ForbiddenException`, `InvalidParameterException`, `ReferenceViolationException`, `TransactionRequiredException` | adds 409, 403 | `DatabaseExceptionHandler` turns unique and foreign-key violations into domain errors |
| `catalogued` | adds an error catalogue: every code is declared once (`ErrorCodes`), carries parameters, and belongs to a module of the domain | same | parameters for translated messages (step 12); log level by status class (4xx warning, 5xx error); an `UnexpectedException` wrapper with an error fingerprint; a test that fails when a code is thrown but not declared |

The Console's exit-code contract (1 unknown name, 2 domain error) holds at every level.

Go renders the same levels with sentinel errors, typed errors and the same body.

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

- [ ] The same five requests (not found, invalid body, business rule, duplicate, unexpected failure) return the documented status and body at each level (conformance, one table).
- [ ] At `catalogued`, throwing an undeclared code fails a unit test.
- [ ] Projects generated at `minimal` and at `coded` have identical controllers and handlers (compare the two generation runs).

## Out of scope

Translated messages (step 12).

## Notes (done)

- The maintainer's answers (decision 32) changed the plan: the same statuses at every level (the plan's table gave `minimal` other statuses); `minimal` drops only the database error translation (no `DatabaseExceptionHandler`, `SaveChangesAsync` without a `catch`); the error body keeps `errors` as `{ field: [messages] }`.
- At every level: `ExceptionHandler` logs 4xx as a warning and 5xx as an error with the exception, and answers an unexpected failure with `ErrorReference.Of(exception)` (8 hex characters, the same for the same failure at the same place), never with its own message. New kernel file `ErrorReference.cs`, new kernel test `ExceptionHandlerTests`.
- `catalogued`: `src/{Name}.Domain/ErrorCatalogue.cs` (the seven shared codes and every rule code by its constant) and `ErrorCatalogueTests` (fails when a `*Code` constant of a `*Constants` class is missing). Message parameters are left to step 12.
- House rule at every level: a rule code is a constant; `new BusinessRuleException("...")` fails `verify.sh`.
- Layer 1 has a `Failure` building block now (the gap noted in step 01); the template's exception types map to it.
- `manifest.json`: a `when` may list values ("one of"); `DatabaseExceptionHandler.cs` is `{"exceptions": ["coded", "catalogued"]}`.
- Conformance: `example-http.sh` reads `EXCEPTIONS` and expects 500 `unexpected_error` for duplicates and foreign keys at `minimal`. An unexpected failure cannot be caused through the example's requests; `ExceptionHandlerTests` proves it.
- Not done: comparing `minimal` and `coded` generations for identical controllers and handlers is a phase 2 comparison; by design they differ only in the files above.
