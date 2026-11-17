# Phase 1, step 03 — The scaffold skill's foundations (knowledge only)

Needs: phase 1 step 02. Size: large. No C# is written, built or generated in this step.

## Goal

The C# solution and its copy script have already been moved out of the skill to `_temp/template/` (a temporary folder). After this step the skill holds the generation workflow, the C# pages for the lean preset, and the checks, and still no project source. The solution in `_temp/` is only source material.

## Read first

`docs/specs/generation-and-verification.md` (binding), the current `skills/mortis-scaffold/` and `_temp/template/` in full, `tests/smoke.sh`, `tests/check_docs.py`, the architecture layer from step 01.

## Design

**Read, do not change.** The solution is at `_temp/template/assets/csharp/`, with `csharp-example.txt` and the old `scaffold.sh` beside it under `_temp/template/`. It is not edited in phase 1; it is the working code the kernel pages are written from. Phase 2 saves generated projects under `examples/` and then deletes `_temp/`.

**Layer 2 for C#** in `skills/mortis-scaffold/references/csharp/`, written from that solution under the snippet rules of the spec:

| File | Covers |
|---|---|
| `naming.md` | C#'s own rules, the name of every role in the layer 1 glossary, what each `naming.*` choice changes |
| `manifest.json` | every file of the lean preset: path with placeholders, the type it holds, the page that describes it |
| `kernel-shape.txt` | every public type and member signature of the kernel |
| `solution.md` | projects, references, folder conventions, the build-guard files in full, the package list with pinned versions |
| `shared-domain.md`, `shared-application.md`, `shared-other.md` | the kernel types, one section each, with a snippet per non-trivial member |
| `domain.md`, `application.md` | how an aggregate, child entity, value object, event, service, command file, DTO and event handler are written |
| `infrastructure-postgres.md`, `events-inprocess.md` | persistence and the commit order; the in-process dispatchers |
| `hosts.md` | Api and Console |
| `testing.md` | test project layout, `TestSupport`, the kernel tests with their snippets |
| `worked-slice.md` | one aggregate from its test to its controller and scenario, file by file as member snippets |
| `project-docs.md` | the `AGENTS.md`, `CLAUDE.md`, `MORTIS.md`, `docs/architecture.md` and ADRs a generated project gets |

The existing `references/*.md` are the starting material.

**The example domain** (orders, customers, products, the Console scenarios) is specified once, language-neutrally, in `mortis-architect/references/architecture/example-domain.md`.

**`SKILL.md`** is rewritten around the generation workflow of the spec, including the numbered procedure and the progress file.

**Checks shipped in the skill** (written now, first run in phase 2):
- `scripts/lang/csharp.sh`: everything language-specific (create projects, build, test, format, extract the public shape).
- `scripts/verify.sh <project-dir>`: language-neutral; reads `MORTIS.md`; structure against the manifest (missing and extra files), kernel shape, naming, test names, house rules, then the conformance checks for the options that are on. Prints `N passed, M failed, K skipped` and names every skip.
- `scripts/conformance/`: today's database and Console checks from `tests/smoke.sh`, rewritten to take a project directory and to assert behaviour only. No check reads source text.

**Checks in the repository:** `tests/check_installable.sh` (no project source under `skills/`, snippet rules, no origins outside `_sources/`), `tests/check_docs.py` (pages against the manifest and the shape file; one-way wording), `docs/unverified.md` (the ledger).

## Deliverables

Everything above; `docs/decisions.md` decision 4 replaced; the root `README.md` and `scripts/install.sh` updated; `CHANGELOG.md`; `docs/unverified.md` started with every page and check of this step.

## Acceptance (this phase)

- [ ] `tests/check_installable.sh` passes, and fails when a `.cs` file is put under `skills/`, when a snippet contains a namespace with a complete type, and when a studied codebase is named outside `_sources/`.
- [ ] Every source file of `_temp/template/assets/csharp/` appears in `manifest.json`, and every public kernel member appears in `kernel-shape.txt` (compared by `tests/check_docs.py` by reading the source as text; nothing is built).
- [ ] Every non-trivial kernel member has a snippet (the checker counts members with a body of more than one statement against snippets).
- [ ] `verify.sh` and the conformance scripts pass a shell syntax check and print their plan for a `MORTIS.md` with `--dry-run`, naming each check they would run.
- [ ] `tests/smoke.sh` is left as it is (it builds and tests the solution in `_temp/` and is deleted with it). Its checks are listed in the step notes with their new place in `scripts/conformance/`; none is dropped.

