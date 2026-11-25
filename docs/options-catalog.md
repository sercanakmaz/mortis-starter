# Options catalog

What a person starting a project can choose, and what each option costs. Nothing here is built yet unless the "Built" column says so. The build order is in [roadmap.md](roadmap.md) and each step's brief is in [`docs/plans/`](plans/README.md). The exact keys and values are in [specs/decisions-file.md](specs/decisions-file.md).

The **Built** column says whether the option exists in the hand-written C# solution today (`reference/template/`). Marks such as (W1) point to the sources at the end.

## How options fit together

Three kinds of choice, because they behave differently in code:

| Kind | Meaning | Examples |
|---|---|---|
| **Slot** | Exactly one must be chosen; the core cannot run without it | language, database, event delivery |
| **Add-on** | Zero or more; each adds files and registration lines | multi-tenancy, correlation, caching |
| **Level** | One axis with rungs from lean to full | exception handling, testing, validation |

Each option has a **tier**: **L** (lean: a small service or a prototype), **S** (standard: a real product with one team), **A** (advanced: one service of a platform, with a broker and many tenants; each service is its own repository). A preset picks a whole tier; the interview then lets the user move single options up or down.

## 1. Slots

### 1.1 Language

| Option | Tier | Built | Notes |
|---|---|---|---|
| C# / .NET 10 | all | yes | The proven path. |
| Go | all | no | Follows C#. Idiomatic Go, not translated C#: explicit `context.Context`, errors as values, no DI container, packages by aggregate. Likely `pgx` + `sqlc` for PostgreSQL (W). |

### 1.2 Database

| Option | Tier | Built | Notes |
|---|---|---|---|
| PostgreSQL (EF Core / pgx) | all | yes | Transactions make the outbox simple. `xmin` gives optimistic concurrency for free. |
| MongoDB | all | no | An aggregate is one document, so child entities need no mapping. A multi-document transaction needs a replica set; without one the outbox must live inside the aggregate document. Changes the repository, unit of work, migrations and outbox modules. |

### 1.3 Event delivery

How an event raised by an aggregate reaches the handlers that run after the commit. Sync handlers (inside the transaction) exist in every model. There are exactly three models:

| `events.outbox` | What happens | Loses events when | Tier | Built |
|---|---|---|---|---|
| `none` | After COMMIT the event consumers run in the same process | the process dies between COMMIT and the handler | L | yes |
| `after-command` | The event row is written in the same transaction; right after COMMIT the same request publishes it and marks it published. A sweeper publishes rows left behind | never (at-least-once) | S | no |
| `background` | The request only writes the row; a relay job polls and publishes | never (at-least-once); adds a delay of one polling interval | A | no |

`after-command` and `background` are one module with one setting. Any outbox means **at-least-once** delivery, so handlers must be idempotent: the inbox (2.4) comes with it.

### 1.4 Event transport (only with an outbox)

| Option | Tier | Built | Notes |
|---|---|---|---|
| Database as the queue | S | no | The relay job runs the handlers itself, straight from the outbox table (`FOR UPDATE SKIP LOCKED` in PostgreSQL). No broker to run. One process or a few. |
| RabbitMQ | A | no | Needed when another process or service consumes the events. Brings exchanges, queues, retry and dead-letter queues, and a consumer host. |

**Library decision.** MassTransit 9 is a commercial product and official maintenance of version 8 ends after 2026 (W1). OpenTransit, the community fork, has not published releases yet (W6). The starter ships **its own small bus**, wire-compatible with MassTransit 8 (same exchange names, same envelope, same message URNs), so existing MassTransit consumers keep working and the Go version can speak the same format. The full reasoning and the format are in [specs/messaging-wire-format.md](specs/messaging-wire-format.md).

## 2. Methodology add-ons and levels

### 2.1 Command pipeline

| Option | Kind | Tier | Built | Notes |
|---|---|---|---|---|
| Command, handler, dispatcher | core | all | yes | ADR 0001, 0002 |
| Validation: none / a validator per command (FluentValidation) / validator plus domain invariants only | level | L / S / S | yes (per command) | Rules that need the database belong in the domain service. |
| Transaction marker (`ITransactionalCommand`) | core | all | yes | |
| Query side: `Get*` as commands / separate `IQuery` and query handlers / read-model projections | level | L / S / A | yes (first rung) | |
| Pipeline behaviors as plug-ins (logging, write guards, feature gates) | add-on | A | yes (middleware interface) | |
| Idempotent commands (an idempotency key on the request) | add-on | A | no | Protects `POST` retries. |

