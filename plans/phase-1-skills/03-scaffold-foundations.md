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

- A reference page may not be a source file with prose around it. Give the contract, the rules, and a snippet for each member that has a real body.
- Do not let `verify.sh` depend on the example's names. It reads `MORTIS.md` and the manifest.
- Pin package versions and the SDK; "latest" makes results differ from day to day.
- It will be tempting to build "just to check". Don't; note the doubt in `docs/unverified.md`. Phase 2 is where it is settled.
