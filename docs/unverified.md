# Unverified

Everything written in phase 1 that has never been compiled or generated. Phase 2 proves each line or fixes it. One row per page or check, added by the step that wrote it.

| Step | Item | What awaits proof |
|---|---|---|
| 01 | `skills/mortis-architect/references/architecture/` (8 files) | A fresh model places things correctly (reader test, below); the pseudo-code agrees with the C# pages once they exist |
| 01 | `tests/check_architecture.py`, `tests/fixtures/` | Runs against the template's Shared types only while `_temp/template` exists |
| 02 | `references/options/*.md`, `presets.md`, `interview.md`, `SKILL.md` | The interactive interview (questions asked in rounds) has never run; only the unattended path was exercised |
| 02 | `scripts/*.py`, `references/schema.json` | Unit-tested and run on four prompts; the rules say what combinations are refused, but whether the *scaffold* can build every accepted combination is proven only in phase 2 |
| 03 | `skills/mortis-scaffold/references/csharp/*.md` (17 pages) | Kernel snippets are extracted from the compiled template, so they are known to compile in that context; never yet assembled into files by a model. Example handlers, handler tests and event handler tests under decision 28 were written by hand and never compiled |
| 03 | `manifest.json`, `kernel-shape.txt` | Generated from the template and decision 28; checked against the template's source text only |
| 03 | `scripts/verify.sh`, `structure.py`, `lang/csharp.sh`, `lang/csharp_shape.py`, `lang/csharp_rules.py` | Ran in full once, in the pilot (`docs/pilot.md`); not yet on a small model or another preset |
| 03 | `scripts/conformance/*.sh` | Passed once against the pilot's generated project; no deliberate break has been shown to turn a check red yet |
| 03 | Uniqueness by unique index only (no handler pre-check) | Conformance expects 409 `duplicate`; proven only when the example runs |
| 03 | `project-docs*.md` | The generated `AGENTS.md`, `docs/architecture.md` and ADR 0006 were edited for decision 28 and never read back by a model in a project |
| 03 | Handler and event-handler test bodies (names only in `worked-slice-tests.md`) | Invented by the pilot model; may vary between runs and fail on a small model |
| 04 | `CommandContext`, `IContextCommand`, the pipeline's context stack, `EventContext` stamping, `ContextOf` in the handler bases, `ContextCommandFilter`, the console context, the log line with correlation and causation (overlay sources in `_temp/tools/overlay/`, pages regenerated) | Written by hand, never compiled; the pilot project predates them |
| 04 | Kernel tests `ContextCommandTests`, `CommandContextTests`, the new `EventContextTests`, `CommandPipelineTests`, `ContextCommandFilterTests`, `CommandScenarioTests` | Never compiled or run |
| 04 | `csharp_rules.py`: one context line per command | Shown to flag the old shape in the pilot project; never run on a project with the new shape |
| 04 | Conformance: correlation and causation read from the `stock-flow` log | The log parser was run on a sample log only |
| 04 | Generated `AGENTS.md` rule 20, `docs/architecture.md` "Who did it", ADR 0007 | Never read back in a generated project |
| 05 | `IdentityAggregateConfiguration`, the example's `Category` (domain, application, infrastructure, controller, tests), `Product.CategoryId`, ADR 0008 | Written by hand, never compiled; whether EF translates `aggregate.Id.Equals(id)` in `FindByIdAsync` for an `int` key is unproven |
| 05 | Conformance: categories table identity column, the product foreign key, `POST /categories`, `GET /categories/{id}`, a product with an unknown category | Never run |
| 06 | The `correlation` add-on: `CorrelationIdMiddleware` with a logging scope, its tests, ADR 0009, `correlation.md`; core: correlation and causation on the command log lines, the async failure log line, the Console printing its run's id | Written by hand, never compiled |
| 06 | `conformance/addon-correlation.sh` (header returned, malformed header, three log checks with the example) | Never run |
| 06 | `structure.py` treats a `when` on a list key (`addons`) as "contains" | Never run on a project with an add-on |

## Reader test (step 01)

Fresh Haiku-class model, given only the eight architecture files and the twenty questions without their answers.

| Run | Result | Note |
|---|---|---|
| 1 | 17/20 | Three questions were ambiguous or had no matching block; the questions were reworded, not the files |
| 2 | 19/20 | The miss was a block-name choice (`Command` instead of `Host adapter`) for a host loop |
| 3 | 19/20 | After the cheat sheet and the cardinality changes (same questions); the same host-loop question missed |
| 4 | 20/20 | After decision 28 (use cases may change several aggregates); one question replaced by a transfer question |

The questions were revised after run 1, so run 2 is a re-test, not an independent first pass. The first run of a different model on the same questions would be the honest confirmation.
