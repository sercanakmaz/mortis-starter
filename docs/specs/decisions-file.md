# The decisions file: MORTIS.md

One file per repository, and one repository per service (decision 30). Written by `mortis-architect`, read by `mortis-scaffold`, `mortis-develop`, `mortis-review` and `verify.sh`. It sits in the root of the generated project and is committed with it. Finalised in [phase 1, step 02](../../plans/phase-1-skills/02-decision-model.md). The single source of keys, values, defaults and rules is `skills/mortis-architect/references/schema.json`; the tables below are checked against it by `tests/check_docs.py`.

## Why markdown

A generated project is full of markdown that assistants and people read (`AGENTS.md`, `CLAUDE.md`, `docs/`). A decisions file in the same form is read by both without a tool, can say *why* next to *what*, and can point at the skills to use for each kind of work. A JSON file would be invisible to an assistant until a script opened it.

It stays machine-readable: the choices live in **one table with a fixed shape**, and that table is the only part scripts read. There is one file, not a readable one and a machine one that could drift apart.

## Shape

```markdown
# Project decisions

This project was started with mortis-starter. The table below records what was chosen. Skills and checks read it; change a row here before changing the code it describes.

## Decisions

| Key | Value | Why |
|---|---|---|
| version | 1 | |
| name | Acme.Billing | |
| language | csharp | the team's language |
| preset | standard | |
| database | postgres | |
| events.outbox | after-command | events must not be lost; low latency matters |
| events.transport | database | one service, no broker to run yet |
| keys.default | guid | |
| naming.database | snake_case | |
| naming.json | camelCase | |
| naming.routes | kebab-case | |
| naming.events | bare | |
| exceptions | coded | |
| queries | commands | |
| tenancy.mode | none | |
| languages.mode | none | |
| testing | integration | |
| auth | none | |
| hosts.worker | in-api | |
| addons | correlation, api-conventions | |
| example | true | |

## Working in this project

| To do this | Use |
|---|---|
| Add an aggregate, command, query, event or handler | the `mortis-develop` skill |
| Check a change before merging | the `mortis-review` skill |
| Turn an option on or change one | edit the table above, then ask `mortis-develop` to apply it |
| Verify the whole project | `verify.sh` from the `mortis-scaffold` skill |

The rules of the code are in [AGENTS.md](AGENTS.md); how it works is in [docs/architecture.md](docs/architecture.md); why each non-default choice was made is in [docs/adr/](docs/adr/).
```

Parsing rules, so every script reads it the same way:

- Only the table under the heading `## Decisions` is read. Everything else is for readers.
- One row per key. Keys are dotted, values are plain text; a list is comma-separated. The `Why` column is free text and is ignored by scripts.
- A key that is absent takes the preset's value. An unknown key or value is an error that names the row.
- The validator and `verify.sh` share one small parser (`scripts/decisions.py`, Python standard library), shipped in the skill. `decisions.py FILE get KEY` prints a resolved value for shell use.
- A key that only applies with another (`tenancy.key`, `tenancy.resolver`, `languages.default`, `languages.supported`) is an error when its parent does not enable it, and takes its default when the parent does.

The generated `AGENTS.md` and `CLAUDE.md` link to `MORTIS.md` in their first lines, so every assistant session in the project sees the decisions and knows which skill to reach for.

## Values

The keys `version` (always `1`), `name` (letters, digits, dot, dash, underscore) and `preset` are also required in the file. `language`, `database` and `name` are always asked.

