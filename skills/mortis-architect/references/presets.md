# Presets

A preset is a complete set of answers. The interview picks one from the description, asks only about what the description leaves doubtful, and writes the rows that differ. `scripts/new_decisions.py` writes any preset as a valid `MORTIS.md`.

## Who each is for

| Preset | For | What it leaves out |
|---|---|---|
| `lean` | a prototype, a weekend project, one developer, a small service nobody else consumes | the outbox, a worker, authentication, translated messages, integration tests |
| `standard` | a real product with one team and an API for a front end | a broker, tenants, translated messages, read models, a separate worker, architecture tests |
| `advanced` | one service of a platform: a broker, tenants, a separate worker, many developers | nothing on the list; the most to run and to learn |

## One repository, one service

A `MORTIS.md` describes one service in one repository. A platform of several services is several repositories, each with its own interview and its own `MORTIS.md`. Repeat the shared choices (`events.transport`, `tenancy.*`, `naming.events`) in each, so the services speak the same wire format.

## The decision tables

Keys that are always asked (`name`, `language`, `database`) and keys that only apply with another (`tenancy.key`, `tenancy.resolver`, `languages.default`, `languages.supported`) are not in the table.

| Key | Lean | Standard | Advanced |
|---|---|---|---|
| `events.outbox` | none | after-command | background |
| `events.transport` | in-process | database | rabbitmq |
| `keys.default` | guid | guid | guid |
| `naming.database` | snake_case | snake_case | snake_case |
| `naming.json` | camelCase | camelCase | camelCase |
| `naming.routes` | kebab-case | kebab-case | kebab-case |
| `naming.events` | bare | bare | bare |
| `exceptions` | minimal | coded | catalogued |
| `queries` | commands | split | read-models |
| `tenancy.mode` | none | none | interface |
| `languages.mode` | none | none | data |
| `testing` | unit | integration | architecture |
| `auth` | none | jwt | jwt |
| `hosts.worker` | none | in-api | separate |
| `addons` | none | correlation, api-conventions | correlation, soft-delete, caching, scheduler, secrets, loop-guard, contexts, events-project, idempotent-commands, observability, api-conventions |
| `example` | true | true | false |

When `tenancy.mode` is `interface` the defaults are `tenancy.key` = `guid` and `tenancy.resolver` = `claim`. When `languages.mode` is not `none` the defaults are `languages.default` = `en` and `languages.supported` = `en`.

## Moving off a preset

Every row you change from the preset's value gets a reason in the `Why` column and a decision record in `docs/adr/` (see `adr-template.md`). The rules between options (`references/schema.json`, key `rules`) decide which combinations are refused; the usual moves:

| You want | Change | The rules then require |
|---|---|---|
| lean, but events must not be lost | `events.outbox` to `after-command`, `events.transport` to `database` | `hosts.worker` other than `none` (R1, R3) |
| standard, plus a broker | `events.transport` to `rabbitmq`, `hosts.worker` to `separate` | an outbox (R2, R4) |
| standard, plus tenants | `tenancy.mode` to `interface` | nothing; the tenant is added to outbox rows (R7) |
| standard, plus translated messages | `languages.mode` to `messages` | `exceptions` other than `minimal` (R5) |
| any preset on MongoDB | `database` to `mongo` | `keys.default` of `guid` (R12); a replica set when an outbox is used (R8) |
