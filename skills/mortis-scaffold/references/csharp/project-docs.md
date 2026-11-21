# Project documents, C#

The markdown files a generated project gets. They are data: write each one exactly as shown, replacing `{Name}`. Remove a line only where this page says so. `MORTIS.md` is written by `mortis-architect` and stays as it is.

| File | Content |
|---|---|
| `MORTIS.md` | from `mortis-architect`, unchanged |
| `AGENTS.md` | below |
| `CLAUDE.md` | below |
| `README.md` | below |
| `plans/README.md` | below |
| `docs/architecture.md` | `project-docs-architecture.md` |
| `docs/adr/0000` to `0008`, `0010`, `0012`, `0009` with the correlation add-on, `0011` with an outbox, `0013` with RabbitMQ, `0014` with `auth` = `jwt`, `0015` with tenancy | `project-docs-adr.md` |

Without the example, remove from `README.md` and `AGENTS.md` §5 the lines that run `CreateProduct`; keep everything else.

## AGENTS.md

~~~~markdown
# {Name}: working agreement

The decisions this project was started with are in [MORTIS.md](MORTIS.md); read them first.

This file is the always-on contract for everyone who changes this solution, people and AI agents alike. It holds the rules that apply to all work. The reasoning and the request flow are in [docs/architecture.md](docs/architecture.md).

## 1. Map

```
src/
  {Name}.Shared/          building blocks: Entity, AggregateRoot, EventBase, EventContext, command pipeline, exceptions
  {Name}.Domain/          Aggregates/<Plural>/ : aggregate, child entities, value objects, Events/, repository interface; Services/ and Ports/ when needed
  {Name}.Application/     <Plural>/ : commands (command + validator + handler in one file), event handlers, DTOs
  {Name}.Infrastructure/  Persistence/ : AppDbContext, EfRepository, UnitOfWork, <Plural>/ repository + EF configuration, Migrations/
  {Name}.Api/             HTTP host: controllers, middlewares
  {Name}.Console/         manual-testing host: runs one command, `dotnet run --project src/{Name}.Console -- <Command> '<json>'`
test/
  {Name}.Tests/           TestSupport/ (every fake, builder and mock) + tests, named after the type under test
```

Dependencies point one way: `Shared ← Domain ← Application ← Api / Console`, and `Infrastructure → Domain`. Only Infrastructure knows EF Core and PostgreSQL. Only the two hosts wire everything together.

## 2. Non-negotiables

