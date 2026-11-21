# Phase 1, step 18 — Advanced add-ons

Needs: phase 1 step 08 for most. Optional: each add-on is its own small knowledge-only step, done when a real project asks, with the same deliverables as steps 04 to 16. No C# is written, built or generated here; the last column is what phase 2 proves.

| Add-on | What it is | Behaviour to prove in phase 2 |
|---|---|---|
| `soft-delete` | `ISoftDeletable`, a query filter, unique indexes that ignore deleted rows, a `Deleted` event | a deleted product is invisible, its SKU can be reused, and it can be restored |
| `caching` | `ICacheService` (in-memory, and distributed through the database or Redis), key constants, invalidation by sync event handlers; never cache the write-side aggregate | a cached read is not stale after the command that changes it returns |
| `scheduler` | Recurring and one-shot jobs in the Worker, safe with several replicas, jobs send commands | with two workers a job fires once per schedule tick |
| `storage` | A file-storage port with S3 / MinIO and local-disk adapters | a file uploaded through the Api is readable through the port in a test against MinIO |
| `secrets` | Values encrypted at rest with an envelope key | a stored secret is unreadable in the database and readable through the port |
| `loop-guard` | A hop counter on the event context; a chain longer than the limit stops with an error | two handlers that trigger each other stop at the limit and the error names the chain |
| `contexts` | Bounded contexts: `Contexts/<Context>/<Plural>` in Domain, Application and Infrastructure; contexts talk through events and ids | an architecture test forbids a reference from one context's domain to another's |
| `events-project` | Event contracts in their own project or module so another service can reference them | a second sample consumer service builds against the contracts only |
| `ordered-handlers` | An explicit order among sync handlers of one event | handlers run in the declared order |
| `feature-flags` | A flag port read by pipeline middleware and handlers | a flagged command is refused when the flag is off |
| `email` | A mail port with an SMTP adapter and a development catcher in compose | an event consumer sends a message the catcher receives |
| `realtime` | Push to connected clients from event consumers | a client receives an event after the command commits |
| `sagas` | A process manager: state in a table, advanced by events, with timeouts from the scheduler | a three-step process survives a restart between steps |

Process practices that are documents, not code, and can be added to the core's `docs/` at any time: an incident log, a "where else must this change" checklist, a runbook template, a performance guide ("measure first"), agent-rule files for other assistants generated from `AGENTS.md`.

Left out unless asked: Kafka, event sourcing, gRPC, GraphQL, a database or schema per tenant.
