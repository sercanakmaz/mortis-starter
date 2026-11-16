# Decisions about these skills

Decisions about the skills repository itself: what it contains, how it is laid out and how it is checked. Decisions about the generated backend are architecture decision records inside the template: `skills/ddd-backend-scaffold/assets/csharp/docs/adr/` (0001 the command dispatcher, 0002 commands as records with interfaces, 0003 the user on the command, 0004 event handlers, 0005 the Console).

## Decided

| # | Decision | Why |
|---|---|---|
| 1 | The shape comes from `csharp_boilerplate`; the rules and safeguards come from the Zeyrek backend (banned APIs, no comments in code, no private methods in services and handlers, explicit registration, tests that hold only `[Fact]` / `[Theory]`). | The boilerplate is the structure the author wants to reuse; Zeyrek is where the rules were learned. |
| 2 | The stack is .NET 10, EF Core and PostgreSQL, with our own command pipeline and no mediator library. Code and messages are English. | One stack done well. A mediator would add a dependency for what `CommandPipeline` and `CommandDispatcher` already do. |
| 3 | A skill is `SKILL.md` plus `references/` (an architecture overview and one reference per layer), modelled on Zeyrek's `architecture-overview.md`. The template's structure is described in those files, not left for the reader to rediscover. | `SKILL.md` stays short; Claude reads only the reference a task needs. |
| 4 | There is one folder per language: `assets/csharp/`. It is a complete solution named `Boilerplate` with the Order / Customer / Product example inside. `--no-example` removes the example with the manifest `assets/csharp-example.txt`. A Go version would be `assets/go/` and `--lang go`. | There used to be a `templates/` and an `examples/` folder for the same thing, and the example had no `Shared` layer. Two copies drifted; one real solution cannot. |
| 5 | The template compiles and is tested as it is. `tests/smoke.sh` scaffolds three solutions (with example, `--no-example`, `--kernel-only`) and checks them against a real PostgreSQL. `tests/check_docs.py` fails when a reference names a file or type that does not exist, or the overview omits a template source file. | The documentation is only trustworthy if a change cannot leave it behind. |
| 6 | Event handlers have two kinds, sync (inside the transaction, can veto) and async (after the commit, eventually consistent), both in the Application layer, both sending commands. | The reaction to an event must run a use case; see ADR 0004. |
| 7 | "Use case" is called "command". A host sends a command through `ICommandDispatcher` and never names its handler. Commands are records that implement `ICommand<TContent>`, `IUserCommand` and `ITransactionalCommand`; the user is data on the command, filled by the presentation layer. | ADRs 0001 to 0003. |
| 8 | The Console runs any registered command by name and does not migrate. Migrations are `dotnet ef database update`. | ADR 0005. |
| 9 | Work is done in phases. Phase 1 (`ddd-backend-scaffold`) is complete. | One tested thing at a time. |
| 10 | The assistant does not commit. The maintainer reviews and commits. | The maintainer owns the history. |
| 11 | A small change is made at once; a big one gets a plan first. | Keeps the maintainer in control of direction. |

## Open

These were discussed and are **not** started. Each needs a go-ahead.

- **`ddd-backend-develop`** (phase 2): add an aggregate, child entity, value object, event, command, mapping and test with the repository's rules.
- **`ddd-backend-review`** (phase 3): a script and checklist that check a change against the rules.
- **Messaging and scheduling**: `--with-messaging` (a broker adapter and an outbox) and `--with-scheduler`.
- **A Go template** under `assets/go/`.
- **A Query split** for the `Get*` commands, which are queries in everything but name.
- **One `AuditUser User` property** on writing commands in place of `UserId` and `UserEmail` (ADR 0002).
- **Authentication**: not configured, so an Api write has an empty audit user until claims exist.