## Behaviour to prove in phase 2

- [ ] A fresh model with only the installed skills generates the lean preset and it passes `verify.sh`.
- [ ] The same without the example, and the adoption recipe on an existing solution.
- [ ] The conformance suite has teeth: three deliberate breaks each turn a check red.

## Out of scope

Any new option; Go; running anything.

## Traps

- Decision 28 overrides the template: a command handler loads and saves its aggregates and may change several when the action is about all of them; there is no `*Service` per aggregate by reflex. Do not copy the template's "one service call" shape.

- A reference page may not be a source file with prose around it. Give the contract, the rules, and a snippet for each member that has a real body.
- Do not let `verify.sh` depend on the example's names. It reads `MORTIS.md` and the manifest.
- Pin package versions and the SDK; "latest" makes results differ from day to day.
- It will be tempting to build "just to check". Don't; note the doubt in `docs/unverified.md`. Phase 2 is where it is settled.

## Notes (done)

- Pages in `skills/mortis-scaffold/references/csharp/`: `naming.md`, `manifest.json` (207 entries), `kernel-shape.txt` (191 lines from 59 kernel files), `solution.md`, `shared-domain.md`, `shared-application.md`, `shared-other.md`, `domain.md`, `application.md`, `infrastructure-postgres.md`, `events-inprocess.md`, `hosts.md`, `testing.md`, `testing-kernel.md`, `worked-slice.md`, `worked-slice-tests.md`, `project-docs.md`, `project-docs-architecture.md`, `project-docs-adr.md`, `adopting.md`. The example domain in roles: `mortis-architect/references/architecture/example-domain.md`.
- Changed from the plan: `testing.md` split into `testing.md` (layout, rules, kernel test support) and `testing-kernel.md`; the worked slice split into `worked-slice.md` and `worked-slice-tests.md`; project docs split in three. Each kept under about 1,200 lines so one step has one page in view.
- Snippets for kernel members are extracted from the template's compiled source by `_temp/tools/expand_pages.py` (temporary, deleted with `_temp/`). Example code that decision 28 changes (handlers, handler tests, event handler tests, repository interfaces, AGENTS.md rules 8 to 13, architecture.md, ADR 0006) is written by hand.
- Decision 28 applied: no `{Aggregate}Service`, no aggregate modules, no service builders or service tests; handlers load, call, stamp, save; uniqueness is the unique index (the template's `Is...ExistsAsync` pre-checks are gone). Seven handler test classes replace the three service test classes.
- Scripts: `verify.sh` (`--dry-run` lists 66 checks for the lean preset with the example), `structure.py` (manifest, missing and extra files), `decisions.py` and `references/schema.json` (exact copies of the architect skill's, checked by `check_docs.py`), `lang/csharp.sh`, `lang/csharp_shape.py`, `lang/csharp_rules.py`, `conformance/{lib,kernel,example-database,example-console,example-http}.sh`.
- Repository checks: `tests/check_installable.sh` (fails on a `.cs` under `skills/`, on a snippet with a namespace and a complete type, on a named origin; all three shown failing); `tests/check_docs.py` rewritten (manifest covers the template, kernel shape equals the template's, every kernel member with several statements has a snippet, one-way wording, the copies, the schema tables).
- Where each `tests/smoke.sh` check went: docs check → `tests/check_docs.py`; core and example build and tests → `verify.sh` build and tests; "no example code left" and "no migration left" → `structure.py` (extra files); "no placeholder name left" → not needed (nothing is copied); banned API → `conformance/kernel.sh`; migrations and schema (5) → `conformance/example-database.sh`; order-flow, stock-flow, Console runner, blocked-customer (27) → `conformance/example-console.sh`; the two VS Code checks → `lang/csharp.sh editor-checks`, run by `verify.sh`; HTTP (21) → `conformance/example-http.sh`; kernel-only mode → the adoption recipe (`adopting.md`), proven in phase 2. None dropped.
- Found, not done: the conformance log contract ("Published <wire name>", "Async handler ... failed") is C#'s log text; step 16 should make it a stated contract for every language. The structure check flags a user's own aggregate whose files have the same paths as the example's when `example` is `false`; rare, fix when it bites.