### 2.2 Domain model

| Option | Kind | Tier | Built | Notes |
|---|---|---|---|---|
| Aggregate, child entity, value object, domain service, repository interface | core | all | yes | |
| Key type per aggregate (`Entity<TKey>`): Guid v7, `int` / `long` identity, string, strongly typed id | core, chosen per aggregate | all | yes (Guid only) | An integer key is generated by the database, so the id is unknown until the insert. That changes "the client may send the id" and the event payload. |
| Audit fields from the command's user | core | all | yes | ADR 0003 |
| Soft delete | add-on | S | no | A filter on every query; a unique index must ignore deleted rows. |
| Optimistic concurrency | per aggregate | S | yes (`xmin` on Product) | |
| Bounded contexts (`Contexts/<Context>/<Plural>`) | level: flat / contexts | L / A | yes (flat) | |
| A separate `Events` project for contracts | add-on | A | no | Needed once another process consumes events. |

### 2.3 Exception handling (level)

| Rung | Contains | Tier | Built |
|---|---|---|---|
| 1. Minimal | `NotFound`, `Validation`, `BusinessRule`, one error body | L | |
| 2. Coded | A base exception with a stable code and HTTP status; database errors (unique, foreign key) mapped to domain errors | S | yes |
| 3. Catalogued | A module / error-code catalogue, parameters for translated messages, log level by status class, an "unexpected" wrapper | A | no |

Go uses the same rungs with sentinel and typed errors instead of exceptions.

### 2.4 Events and consistency

| Option | Kind | Tier | Built | Notes |
|---|---|---|---|---|
| Sync and event consumers in Application | core | all | yes | ADR 0004 |
| Correlation id: request header in, onto the command, onto every event, into logs | add-on | S | no | Also a causation id (which event caused this command). |
| Inbox / idempotent consumer (processed-message table) | add-on, required by any outbox | S | no | W2, W3 |
| Retry and dead-letter policy | add-on | S | yes (retry on concurrency only) | |
| Handler-loop guard | add-on | A | no | Listed as a known gap in the template. |
| Ordered handlers | add-on | A | no | |
| Saga / process manager | add-on | A | no | Late; depends on RabbitMQ and the scheduler. |

### 2.5 Multi-tenancy (add-on)

One model: a shared database where an aggregate or entity opts in by implementing `IMultiTenant`, which gives it a `TenantId`. Reads are filtered and writes are stamped automatically; an aggregate without the interface is global. Tier A (available to any preset). Today: none.

A database or schema per tenant is **not offered**: it is exceptional, and it multiplies migrations and connection handling. One rule matters most in the shared model: a background job or consumer must set the tenant before it resolves anything that touches the database.

The host resolves the tenant (header, claim or subdomain) and puts it on the command's context; events, outbox rows and bus messages carry it. Details: [phase 1, step 11](plans/phase-1-skills/11-multi-tenancy.md).

### 2.6 Multi-language (add-on)

| Part | Tier | Built |
|---|---|---|
| Translated error and validation messages (error code plus parameters, resource files, a language middleware) | S | no |
| Translated data (a column that holds a value per language) | A | no |

### 2.7 Testing (level)

| Rung | Contains | Tier | Built |
|---|---|---|---|
| 1. Unit | Aggregate and service tests with fakes and builders, kernel tests | L | yes |
| 2. Plus handlers and hosts | Command handler, filter, scenario and mapping-shape tests | S | yes |
| 3. Plus integration | Repository and outbox tests against a real database in a container (Testcontainers), consumer tests | S, A | no |
| 4. Plus architecture and contract tests | Layer-dependency tests, event-contract tests, coverage gate | A | no |

The house rules stay at every rung: fakes and builders live in `TestSupport`, a test class holds only tests.

### 2.8 Hosts and cross-cutting add-ons

| Option | Tier | Built | Notes |
|---|---|---|---|
| Api host, Console command runner | all | yes | ADR 0005 |
| Worker host (outbox relay, consumers, jobs) | S | no | Arrives with the outbox. |
| Authentication: JWT bearer / OpenID Connect | S | no | Fills `IUserCommand` (today the audit user of an Api write is empty). |
| Authorization: policy per command / permission catalogue | S / A | no | |
| API surface: OpenAPI, versioning, pagination and filtering conventions, standard error body | S | yes (OpenAPI) | |
| Structured logging with correlation; request logging | S | yes (command logging) | |
| Observability: OpenTelemetry traces and metrics, health checks | S | no | |
| Caching: in-memory / distributed, invalidated by sync event handlers | A | no | |
| Scheduler: recurring jobs, clustered | A | no | |
| File storage port (S3 / MinIO) | A | no | |
| Email port | A | no | |
| Real-time push (SignalR / WebSocket) | A | no | |
| Feature flags | A | no | |

