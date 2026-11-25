# Progress of phases 2 and 3

Worked unattended from 2026-11-22 (the maintainer asked for every remaining phase and step and checks the final result). Decision 52 records how the plans' "with the maintainer" sessions are run without one.

## How the work is done (decision 52)

1. **Build, then generate.** A preset value that phase 1 left as knowledge only is first built by hand in the matching reference solution under `_temp/variants/`, compiled, tested and verified with its conformance checks; the page is corrected to what compiled (`check_docs.py` keeps pages and variants equal). Only then is a fresh model asked to generate the preset from the installed skills, which tests reproducibility from snippets, not whether the snippets compile.
2. **Fixes go to the skills**, never to a generated project; each is logged in `docs/generation-log.md`.
3. **The maintainer's approvals** (a proposed page change, "stop and tell the maintainer") are replaced by a line in this file and in the step's plan notes.
4. Each step is committed when the next one starts.

## Status

| Phase.step | State | Notes |
|---|---|---|
| 2.01 lean | done | generated with the example: `verify.sh` 14/14; 19 defects fixed in the skills; six judge breaks caught; saved in `examples/csharp/lean/` |
| 2.02 standard | done | built `split`, `integration`, `api-conventions` in the variant (18/18); generated: `verify.sh` 18/18 after one structure miss; 8 defects fixed in the skills; saved in `examples/csharp/standard/` |
| 2.03 advanced | done | the whole preset built in the reference solution (verify 29/29); a fresh mid-size model generated it at 28/29, the one failure being the skills' (the wire name with `events-project`), now fixed; 30 further notes in the run report for a tightening pass |
| 2.04 variants | done | run A: standard on MongoDB, reference `_temp/variants-extra/standard-a` 12/12, fresh-model run 12/12 on the first try; run B: lean with `int` keys, `as-code`, `snake_case` JSON, `prefixed` events, reference `lean-b` 14/14, fresh-model run 14/14 on the first try; tenancy with text ids on PostgreSQL, reference `advanced-t` 29/29. Not built: tenancy on MongoDB (decision 56, `unverified.md`) |
| 2.05 sameness and small models | done (reduced) | three lean runs: two small-model (Haiku class) runs passed `verify.sh` 14/14 on their first run; one mid-size run failed only the race check, which was the skills' fault (`Retry(3)`, now `Retry(10)`); defects logged. Not run: the small-model standard run |
| 2.06 develop and review | done | runs 1 (standard), 2 (advanced, tenant-owned) and 3 (outbox on lean) with `mortis-develop`: all build, test and pass `review.sh`; every `verify.sh` failure was the skills' and is fixed; the comparison run without the skills is logged; the review fixtures pass |
| 2.07 package | done | `check_installable.sh` passes; clean-machine stand-in: a fresh home folder, `claude plugin marketplace add` and `claude plugin install mortis-starter@mortis` succeed, and the installed copy's `verify.sh` passes the standard example 18/18 (the machine already had .NET and Docker; fresh-model generation from the skills alone was done in 2.01 to 2.05); trigger tests `tests/triggers/` 100 % triggered, 1 false in 360, on haiku, sonnet and opus (a proxy, see its README). Licence MIT; `_temp/` kept, `_sources/` deleted, repository renamed `mortis-starter` by the maintainer (decision 58) |

Phase 3 (Go), started unattended (decision 54):

| Step | State | Notes |
|---|---|---|
| 3.01 Go knowledge (lean) | done | `_temp/variants-go/lean` passes `verify.sh` 14/14 through `scripts/lang/go.sh` (structure, house rules `go_rules.py`, kernel shape `go_shape.py`, editor files, build with the banned-id check, tests, gofmt, kernel 4, example-database 7, example-console 30, example-http 27); `references/go/` (11 pages, manifest, kernel shape) built from it by `_temp/tools-go/build_go.py`; the schema accepts Go for the lean values only |
| 3.02 Go generation | done | a mid-size model generated the Go lean project from the skills alone; `verify.sh` 14/14 on its first run; saved in `examples/go/lean`; 9 small defects, the real ones fixed |
| 3.03 interop | partly done | the shared outbox is proven: a C# Api that only writes rows (`standard-w`, 18/18) and a Go worker on one database, `tests/check_interop_outbox.sh` 10/10 (decision 57); `mortis-develop` and `mortis-review` exercised on the Go project. Not done: the bus interop (needs Go's RabbitMQ transport) and the optional small-model Go run |
| 3.04 release | waits for the maintainer | needs the licence choice, the decision to delete `_sources/`, and a commit route to the repository (the Mac connection reads only) |
