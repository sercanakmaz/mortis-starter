# TODO

What is left to do and what is not yet proven, ranked by what would hurt a user first. The plan's phases are finished (`plans/PROGRESS.md`); this file is the backlog from v0.2.0 on. Sizes: **S** an hour or two, **M** a session, **L** several sessions. The evidence behind each "not proven" line is in [docs/unverified.md](docs/unverified.md); decisions are in [docs/decisions.md](docs/decisions.md).

When an item is done, delete it here, delete its row in `docs/unverified.md`, and log the run in `docs/generation-log.md`.

## Remaining tasks

| # | Task | Size | Notes |
|---|---|---|---|
| T1 | Rename the repository folder to `mortis-starter` | S | The maintainer's choice (decision 58); nothing inside the repository names the old folder |
| T2 | Add a git remote and push `main` with its tags | S | `git push -u origin main --tags`; the CI workflow (`.github/workflows/checks.yml`) runs on the first push |
| T3 | Run the live trigger measurement | S | `claude -p` with the skills installed (skill-creator's `run_eval.py`); the trigger tests in `tests/triggers/` are a proxy |
| T4 | Run the develop and review evals (`skills/mortis-develop/evals/`, `skills/mortis-review/evals/`) | M | Written, never run |

## Not proven: correctness a user meets

| # | Item | Size | How to prove it |
|---|---|---|---|
| U1 | Every conformance check can fail: no check has been broken on purpose to show it turns red | M | Break one rule per check in a copy of each variant, expect exactly that check to fail |
| U2 | On MongoDB, `verify.sh` runs no HTTP, auth, query, api-conventions or outbox check (they read PostgreSQL with SQL) | M | Read state through the Api or `mongosh` in place of `psql`, then run them on `_temp/variants-extra/standard-a` |
| U3 | `permissions.md` (with `conformance/permissions.sh` and ADR 0019) has never been compiled or run | M | Build it in a variant (decision 52), then a generation run |
| U4 | `storage.md`, `contexts.md`, their checks and ADRs 0027, 0030 have never been compiled or run | M each | As U3 |
| U5 | The architect's interactive interview (questions in rounds) has never run; only the unattended path has | S | A model run with a person answering, or a scripted answerer |
| U6 | Develop recipes no model has used: `add-value-object`, `add-read-model`, `add-translated-field`, `change-a-key-type`, `make-aggregate-tenant-owned` on an existing aggregate | M | One develop run per recipe on a generated project, then `verify.sh` and `review.sh` |
| U7 | The review checklist has never met a change with planted judgement problems; `review.sh` reads C# only | M | Plant problems the scripts cannot see, check the model's review finds them; a Go reading of the checklist |

## Not proven: options and combinations no build has

| # | Item | Size |
|---|---|---|
| O1 | Tenancy on MongoDB (tenant filter in `MongoRepository`, tenant-first unique indexes) | M |
| O2 | MongoDB with `events.transport` = `rabbitmq`; `LocalizedText` on MongoDB | M each |
| O3 | Tenancy with `events.transport` = `in-process` or `database` | M |
| O4 | `tenancy.resolver` = `header` or `subdomain` | S each |
| O5 | `keys.default` = `long` | S |
| O6 | `hosts.worker` = `separate` with `after-command`; `in-api` with `background`; `events.transport` = `rabbitmq` with `in-api` | S each |
| O7 | The outbox's raw SQL with `naming.database` = `as-code` | S |
| O8 | `languages.mode` = `messages` without `data`; `ErrorCatalogue` with messages; translated validation messages; searching by a translated field | M |

## Not proven: robustness

| # | Item | Size |
|---|---|---|
| R1 | Two outbox workers on one table, and a crash mid-transaction (safe by construction: `skip locked`, one transaction per row) | M |
| R2 | `Auth:Authority` against a real OpenID Connect identity server (only the development signing key is exercised) | M |
| R3 | `TenantScope.Bypass` used by a real job that crosses tenants | S |
| R4 | Enums or nested types in a message on the bus (C#, Go, MassTransit) | S |
| R5 | `<queue>_skipped`: neither bus moves an unhandled message there, as the wire-format spec's topology says; both log and acknowledge it. Implement it or change the spec | S |

## Go

| # | Item | Size |
|---|---|---|
| G1 | A Go standard preset: the outbox and the RabbitMQ bus as Go skill pages (today they live in `_temp/interop/go-worker`, decisions 57 and 59) | L |
| G2 | A small-model generation run of the Go lean preset (optional in the plan) | S |
