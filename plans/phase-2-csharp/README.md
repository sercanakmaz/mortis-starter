# Phase 2 — Generate C# with the maintainer

The skills are complete on paper. Now they are used for real: a fresh model with only the installed skills generates C# projects, and the skills' own `verify.sh` judges them. This is the first time anything written in phase 1 is compiled or run.

## How a session works

The maintainer is present for every run.

1. The maintainer picks the step and starts the run (an empty folder, only the installed skills, the step's prompt).
2. `verify.sh` runs. The coding model reports what passed, what failed and what was skipped.
3. For each failure, the cause is found and **fixed in a skill page or check**, never in the generated project. The model shows the maintainer the proposed change to the skill before making it.
4. The run is repeated from an empty folder, or, when only a late part failed, resumed from the progress file. The maintainer decides which.
5. When the run is green, the generated project is saved under `examples/csharp/<preset>/` as proof, and the lines it proved are removed from `docs/unverified.md`.

## Keeping the cost down

- **One run per attempt.** No repeated runs for statistics until step 05.
- **Presets, not options.** Each run exercises many options at once; there is no run per option.
- **Resume, don't restart**, when the failure is in a late layer and the early layers were already green.
- **Fix in batches.** Collect all failures of a run, fix the pages, then run once.
- Each run records its time and tokens in `docs/generation-log.md`, so the cost is visible and the maintainer can stop when it stops paying.

## Steps

| # | File | Generates | Proves |
|---|---|---|---|
| 01 | [01-lean.md](01-lean.md) | the lean preset, with and without the example | the foundations: kernel, layers, in-process events, hosts, tests |
| 02 | [02-standard.md](02-standard.md) | the standard preset | context, correlation, coded exceptions, outbox after the command, database transport, inbox, query split, authentication, translated messages, integration tests |
| 03 | [03-advanced.md](03-advanced.md) | the advanced preset | background outbox, the RabbitMQ bus and MassTransit interoperability, tenancy, translated data, read models, catalogued exceptions, architecture tests, observability |
| 04 | [04-variants.md](04-variants.md) | the choices no preset covers | MongoDB, integer keys, text tenant ids, non-default naming, minimal exceptions |
| 05 | [05-sameness-and-small-models.md](05-sameness-and-small-models.md) | repeated runs | how alike runs are; whether small models succeed |
| 06 | [06-develop-and-review.md](06-develop-and-review.md) | changes to a generated project | `mortis-develop` and `mortis-review` |
| 07 | [07-package.md](07-package.md) | nothing new | the plugin, the README, continuous integration |

## Exit

`docs/unverified.md` has no C# line left, the three presets and the variants are saved under `examples/csharp/`, and the plugin installs on a clean machine.