| # | Rule | What enforces it |
|---|---|---|
| 1 | **All business logic lives in the aggregate.** Properties are `protected set`, collections are a private `List<T>` exposed as `IReadOnlyCollection<T>`, state changes through named methods (`Complete`, `AddLine`, `Override`). A rule that needs several entities or another system goes to a domain service in `Services/`; command handlers only coordinate: load, call, save. | Review |
| 2 | **Child entities and value objects are reached only through their aggregate root.** A child's constructor and mutating methods are `internal`. A child gets no repository and no command. A value object is an immutable `record` that validates itself in its constructor. | Compiler (`internal`) |
| 3 | **Events are raised inside the aggregate method that changes the state.** Event records are past-tense names (`Created`, `Completed`) in `Aggregates/<Plural>/Events/` and carry ids as strings, never entities. | Compiler (`RaiseEvent` is `protected`), repository tests |
| 4 | **Ids are `Guid.CreateVersion7()` and are assigned in the domain, never by the database.** Time is `DateTime.UtcNow`. No `.Result`, no `.Wait()`. | Build fails (`BannedSymbols.txt`, RS0030); `ValueGeneratedNever()` in the configuration extensions |
| 5 | **Every command that writes implements `ITransactionalCommand`.** | A repository write outside a transaction throws `TransactionRequiredException` |
| 6 | **Load with `FindByIdTrackingAsync` before you change an aggregate**, then call `UpdateAsync`. | `UpdateAsync` throws for an untracked aggregate |
| 7 | **A repository write is sent to the database immediately.** Statement order inside a handler is behaviour, not style. | Review |
| 8 | **A domain service file holds the interface and the class, nothing else.** Primary-constructor injection. No private methods and no local functions in a domain service, `*CommandHandler` or `*EventHandler`: a rule goes to the aggregate, value object or domain service, the steps stay inline. No instance field that remembers data between calls. There is no `<Aggregate>Service`. A domain service is pure by default, may read through a domain interface (a narrow read port preferred) when the rule decides what it needs, and **never writes**: no `CreateAsync`, `UpdateAsync`, `DeleteAsync`, no `IUnitOfWork`. The command handler owns every write and the commit. | Review; `verify.sh` fails a write call or `IUnitOfWork` in Domain |
| 9 | **A change to a second aggregate is an event handler, unless the action is about both.** Ask: without the second change, does the command still do what its name says? If yes, it is a consequence: an event handler in the Application layer, in the *reacting* aggregate's folder (`Application/<Plural>/`), named `<Action>When<Aggregate><Event>EventHandler`. If no (a transfer between two accounts), the command handler loads and saves both. | Review |
| 10 | **A command handler is the use case**: it loads the aggregates the action needs, calls their operations or a domain service, stamps the user, saves, and maps the result with `XDto.Map(...)`. It holds no business rule. **A host sends a command and never names its handler**: a controller, the Console's runner, an event handler or an event consumer injects `ICommandDispatcher` and calls `Send(command)`. An event handler or consumer sends a command, never calls a repository or a domain service, so validation, logging and the transaction apply to a reaction as they do to a request. | Review |
| 11 | **A validator checks the shape of the input and is synchronous.** It never reads the database. Uniqueness is a unique index; existence is a foreign key; rules about state belong in the aggregate. | Review |
| 12 | **A reference to another aggregate is its id plus a foreign key** in the EF configuration, with the delete behaviour written there. Never a `kind` column plus an untyped id. No application check that repeats what the foreign key already guarantees. | Configuration tests |
| 13 | **Registration is explicit**: `DomainModule` (domain services), `PersistenceModule` (repositories), `ApplicationModule` (`AddCommandHandler<TCommand, TContent, THandler>()`, validators, sync event handlers); event consumers with one `AddEventConsumer<TConsumer>(endpoint => endpoint.Retry(n))` line each in the host that runs them. No assembly scanning. | Review |
| 14 | **Tests**: xUnit `Assert` only. A test class holds `[Fact]` / `[Theory]` methods and nothing else. Every mock, fake and builder lives in `TestSupport/`. A test file is named after the type it tests. Take the failing run first. | Review |
| 15 | **No comments in code**, in `src/` or `test/`. The reason for a decision goes to `docs/`. | Review |
| 16 | **Locals are named after the thing they hold**: `orderRepository`, never `repository`, `service`, `builder` or a `Mock` suffix. Constants live in a `*Constants` class. | Review |
| 17 | **A new abstraction needs two real implementations today**, or it is a boundary a test must replace. Choosing one of several implementations is keyed dependency injection, never a factory with a `switch`. Write the decision down in `docs/adr/`. | Review |
| 18 | **A new NuGet package needs the owner's agreement.** No mediator, mapping or assertion library: the command pipeline, `Dto.Map` and xUnit `Assert` are the choices here. | Review |
| 19 | **A reaction is a sync event handler or an event consumer, and its base class says which.** `BaseSyncEventHandler<TEvent>` runs inside the transaction, awaited, ordered by `Sequence`; a throw rolls the whole command back, so it is the place for a rule that must hold together with the change. `BaseEventConsumer<TEvent>` runs after COMMIT in its own scope and transaction as the `system` user; its failure is logged and never undoes the original change, so it is for eventual consistency (stock, read models, notifications). An event with an event consumer implements `IQueueEvent` and carries every value the consumer needs; the consumer never reloads the publisher. A consumer runs only when the host that runs it has its `AddEventConsumer` line, and `Retry(n)` there is its retry count. Write event consumers for at-least-once delivery, because a broker will replace the in-process dispatcher. | Compiler (`BaseEventConsumer` requires `IQueueEvent`), dispatcher and handler tests |
| 20 | **What the host knows is data on the command, never ambient.** A command that writes implements `IContextCommand` with one line, `[JsonIgnore] public CommandContext Context { get; set; }`: never a user, correlation or tenant property of its own. The host fills it: `ContextCommandFilter` in the Api, `.AsConsoleUser()` in the Console. The handler passes `command.Context.User` to the aggregate. An event handler sends its command with `Context = ContextOf(@event)`. There is no `IUserContext` and no ambient correlation id. | Filter, pipeline, event-context and handler tests; `verify.sh` |

## 3. Adding an aggregate: every place it touches

