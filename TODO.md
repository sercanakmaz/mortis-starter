# TODO

The single list of what is left to do and what is not yet proven, ranked by what would hurt a user first. The plan's phases are finished (`docs/plans/PROGRESS.md`); this file is the backlog from here on. It replaces `docs/unverified.md` (merged 2026-11-25).

Sizes: **S** an hour or two, **M** a session, **L** several sessions. Decisions are in [docs/decisions.md](docs/decisions.md).

How to keep it:

- One list. A new task or a newly found gap gets a row here, nowhere else.
- When an item is done, delete its row, add a line to [CHANGELOG.md](CHANGELOG.md), and log the run in [docs/generation-log.md](docs/generation-log.md). When an item is dropped, delete its row and say why in `docs/decisions.md`. History lives in those files, not here.
- Keep the ids (T1, U1, ...) stable; a new row takes the next free number in its section.
- No dates or promises. The next release is whatever is done when it is cut.

## Remaining tasks

| # | Task | Size | Notes |
|---|---|---|---|
| T1 | Rename the repository folder to `mortis-starter` | S | The maintainer's choice (decision 58); nothing inside the repository names the old folder |
| T3 | Run the live trigger measurement | S | `claude -p` with the skills installed (skill-creator's `run_eval.py`). The trigger tests in `tests/triggers/` are a proxy: a model chooses from the menu, where a real session also decides whether to use a skill at all |
| T4 | Run the develop and review evals (`skills/mortis-develop/evals/`, `skills/mortis-review/evals/`) | M | Written, never run |

## Not proven: what a user meets

| # | Item | Evidence so far | How to prove it | Size |
|---|---|---|---|---|
| U1 | Every conformance check can fail | The checks pass on every build; none has been broken on purpose to show it turns red | Break one rule per check in a copy of a variant; expect exactly that check to fail | M |
| U2 | `verify.sh` on MongoDB | Runs only `kernel.sh`, `database-mongo.sh`, `testing.sh` and the add-on scripts; `example-console.sh`, `example-http.sh`, `auth.sh`, `queries.sh`, `api-conventions.sh` and `outbox.sh` read PostgreSQL with SQL. Run A's HTTP behaviour was checked by hand | Read state through the Api or `mongosh` in place of `psql`; run them on `reference/variants-extra/standard-a` | M |
| U3 | `permissions.md`, `conformance/permissions.sh`, ADR 0019 | Written by hand (decision 47); never compiled or run; no kernel-shape lines | Build it in a variant (decision 52), then a generation run | M |
| U4 | `storage.md`, `contexts.md`, `conformance/storage.sh`, `contexts.sh`, ADRs 0027 and 0030, their `shape-advanced.txt` lines | Written by hand; never compiled or run. `storage` is in no preset; `contexts` was deferred (decision 53) | As U3 | M each |
| U5 | The architect's interactive interview (questions in rounds) | Only the unattended path has run | A model run with a person answering, or a scripted answerer | S |
| U6 | Develop recipes no model has used: `add-value-object`, `add-read-model`, `add-translated-field`, `change-a-key-type`, `make-aggregate-tenant-owned` on an existing aggregate | The other recipes were used in four develop runs (C# standard and advanced, the outbox on lean, Go lean) | One develop run per recipe on a generated project, then `verify.sh` and `review.sh` | M |
| U7 | The review checklist and `review.sh` | The checklist was applied after the four develop runs, never to a change with planted judgement problems. `review.sh` (14 checks) passed the variants, 14 fixtures and four models' changes; its heuristic checks (C03, C09, C10) may miss shapes the fixtures do not plant; it reads C# only | Plant problems the script cannot see and check the model's review finds them; a Go reading of the checklist | M |
| U8 | The architecture layer (`skills/mortis-architect/references/architecture/`, 8 files) | Read by every generation run; no reader test has checked that a fresh model places things correctly from it alone | The reader test in the phase 1 plan (step 01) | S |
| U9 | The generated project documents (`AGENTS.md`, `docs/architecture.md`, the ADRs) | Read by models in the develop runs; in the comparison run without the skills `AGENTS.md` carried most rules; never measured on their own | A develop run that may read only the project documents, scored against the skills' run | M |

## Not proven: options and combinations no build has

The architect's rules (`scripts/*.py`, `references/schema.json`) accept these; the scaffold has built the three presets and the option builds of phase 2 (MongoDB, `int` keys, as-code names, snake_case JSON, prefixed events, text tenant ids, `separate` with `background`), but not these.

| # | Item | Evidence so far | Size |
|---|---|---|---|
| O1 | Tenancy on MongoDB (tenant filter in `MongoRepository`, tenant-first unique indexes) | Hand-written on `infrastructure-mongo.md`; run A was the standard preset without tenancy (decision 56) | M |
| O2 | MongoDB with `events.transport` = `rabbitmq`; `LocalizedText` on MongoDB | Hand-written notes on `infrastructure-mongo.md` | M each |
| O3 | Tenancy with `events.transport` = `in-process` or `database` | The pages say where `EventPublisher` and `OutboxWorker` enter the tenant | M |
| O4 | `tenancy.resolver` = `header` or `subdomain` | Page notes only (`tenancy.key` = `text` is built: `reference/variants-extra/advanced-t`) | S each |
| O5 | `keys.default` = `long` | Page snippets only (`int` is built: `reference/variants-extra/lean-b`) | S |
| O6 | `hosts.worker` = `separate` with `after-command`; `in-api` with `background`; `events.transport` = `rabbitmq` with `in-api` (the Api would host the receive endpoint) | Page snippets only (`separate` with `background` is built: `reference/variants-extra/standard-w`) | S each |
| O7 | The outbox's raw SQL with `naming.database` = `as-code` | Written for `snake_case` only; the as-code build has no outbox | S |
| O8 | `languages.mode` = `messages` without `data`; `ErrorCatalogue` with messages; translated validation messages; searching by a translated field | Page snippets and notes; the pages describe the JSON path and the per-language index | M |
| O9 | Any other combination the rules accept | Only the combinations above and the builds named in this section's lead are built | M per combination |

## Not proven: robustness

| # | Item | Evidence so far | Size |
|---|---|---|---|
| R1 | Two outbox workers on one table; a crash mid-transaction | Safe by construction (`skip locked`, one transaction per row); no check | M |
| R2 | `Auth:Authority` against a real OpenID Connect identity server | Only the development signing key is exercised | M |
| R3 | `TenantScope.Bypass` used by a real job that crosses tenants | Unit-tested; no example job crosses tenants | S |
| R4 | Enums or nested types in a message on the bus | Wire-format checks 1 to 6 pass against MassTransit 8.5.12 with both serializers, C# and Go; the example events have neither | S |
| R5 | `<queue>_skipped` | Neither the C# nor the Go bus moves a message no consumer handles there, as the wire-format spec's topology says; both log and acknowledge it. Implement it or change the spec | S |
| R6 | The generated pages drift from their tools | Rerunning `expand_pages.py` and `build_project_docs.py` moves the namespace rows of `ConsoleArgumentsTests` and `ConsoleCommandRunnerTests` from `testing-kernel.md` to `hosts.md`, because `build_manifest.py` files every `Console/` test under `hosts.md` while `ConsoleCommandRunnerTests`' code is on `testing-kernel.md`. `check_docs.py` passes either way, so it does not check that the committed pages equal a fresh run. Fix the page rule, then add that check | S |

## Go

| # | Item | Evidence so far | Size |
|---|---|---|---|
| G1 | A Go standard preset: the outbox and the RabbitMQ bus as Go skill pages | Both built and proven only in `reference/interop/go-worker` (decisions 57 and 59) | L |
| G2 | A small-model generation run of the Go lean preset | Optional in the plan; not run | S |
