# Architecture overview

Read this first. It says what each project is for, which way dependencies point, and where every file of the template lives. The other references go deep on one layer each.

## The projects

| Project | Holds | May reference |
|---|---|---|
| `Shared` | Building blocks with no database library: `Entity`, `AggregateRoot`, `EventBase`, `EventContext`, the two event-handler bases, `ICommand<TContent>`, `IUserCommand`, `ITransactionalCommand`, `BaseCommandHandler`, `CommandPipeline`, `IRepository`, `IUnitOfWork`, the exceptions | nothing of ours |
| `Domain` | `Aggregates/<Plural>/`: aggregate root, child entities, value objects, `Events/`, service, repository interface, constants, module | `Shared` |
| `Application` | `<Plural>/`: commands (command + validator + handler in one file), DTOs, event handlers | `Domain`, `Shared` |
| `Infrastructure` | Everything that implements a Domain or Shared interface: `AppDbContext`, `EfRepository`, `UnitOfWork`, repositories, EF configurations, migrations, logging, the event dispatchers | `Domain`, `Shared` |
| `Api` | HTTP host: controllers, the `UserCommandFilter`, `ExceptionHandler`, `CorrelationIdMiddleware` | `Application`, `Infrastructure` |
| `Console` | Manual-testing host: runs any registered command by name, plus multi-step scenarios | `Application`, `Infrastructure` |
| `Tests` | `TestSupport/` (fakes, builders) and the tests | everything |

Dependencies point one way: `Shared ← Domain ← Application ← Api / Console`, and `Infrastructure → Domain`. Only Infrastructure knows EF Core and PostgreSQL. Only the two hosts wire everything together.

## Where every file lives

This is the solution without the example (`--no-example`); the example adds the aggregate, command, controller and test folders on top. Paths below drop the `Boilerplate.` prefix of the project folders.

```
src/Shared/Domain/          AggregateRoot.cs  Entity.cs  AuditUser.cs  SystemUser.cs  EventBase.cs  EventContext.cs
                            EventHandlerExecutor.cs  BaseSyncEventHandler.cs  BaseAsyncEventHandler.cs  IEventDispatcher.cs
src/Shared/Application/     ICommand.cs  IUserCommand.cs  BaseCommandHandler.cs  CommandPipeline.cs
                            CommandResult.cs  ICommandHandlerMiddleware.cs
                            ICommandDispatcher.cs  CommandDispatcher.cs  CommandHandlerExtensions.cs
src/Shared/Persistence/     IRepository.cs  IUnitOfWork.cs
src/Shared/Exceptions/      ExceptionBase.cs  BusinessRuleException.cs  DuplicateException.cs  ForbiddenException.cs
                            InvalidParameterException.cs  NotFoundException.cs  ReferenceViolationException.cs
                            TransactionRequiredException.cs  ValidationFailedException.cs
src/Shared/                 SharedModule.cs

src/Domain/                 DomainModule.cs   (one AddXModule() line per aggregate)
src/Application/            ApplicationModule.cs   (handlers, validators, event handlers)

src/Infrastructure/Persistence/   AppDbContext.cs  EfRepository.cs  UnitOfWork.cs  ConfigurationExtensions.cs
                                  DatabaseExceptionHandler.cs  DesignTimeDbContextFactory.cs
                                  PersistenceConstants.cs  PersistenceModule.cs
src/Infrastructure/Messaging/     LoggingEventDispatcher.cs  AsyncEventHandlerDispatcher.cs  MessagingConstants.cs
src/Infrastructure/Logging/       LoggingCommandHandlerMiddleware.cs
src/Infrastructure/               InfrastructureModule.cs

src/Api/                    Program.cs  appsettings.json
src/Api/Filters/            UserCommandFilter.cs
src/Api/Middlewares/        ExceptionHandler.cs  CorrelationIdMiddleware.cs  ErrorResponseModel.cs  ApiConstants.cs

src/Console/                Program.cs  appsettings.json
src/Console/Scenarios/      IScenario.cs  CommandScenario.cs  CommandScenarioExtensions.cs  ConsoleUserExtensions.cs
                            ScenarioConstants.cs  ScenarioModule.cs

test/Tests/TestSupport/     fakes and builders, mirroring the source folders
test/Tests/                 tests, mirroring the source folders

repo root                   AGENTS.md  CLAUDE.md  README.md  BannedSymbols.txt  Directory.Build.props
                            Directory.Packages.props  global.json  .editorconfig  Dockerfile  docker-compose.yml
                            docs/architecture.md  docs/adr/0000-template.md  plans/README.md  .vscode/launch.json  .vscode/tasks.json
```

Each aggregate the example adds follows one pattern; [domain-layer.md](domain-layer.md), [application-layer.md](application-layer.md) and [persistence.md](persistence.md) show it with real files. `AGENTS.md` §3 in the scaffolded solution is the checklist of every place an aggregate touches.

## The life of a request

1. The Api binds the body to a command; `UserCommandFilter` fills its `UserId` and `UserEmail` from the claims. (The Console does the same for its own user.)
2. The controller sends the command through `ICommandDispatcher`, which finds the registered `BaseCommandHandler<TCommand, TContent>` and calls it; the controller never names the handler. `CommandPipeline<TCommand>` validates, runs the middlewares, begins a transaction when the command is `ITransactionalCommand`, and calls `OnHandle`.
3. `OnHandle` calls one service method with `command.ToAuditUser()`. The service loads the aggregate, calls a method on it, saves through the repository. The aggregate raises events.
4. On commit, `UnitOfWork` saves, runs sync event handlers inside the transaction, commits, then hands queue events to the dispatchers, which run async handlers.
5. `ExceptionHandler` turns any `ExceptionBase` into `{ code, message, errors }` with the exception's HTTP status.

The decisions behind the layout (dispatcher, commands as interfaces, the user on the command, event handlers, the Console) are the ADRs in the solution's `docs/adr/`. The full account, with the commit order, is in `docs/architecture.md` of the scaffolded solution. Sync and async handlers are in [event-handlers.md](event-handlers.md).

## Other references

| When you need | Read |
|---|---|
| To add or change an aggregate, service or event | [domain-layer.md](domain-layer.md) |
| To add a command, a validator, a DTO, or change the pipeline | [application-layer.md](application-layer.md) |
| EF mapping, repositories, transactions, concurrency, migrations | [persistence.md](persistence.md) |
| Controllers, error responses, the Console runner, VS Code | [hosts.md](hosts.md) |
| To write a test, a fake or a builder | [testing.md](testing.md) |
| To change the template itself | [template-internals.md](template-internals.md) |
| To bring the building blocks into an existing solution | [adopting-in-an-existing-project.md](adopting-in-an-existing-project.md) |