| Project | Files |
|---|---|
| Domain | `Aggregates/<Plural>/<Aggregate>.cs`, `Events/*.cs`, `I<Aggregate>Repository.cs`, `<Aggregate>Constants.cs` |
| Infrastructure | `Persistence/<Plural>/<Aggregate>Repository.cs`, `<Aggregate>Configuration.cs`, one line in `PersistenceModule.cs`, and a migration |
| Application | `<Plural>/<Verb><Aggregate>Command.cs` per command, `<Aggregate>Dto.cs`, and the handler and validator lines in `ApplicationModule.cs` |
| Api | `Controllers/<Plural>Controller.cs` |
| Tests | `TestSupport/Domain/<Plural>/Fake<Aggregate>.cs`, `<Aggregate>RepositoryBuilder.cs`; `Domain/<Plural>/<Aggregate>Tests.cs`; `Infrastructure/Persistence/<Plural>/<Aggregate>RepositoryTests.cs`, `<Aggregate>ConfigurationTests.cs`; `Application/<Plural>/<Command>HandlerTests.cs` per command that writes, and one per event handler |

An aggregate whose key the database assigns (`int`, `long`) raises `Created` from `MarkCreated()` after the insert, never in its constructor (ADR 0008).

Write the fakes, builders and tests first, watch them fail, then write the code.

## 4. How work is done

- **Know how it will be verified before you start**: a test, a Console command, or a request against the running Api.
- **Name the files you expect to touch before you touch them.** A finding outside that list is written down, not fixed on the way.
- **Fixing a bug**: first a test that fails with the bug's own symptom, then the fix, then the same test passes unchanged. Never narrow the scenario, never fit the expected value to the output, never weaken an assertion that went red.
- **"Done" needs output.** Show the build and test result; say which part was skipped and why.
- **Git**: an AI agent does not run `git add` or `git commit`, and never runs `checkout`, `restore`, `stash`, `reset`, `clean`, `rebase`, `merge` or `revert`. It leaves its work in the working tree and says what changed.
- **Plans** go to `plans/<work>/` and are deleted when the work ends. What should outlive the work moves to `docs/`.
- **When something has to be explained twice, it is missing from the docs.** Write it into this file or `docs/` in the same change.

## 5. Commands

```bash
dotnet build
dotnet test
docker compose up -d postgres
dotnet ef database update --project src/{Name}.Infrastructure
dotnet run --project src/{Name}.Console -- <Command> '<json>'  # runs one command; F5 in VS Code asks for both
dotnet ef migrations add <Name> --project src/{Name}.Infrastructure --output-dir Persistence/Migrations
```

## 6. Skills

| Task | Skill |
|---|---|
| Start another project, or change an option in MORTIS.md | `mortis-architect`, then `mortis-scaffold` |
| Add or change an aggregate, child entity, value object, event, command, mapping or test | `mortis-develop` |
| Check a change against the rules above | `mortis-review` |
~~~~

## CLAUDE.md

~~~~markdown
@AGENTS.md

The decisions this project was started with are in [MORTIS.md](MORTIS.md).

If the import above did not load, open [AGENTS.md](AGENTS.md) before doing anything else. It is the single source of the rules for this solution; this file only points to it.
~~~~

## README.md

~~~~markdown
# {Name}

A .NET 10 backend built from domain-driven-design building blocks: aggregates, child entities, value objects, domain events, repositories, domain services and commands, on EF Core and PostgreSQL.

## Run it

```bash
docker compose up -d postgres
dotnet build
dotnet test
dotnet ef database update --project src/{Name}.Infrastructure
dotnet run --project src/{Name}.Api
```

`dotnet run --project src/{Name}.Console --` lists the scenarios and the commands you can run by name, for example `-- CreateProduct '{"sku":"BOLT","name":"Bolt","stock":10}'`. In VS Code, F5 on "Console: run a command or scenario" asks for the name and the JSON.

## Read next

- [AGENTS.md](AGENTS.md): the rules, and every place an aggregate touches.
- [docs/architecture.md](docs/architecture.md): what happens when a command runs, how events flow, how persistence behaves.
- [MORTIS.md](MORTIS.md): the decisions this project was started with.
- [docs/adr/](docs/adr/): why it is built this way (the dispatcher, commands as interfaces, the user on the command, event handlers, the Console, handlers as use cases, one context on the command).
~~~~

## plans/README.md

~~~~markdown
# Plans

A plan for a piece of work lives here and nowhere else: `plans/<work-name>/README.md`, in kebab-case.

A plan states, in this order:

1. **Goal**: what will be true when the work is done.
2. **Now / repro**: what happens today, or how to reproduce the bug.
3. **Expected**: the behaviour after the change, as something that can be observed.
4. **Surface**: the files that will be touched. Leaving this list later needs a reason.
5. **Constraints**: what must not change.
6. **Verification**: the test, Console scenario or request that proves it. If this line cannot be written, the work is not ready to start.
7. **End**: what closes the work.

A plan carries no commit steps. When the work ends the plan folder is deleted, and anything that should outlive it moves to `docs/`.
~~~~
