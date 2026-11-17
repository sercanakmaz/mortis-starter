# Adopting the building blocks in an existing solution, C#

Use this when the solution already exists and has code worth keeping. Move one aggregate at a time, with the old code still running. Never a rewrite, and never a generation over existing files.

## 1. Write MORTIS.md

Run the `mortis-architect` skill with the solution's root namespace as `name`, and `example` = `false`. The rows describe where the solution is going; record in its `Why` column what the solution does today where it differs.

## 2. Bring in the kernel

1. Add a project `src/{Name}.Shared` with the tools (`solution.md` "Create it with the tools", the Shared line only) and write it from `shared-domain.md`, `shared-application.md` and `shared-other.md`.
2. Add `BannedSymbols.txt` and the analyzer lines of `Directory.Build.props` (`solution.md`) to the solution's own files. Keep the solution's `Nullable` setting.
3. Build. Count the banned calls the existing code now fails on. Fix them when there are few; when there are many, set the RS0030 severity to `warning`, record the count, and bring it down as files are touched.

## 3. Bring in persistence

Write `EfRepository`, `UnitOfWork`, `DatabaseExceptionHandler`, `ConfigurationExtensions` and `PersistenceConstants` (`infrastructure-postgres.md`) into the project that owns the solution's `DbContext`, using that context's type in place of `AppDbContext`. Check these against the existing context first:

| The kernel assumes | If the solution differs |
|---|---|
| Ids are assigned in code (`ValueGeneratedNever()`) | Existing tables keep their keys; an aggregate you migrate gets code-assigned ids for itself and its children |
| One `DbContext` per unit of work | Each context gets its own unit of work and its own transactional marker |
| PostgreSQL error codes | Map the provider's own unique and foreign-key codes in `DatabaseExceptionHandler` |

## 4. Wire the pipeline beside what exists

Register `AddSharedModule()` and the unit of work in the host. Nothing changes for existing endpoints until a controller sends a command through `ICommandDispatcher`. An existing mediator stays until its aggregate is migrated; never call it from inside a new command and expect one transaction unless both use the same `DbContext` instance.

## 5. Migrate one aggregate

Pick the smallest aggregate with real behaviour, in this order:

1. Characterisation tests for what the old code does today, through its public entry point.
2. Move state and rules into an `AggregateRoot<TKey>` class (`domain.md`).
3. The repository interface, mapping and repository (`infrastructure-postgres.md`).
4. One command file per entry point, its handler loading and saving the aggregate (`application.md`).
5. Point the controller at the dispatcher. Run the characterisation tests unchanged.
6. Delete the old path for that aggregate only.

Copy `AGENTS.md` and `docs/architecture.md` (`project-docs.md`) into the solution once the first aggregate is migrated, and edit the map in `AGENTS.md` §1 to the real project names.

## What not to do

- Rename existing projects in the same change that moves logic.
- Migrate two aggregates in one change.
- Let old code call new aggregates through setters.
