# Unverified

What phase 1 has not proven. Since decision 37 every page's code is compiled, tested and verified (`verify.sh`, conformance on PostgreSQL) in the three reference solutions in `_temp/variants/`; rows that were only about compiling and running that code were closed then. What remains: anything no variant exercises, and whether a model generates the variants' code from the skills alone (phase 2). One row per open item.

| Step | Item | What awaits proof |
|---|---|---|
| 01 | `skills/mortis-architect/references/architecture/` (8 files) | A fresh model places things correctly (reader test, below); the pseudo-code agrees with the C# pages once they exist |
| 01 | `tests/check_architecture.py`, `tests/fixtures/` | Runs against the lean variant's Shared types while `_temp/variants` exists |
| 02 | `references/options/*.md`, `presets.md`, `interview.md`, `SKILL.md` | The interactive interview (questions asked in rounds) has never run; only the unattended path was exercised |
| 02 | `scripts/*.py`, `references/schema.json` | Unit-tested; the scaffold has built the three presets and the option builds of phase 2 (MongoDB, `int` keys, as-code, snake_case JSON, prefixed events, text tenant ids, `separate` with `background`); other accepted combinations are not built |
| 03 | `project-docs*.md`, generated `AGENTS.md`, `docs/architecture.md`, the ADRs | Read by models in the develop runs and the comparison run of phase 2 step 06 (where `AGENTS.md` alone carried most rules); never measured on their own |
| 03 | `scripts/conformance/*.sh` | Pass on the three variants; no deliberate break has been shown to turn each check red |
| 08 | Two workers on one table; a crash mid-transaction | No check; safe by construction (`skip locked`, one transaction per row), unproven |
| 08 | The outbox's raw SQL with `naming.database` = `as-code` | Written for `snake_case` only; the as-code build (`_temp/variants-extra/lean-b`) has no outbox |
| 09 | Enums or nested types in a message | Checks 1 to 6 pass against MassTransit 8.5.12 with both serializers, C# and Go; the example events have no enum or nested type |
| 09 | `events.transport` = `rabbitmq` with `hosts.worker` = `in-api` | No variant builds it; the Api would then host the receive endpoint |
| 11 | `Auth:Authority` (keys from an OpenID Connect authority's metadata) | Only the development signing key is exercised; no identity server runs in the checks |
| 11 | Tenancy with `events.transport` = `in-process` or `database` | The pages say where `EventPublisher` and `OutboxWorker` enter the tenant; no variant builds it |
| 11 | `tenancy.resolver` = `header` or `subdomain` | Page notes only; no build has them (`tenancy.key` = `text` is built: `_temp/variants-extra/advanced-t`) |
| 11 | `TenantScope.Bypass` used by a real job | Unit-tested; no example job crosses tenants |
| 12 | `languages.mode` = `messages` without `data`, and `ErrorCatalogue` with messages (catalogued without languages) | Page snippets only; no variant builds them |
| 12 | Translated validation field messages; searching by a translated field | Not built; the pages describe the JSON path and the per-language index |
| 15 | `permissions.md`: every snippet (`IAuthorizedCommand`, the permission constants, the pipeline check, `PermissionsOf`, permissions on the context and events, the Console's `--permissions`, the tests); ADR `0019`; `conformance/permissions.sh` | Written by hand; never compiled or run. No kernel-shape lines yet, since `auth` = `jwt` is built without them |
| 18 | `storage.md`, `contexts.md`: every snippet | Written by hand; never compiled. The other six advanced add-on pages are built in the advanced reference solution (decision 52); `storage` is in no preset and `contexts` was deferred (decision 53) |
| 18 | `conformance/storage.sh`, `contexts.sh`; ADRs `0027`, `0030`; their `shape-advanced.txt` lines | Never run |
| 17 | `mortis-develop`: the recipes not used yet (`add-value-object`, `add-read-model`, `add-translated-field`, `change-a-key-type`, `make-aggregate-tenant-owned` on an existing aggregate); its evals | Used by models in four develop runs (aggregate with children, command and consumer on standard, advanced and Go; the outbox turned on in lean); the evals are written, not run |
| 17 | `mortis-review`: the judgement checklist | Applied by models after the four develop runs; never against a change with planted judgement problems; the evals are written, not run |
| 17 | `review.sh` / `review.py` (14 checks) | Run on the variants, 14 fixtures and four models' changes (no false finding); heuristic checks (C03, C09, C10) may miss shapes the fixtures do not plant; reads C# only |
| 37 | Values in no preset: `long` default keys, `hosts.worker` = `separate` with `after-command`, `in-api` with `background` (`separate` with `background` on the standard preset is built: `_temp/variants-extra/standard-w`). (`int` keys, `as-code`, `snake_case` JSON and `prefixed` events are built in `_temp/variants-extra/lean-b`, phase 2 step 04) | Page snippets only |
| 2.04 | MongoDB with tenancy (`MongoRepository` tenant filter, tenant-first unique indexes), with `events.transport` = `rabbitmq`, and `LocalizedText` class map | Hand-written on `infrastructure-mongo.md`; run A was reduced to the standard preset on MongoDB without tenancy (decision 56). Tenancy with `tenancy.key` = `text` is built on PostgreSQL (`_temp/variants-extra/advanced-t`, 29/29) |
| 2.04 | With `database` = `mongo`, `verify.sh` runs only `kernel.sh`, `database-mongo.sh`, `testing.sh` and the add-on scripts: `example-console.sh`, `example-http.sh`, `auth.sh`, `queries.sh`, `api-conventions.sh` and `outbox.sh` read PostgreSQL with SQL | Run A's HTTP behaviour was checked by hand; the checks need a database-neutral way to read state (through the Api, or `mongosh` equivalents) |
| 3.03 | `<queue>_skipped`: neither the C# nor the Go bus moves a message no consumer handles there, as the wire-format spec's topology says | Both log and acknowledge it |
| 3.03 | The Go RabbitMQ bus and the Go outbox worker as Go skill pages (a Go standard or advanced preset) | Built and proven only in `_temp/interop/go-worker` (decisions 57, 59) |
| 3.03 | A small-model run of the Go lean preset | Optional in the plan; not run |
| 2.07 | The live trigger measurement (`claude -p` with the skills installed) | The trigger tests are a proxy (a model chooses from the menu); `tests/triggers/README.md` |
