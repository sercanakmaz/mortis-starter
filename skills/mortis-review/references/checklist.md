# The judgement checklist

What `review.sh` cannot decide. Walk it over the changed files, layer by layer; each item names the rule it comes from.

## Domain

- **Is every rule in the aggregate?** A condition about the aggregate's own state belongs in its method, which throws a coded exception (AGENTS 1). The script flags handlers that branch on `Status` or `Is...`; look also for rules hidden in validators or mapping.
- **Children only through the root** (AGENTS 2): no public constructor or setter on a child entity, no repository or command for it.
- **Events are past tense, raised in the method that changes the state, and carry ids as strings** (AGENTS 3). A queue event carries every value its consumers need, so they never reload the publisher (AGENTS 19).
- **A domain service is pure or reads through a narrow port, and never writes** (AGENTS 8, decision 31). A service that takes a whole repository to call one finder should take a read port in `Domain/Ports/` (a warning). Any write, save, transaction or unit of work in Domain is an error (the script catches the calls).

## Application

- **The second-aggregate test** (AGENTS 9, decision 28): when a command handler changes two aggregates, ask whether the command still does what its name says without the second change. If yes, the second change is a consequence and belongs in an event handler in the reacting aggregate's folder. If no (a transfer between two accounts), it stays. Never judge by counting calls.
- **A handler coordinates only** (AGENTS 10): load, call, stamp the user, save, map. No `if` on business state (the script flags the obvious ones), no loops that implement a rule.
- **Sync or async** (AGENTS 19): a reaction that must hold with the change is a sync handler; one that may lag (stock, notifications, read models) is a consumer of an `IQueueEvent`. A consumer whose command is not idempotent is a finding with any outbox.
- **Validators check shape only** (AGENTS 11): a validator that reads the database, or checks existence or uniqueness, is a finding; that is a foreign key or a unique index.
- **One context line, filled by the host** (AGENTS 20): no user, tenant or correlation property of its own on a command.

## Infrastructure

- **References between aggregates are an id plus a foreign key** with the delete behaviour written in the configuration (AGENTS 12).
- **Raw SQL and bulk updates** add the tenant filter by hand with tenancy, and `is_deleted = false` with soft delete.
- **A migration matches the model change** and was not edited after being applied elsewhere.
- **Secrets** (with the add-on): a secret column is never filtered on and never logged.

## Hosts

- **A host sends commands and never names a handler** (AGENTS 10; the script catches type names).
- **Every consumer has its `AddEventConsumer` line in the host that runs it**, with a deliberate `Retry(n)` (AGENTS 13).
- **Errors leave as the error body**, never as a framework default page.

## Tests

- **A new behaviour has a test that was seen failing**, named `Method_Condition_Should_Result`, in a class that holds only tests (AGENTS 14).
- **A bug fix has a test with the bug's own symptom and a row in `docs/incidents.md`.** An assertion weakened to make a test pass is an error.
- **Fakes and builders live in `TestSupport/`**; mocks are wrapped in builders.

## Every change

- **The places it forgot**: `../mortis-develop/references/where-else.md` for the options that are on.
- **Names** from the closed list of `../mortis-develop/references/canonical-shapes.md`; no `{Aggregate}Service`, `Manager`, `Helper`.
- **No new package or abstraction without its record in `docs/adr/`** (AGENTS 17, 18).
- **No comments in code** (AGENTS 15): the reason goes to `docs/`.
