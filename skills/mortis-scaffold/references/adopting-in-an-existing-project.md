# Adopting the building blocks in an existing solution

Use this when the solution already exists and has code worth keeping. The goal is to move one aggregate at a time, with the old code still running, never a rewrite.

## 1. Copy the kernel

```bash
bash <skill-dir>/scripts/scaffold.sh <Name> --kernel-only --out <a-temp-dir>
```

This writes `src/<Name>.Shared/` and three `*.example` files. Use the existing solution's root namespace as `<Name>` so the namespaces match.

1. Move `src/<Name>.Shared` into the solution and add it with `dotnet sln add`.
2. Copy `BannedSymbols.txt` to the solution root.
3. Merge the examples into the solution's own files, then delete them:
   - `Directory.Build.props.example`: the `BannedApiAnalyzers` reference and the `AdditionalFiles` line are what matter. The template also sets `<Nullable>disable</Nullable>`; keep the existing solution's own setting.
   - `Directory.Packages.props.example`: only if the solution uses central package management; otherwise add the versions to the project files.
   - `.editorconfig.example`: the line `dotnet_diagnostic.RS0030.severity = error` is the one that makes a banned call fail the build.
4. Build. Existing code that calls `Guid.NewGuid()`, `DateTime.Now`, `.Result` or `.Wait()` now fails. Count the failures before deciding: fix them when there are few; when there are many, set the severity to `warning` for now, record the count, and bring it down as files are touched. A count that goes up is the signal.

## 2. Decide where persistence lives

`<Name>.Shared` has only `IRepository` and `IUnitOfWork`. The EF Core implementations are in the full template under `assets/csharp/src/Boilerplate.Infrastructure/Persistence/`. Copy `EfRepository.cs`, `UnitOfWork.cs`, `DatabaseExceptionHandler.cs`, `ConfigurationExtensions.cs` and `PersistenceConstants.cs` into the project that owns the solution's `DbContext`, and replace `AppDbContext` with that context's type.

Check these three things against the existing context before going further, because the rules depend on them:

| The template assumes | If the existing solution differs |
|---|---|
| Ids are assigned in code and mapped with `ValueGeneratedNever()` | Existing tables with database-generated keys keep working for aggregates you have not migrated. Do not mix: an aggregate you migrate gets code-assigned ids for itself and its children |
| One `DbContext` per unit of work | With several contexts, give each its own unit of work and its own transactional marker interface |
| PostgreSQL error codes in `DatabaseExceptionHandler` | Map the provider's own unique and foreign-key error codes |

## 3. Wire the pipeline beside what exists

Register `AddSharedModule()` and the unit of work in the host. Nothing changes for existing endpoints until a controller sends a command through `ICommandDispatcher`. If the solution already has a mediator, leave it: new work goes through command handlers, old handlers stay until their aggregate is migrated. Do not call an old mediator handler from inside a new command and expect one transaction unless both use the same `DbContext` instance.

## 4. Migrate one aggregate

Pick the smallest aggregate with real behaviour. In this order:

1. Write characterisation tests for what the old code does today, through its public entry point.
2. Move the state and its rules into an `AggregateRoot<TKey>` class: `protected set`, named methods, events raised inside those methods.
3. Add the repository interface, the EF configuration and the repository class.
4. Move orchestration into `<Aggregate>Service`; move each entry point into a command.
5. Point the controller at the command handlers. Run the characterisation tests unchanged.
6. Delete the old path for that aggregate only.

`AGENTS.md` §3 from the full template lists every file an aggregate touches. Copy `AGENTS.md` and `docs/architecture.md` from `assets/csharp/` into the solution once the first aggregate is migrated, and edit the map in §1 to match the real project names.

## What not to do

- Do not scaffold a full solution on top of an existing one, or rename existing projects to match the template in the same change as moving logic.
- Do not migrate two aggregates in one change. The second one hides which change broke a test.
- Do not add a compatibility layer that lets old services call new aggregates through setters. An aggregate with public setters is the thing being removed.