| Key | Values | Lean | Standard | Advanced |
|---|---|---|---|---|
| `language` | any language the skills have references for (`csharp` first, then `go`) | asked | asked | asked |
| `database` | `postgres`, `mongo` | asked | asked | asked |
| `events.outbox` | `none`, `after-command`, `background` | `none` | `after-command` | `background` |
| `events.transport` | `in-process`, `database`, `rabbitmq` | `in-process` | `database` | `rabbitmq` |
| `keys.default` | `guid`, `int`, `long` | `guid` | `guid` | `guid` |
| `naming.database` | `snake_case`, `as-code` | `snake_case` | `snake_case` | `snake_case` |
| `naming.json` | `camelCase`, `snake_case` | `camelCase` | `camelCase` | `camelCase` |
| `naming.routes` | `kebab-case`, `lowercase` | `kebab-case` | `kebab-case` | `kebab-case` |
| `naming.events` | `bare`, `prefixed` | `bare` | `bare` | `bare` |
| `exceptions` | `minimal`, `coded`, `catalogued` | `minimal` | `coded` | `catalogued` |
| `queries` | `commands`, `split`, `read-models` | `commands` | `split` | `read-models` |
| `tenancy.mode` | `none`, `interface` | `none` | `none` | `interface` |
| `tenancy.key` | `guid`, `text` | | | `guid` |
| `tenancy.resolver` | `header`, `claim`, `subdomain` | | | `claim` |
| `languages.default` | a language tag such as `en`; only with `languages.mode` other than `none` | | | |
| `languages.supported` | tags, comma separated; same condition | | | |
| `languages.mode` | `none`, `messages`, `data` | `none` | `none` | `data` |
| `testing` | `unit`, `handlers`, `integration`, `architecture` | `unit` | `integration` | `architecture` |
| `auth` | `none`, `jwt` | `none` | `jwt` | `jwt` |
| `hosts.worker` | `none`, `in-api`, `separate` | `none` | `in-api` | `separate` |
| `addons` | any of `correlation`, `soft-delete`, `caching`, `scheduler`, `storage`, `secrets`, `loop-guard`, `contexts`, `events-project`, `idempotent-commands`, `observability`, `api-conventions` | none | `correlation`, `api-conventions` | all but `storage` |
| `example` | `true`, `false` | `true` | `true` | `false` |

The three outbox models are exactly: `none` (events reach event consumers in the same process after the commit; lost if the process dies in between), `after-command` (saved with the change, published by the same request right after the commit, with a sweeper for leftovers), `background` (saved with the change, published by a relay job).

## Rules

The validator refuses a file that breaks one; the interview never offers a combination that would.

| # | Rule |
|---|---|
| R1 | `events.outbox` = `none` requires `events.transport` = `in-process`, and the reverse. |
| R2 | `events.transport` = `rabbitmq` requires an outbox. |
| R3 | Any outbox requires `hosts.worker` other than `none`. |
| R4 | `events.transport` = `rabbitmq` requires `hosts.worker` = `separate`. |
| R5 | `languages.mode` other than `none` requires `exceptions` other than `minimal`. |
| R6 | `languages.default` must be in `languages.supported`. |
| R7 | `tenancy.mode` = `interface` with any outbox adds the tenant to outbox and inbox rows (a note, never an error; the scaffold skill applies it). |
| R8 | `database` = `mongo` with any outbox requires a replica set; the generated compose file starts one (a note, never an error). |
| R9 | `addons` containing `scheduler` requires `hosts.worker` other than `none`. |
| R10 | `queries` = `read-models` requires `events.outbox` other than `none`, because the projection is fed asynchronously. |
| R11 | `language` must be available: the schema lists which languages have references. `go` is listed as not available until it has. |
| R12 | `database` = `mongo` does not support `keys.default` = `int` or `long`. |
| R13 | `tenancy.mode` = `interface` requires `auth` = `jwt`: the tenant is a claim of the signed-in caller. |
| R14 | `addons` containing `idempotent-commands` with `database` = `mongo` is not offered yet; the scaffold skips that add-on and says so (a note, never an error). |

## Interview order

1. What is being built and by how many people: picks the preset. One question.
2. Slots the preset cannot guess: language, database.
3. Only then the questions whose answer the user's description makes doubtful, at most four at a time, each with the preset's value as the recommended option and one line on the cost of the alternative.
4. Show the resulting table, the rules that fired, and what was chosen for the user by default. Ask for one confirmation.
5. Write `MORTIS.md` (the table, with the reason the user gave in the `Why` column) and one decision record for every value that differs from the preset.

Naming choices are asked only when the description mentions something existing that the project must fit (a database, an API style, consumers of its events). Identifier casing is never asked: the language decides it.

When the session cannot ask, the skill moves off the preset only for a value the description states outright; a value it only hints at keeps the preset's value and is listed under open questions.

A user who says "just give me the lean one in C# with PostgreSQL" gets no further questions.