### 2.9 Further options

| Option | Tier | Phase 1 step |
|---|---|---|
| One context on the command (user, correlation, tenant, language) in place of one property pair per concern | core | 04 |
| Unknown request members are refused | S | 16 |
| Partial update that tells "absent" from "null" | S | 16 |
| Standard list and error response shapes, a pagination helper | S | 16 |
| Permission per command | S | 15 |
| Secrets encrypted at rest | A | 20 |
| Per-event exchange type and name override | A | 09 |
| Aggregate version conflict as a domain error, for every database | S | 13 |
| Strategy selection through keyed dependency injection, never a factory `switch` | S | already a rule |
| A closed vocabulary of class names and one reference slice to copy | S | 03, 17 |
| A fixed order for writing a new aggregate, test first | S | 17 |
| Incident log, "where else must this change" checklist, runbook, performance guide | A | 17, 20 |
| Agent-rule files for other assistants generated from `AGENTS.md` | A | 21 |
| Error fingerprint, query statistics, request logging | A | 16 |
| Lease and heartbeat for long jobs | A | 20 |

### 2.10 Engineering standards (always on, strictness is a level)

| Option | Built |
|---|---|
| `AGENTS.md` rules with what enforces each | yes |
| Banned APIs at build time | yes |
| ADR template and a "definition of done" | yes |
| Coding standards: no comments, no private helpers in orchestration classes, explicit registration | yes |
| Migrations: EF migrations / FluentMigrator / SQL files | yes (EF) |
| Container and CI: Dockerfile, compose, a CI workflow, a Makefile or task file | yes (first two) |

## 3. Presets

| | Lean | Standard | Advanced |
|---|---|---|---|
| Event delivery | `none` (in-process) | `after-command`, database as queue | `background`, RabbitMQ |
| Consistency add-ons | no | correlation, inbox, retry | plus loop guard, sagas |
| Exceptions | minimal | coded | catalogued |
| Validation | validator per command | same | same |
| Queries | `Get*` commands | separate queries | read models |
| Tenancy | no | no | `IMultiTenant` |
| Language | no | no (asked when languages are mentioned) | translated messages and data |
| Testing | unit | plus handlers, hosts, integration | plus architecture and contract |
| Hosts | Api, Console | plus Worker, authentication | plus scheduler, caching, storage |
| Layout | flat aggregates | flat aggregates | bounded contexts, `Events` project |

## 4. Rules between options

- Any outbox needs the inbox add-on and a Worker host (or a hosted service inside the Api).
- RabbitMQ needs an outbox. Publishing to a broker without one loses events.
- Multi-tenancy puts the tenant on events, so the outbox table, the inbox and every consumer carry it.
- MongoDB without a replica set cannot use a separate outbox collection in one transaction.
- An integer key excludes client-supplied ids for that aggregate.
- Translated messages need at least coded exceptions.
- Sagas need RabbitMQ and the scheduler.

## 5. Left out on purpose (can be reopened)

Kafka, event sourcing, gRPC, GraphQL, a database or schema per tenant, a front end, Kubernetes manifests, anything specific to one product.

## Sources

1. MassTransit, "Announcing MassTransit v9": https://masstransit.io/introduction/v9-announcement
2. Chris Richardson, "Pattern: Transactional outbox": https://oreil.ly/sLfnp
3. Polling and log-tailing relays, at-least-once delivery: https://gravitee.io/corpus/gen-1697/event-driven-programming/transactional-outbox-and-change-data-capture-cdc-patterns-for-reliable-event-publishing.html
4. Three Dots Labs, outbox with Watermill in Go: https://academy.threedots.tech/knowledge/outbox/
5. PostgreSQL tenancy models compared: https://trackit.io/how-to-choose-the-right-multitenant-database-design-for-your-saas
6. OpenTransit, status of the fork: https://opentransitlab.github.io/OpenTransit
7. MassTransit, message types and the envelope: https://masstransit.io/architecture/interoperability
