---
name: ddd-backend-scaffold
description: Scaffolds a new .NET 10 backend solution built from domain-driven-design building blocks (aggregates, child entities, value objects, domain events with sync and async handlers, repositories, domain services, commands) on EF Core and PostgreSQL, or copies just the building blocks into a solution that already exists. Use whenever the user wants to start, create, bootstrap or scaffold a C# / .NET backend, API, service or microservice, asks for a .NET "boilerplate", "starter", "template" or "DDD / clean project structure", or wants an existing .NET project to adopt this structure, even when they never say "DDD".
---

# Scaffold a DDD backend

This skill creates a solution that builds and passes its tests the moment it exists. The files come from a template that is itself compiled and tested, so run the script instead of writing the files by hand: retyping sixty files is slower and drifts from the tested version.

## What the solution contains

```
src/<Name>.Shared          building blocks, no database library
src/<Name>.Domain          Aggregates/<Plural>/ with Events/ inside each aggregate folder
src/<Name>.Application     commands (command + validator + handler in one file), DTOs, event handlers; hosts send commands through ICommandDispatcher
src/<Name>.Infrastructure  EF Core + PostgreSQL: AppDbContext, EfRepository, UnitOfWork, repositories, mappings
src/<Name>.Api             HTTP host
src/<Name>.Console         manual-testing host with Scenarios/
test/<Name>.Tests          TestSupport/ (fakes, builders) + tests
AGENTS.md, CLAUDE.md, docs/architecture.md, docs/adr/ (decisions 0001-0005), plans/, Dockerfile, docker-compose.yml
```

It also carries its guards: `BannedSymbols.txt` makes `Guid.NewGuid()`, `DateTime.Now`, `.Result` and `.Wait()` build errors; a repository write outside a transaction throws; `UpdateAsync` refuses an aggregate that was not loaded for tracking.

## Reference map

Start with [references/architecture-overview.md](references/architecture-overview.md): the projects, the dependency rule, where every template file lives, and the life of a request. Then read only what the task needs:

| The task | Read |
|---|---|
| Add or change an aggregate, a service or an event | [references/domain-layer.md](references/domain-layer.md) |
| Add a command, a validator or a DTO; where the user on a command comes from | [references/application-layer.md](references/application-layer.md) |
| Reactions to events, sync versus async, why an effect arrives late or fails silently | [references/event-handlers.md](references/event-handlers.md) |
| EF mapping, repositories, transactions, concurrency, migrations | [references/persistence.md](references/persistence.md) |
| Controllers, error responses, running commands from the Console, VS Code | [references/hosts.md](references/hosts.md) |
| Write a test, a fake or a builder | [references/testing.md](references/testing.md) |
| Change the template or the example themselves | [references/template-internals.md](references/template-internals.md) |
| Bring the building blocks into an existing solution | [references/adopting-in-an-existing-project.md](references/adopting-in-an-existing-project.md) |

The references describe the template and the example exactly: `tests/check_docs.py` in the skills repository fails when a file or type they name does not exist, or a template source file is missing from the overview. The scaffolded solution also carries `docs/architecture.md` (the long account of the request flow, events and persistence) and `AGENTS.md` (the rules), so what you read there is what the user gets.

## Steps

1. **Settle the name and the place.** The name is PascalCase and may contain dots (`Acme`, `Acme.Billing`). If the user gave none, ask. The script refuses a target folder that already has files in it, so never point it at a folder with work in it.
2. **Decide on the example.** By default the solution includes a complete worked example: an `Order` aggregate with an `OrderLine` child entity and a `Money` value object, `Customer` and `Product` aggregates referenced by foreign key, sync event handlers (the customer is updated when an order completes; the order is cancelled when its last line is) and async ones (product stock moves when a line is added or cancelled), commands, controllers, Console scenarios, a migration and about a hundred tests. Keep it when the user wants to see how the pieces fit or says nothing either way; they can delete the example folders later. Pass `--no-example` when they ask for an empty or minimal solution.
3. **Run the script.**

   ```bash
   bash <skill-dir>/scripts/scaffold.sh <Name> [--lang csharp] [--no-example] [--out <parent-dir>]
   ```

   `--lang` picks the folder under `assets/`; `csharp` is the only language today. It needs `bash` and `perl` (both ship with macOS and Linux). It does not need the .NET SDK.
4. **Verify.** Run these in the new folder and read the output:

   ```bash
   dotnet --version        # needs 10.x; if it is older, say so and stop here
   dotnet build            # expect: 0 Warning(s), 0 Error(s)
   dotnet test             # expect: Passed!, 44 tests with `--no-example`, 141 with the example
   ```

   If the build or a test fails, fix the cause or report it with the output. Do not describe the solution as ready on the strength of the script finishing.
5. **Offer the database check** when Docker or a PostgreSQL is available. This is what proves the persistence layer, which unit tests cannot fully do:

   ```bash
   docker compose up -d postgres
   dotnet ef database update --project src/<Name>.Infrastructure
   dotnet run --project src/<Name>.Console -- CreateProduct '{"sku":"BOLT","name":"Bolt","stock":10}'   # example only: any command by name
   dotnet run --project src/<Name>.Console -- order-flow         # example only
   dotnet run --project src/<Name>.Console -- stock-flow         # example only: sync and async handlers on one event
   dotnet run --project src/<Name>.Console -- blocked-customer   # example only: a sync handler vetoes and the command rolls back
   ```

   With `--no-example` there are no tables yet, so `database update` has nothing to apply until the first aggregate and its migration exist.
6. **Report** in a few lines: where the solution is, the build and test result as printed, whether the database check ran, and the next step (below).

## After scaffolding

- The first real change is usually an aggregate. `AGENTS.md` §3 in the new solution lists every file an aggregate touches, and the `ddd-backend-develop` skill walks through it.
- A new migration: `dotnet ef migrations add <Name> --project src/<Name>.Infrastructure --output-dir Persistence/Migrations` (needs `dotnet tool install --global dotnet-ef`).
- The connection string is `ConnectionStrings:Default` in both `appsettings.json` files; the environment variable `ConnectionStrings__Default` overrides it.

## An existing solution

When the user wants this structure inside a solution that already exists, do not scaffold over it. Use `--kernel-only`, which copies only `<Name>.Shared` and the build guards, and follow [references/adopting-in-an-existing-project.md](references/adopting-in-an-existing-project.md).

## Changing the template itself

Read [references/template-internals.md](references/template-internals.md) first, and the decision records in the template's `docs/adr/` for why it is the way it is. In short: the solution in `assets/csharp/` is a real one named `Boilerplate` with the example inside, `--no-example` removes it through `assets/csharp-example.txt`, and `tests/smoke.sh` in the skills repository is the proof that a change still works. When you change a file the references name, change the reference in the same edit; `tests/check_docs.py` (run by the smoke script) catches the ones you forget.

## Limits to state plainly when they matter

- Queue events (`IQueueEvent`) reach the in-process async handlers right after the commit and are logged. There is no message-broker adapter and no outbox table yet, so an async handler is lost if the process dies between the commit and its run.
- Error messages are English only; authentication is not configured.
- The target framework is .NET 10 and the database is PostgreSQL. Other databases need a different EF provider and their own error mapping in `DatabaseExceptionHandler`.
