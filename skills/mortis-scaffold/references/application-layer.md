# Application layer

Everything here is `src/Application/<Plural>/` plus the command kernel in `src/Shared/Application/`. A command is one file holding three things: the command, its validator and its handler.

## Anatomy of a command file

`CreateProductCommand.cs` in the example:

| Part | What it is |
|---|---|
| `CreateProductCommand` | A `record` that names its result type: `ICommand<ProductDto>, IUserCommand, ITransactionalCommand` when it writes, `ICommand<ProductDto>` when it only reads. There is no base class: a command is a record that implements interfaces. Properties are the input. |
| `CreateProductCommandValidator` | A FluentValidation `AbstractValidator<T>`. Synchronous, checks shape only (lengths, ranges, non-empty). It never reads the database. |
| `CreateProductCommandHandler` | Extends `BaseCommandHandler<TCommand, TContent>`. `OnHandle` calls one service method and maps the result with `ProductDto.Map(...)`. |

A DTO (`ProductDto`, `OrderDto`) is a `record` with a static `Map(aggregate)`. There is no mapping library.

```csharp
protected override async Task<CommandResult<ProductDto>> OnHandle(CreateProductCommand command)
{
    var product = await productService.CreateAsync(command.ToAuditUser(), command.Id, command.Sku, command.Name, command.Stock);

    return CommandResult.FromContent(ProductDto.Map(product));
}
```

Register every handler and validator in `ApplicationModule.cs`; there is no assembly scanning. `AddCommandHandler<CreateProductCommand, ProductDto, CreateProductCommandHandler>()` registers a handler under its base type, which is how the dispatcher finds it. Event handlers are registered there too (`BaseSyncEventHandler<Completed>` → the handler class).

## Sending a command

A host never injects a command handler. It injects `ICommandDispatcher` and sends the command:

```csharp
var commandResult = await commandDispatcher.Send(new GetProductCommand(id));

return Ok(commandResult.Content);
```

`Send<TContent>(ICommand<TContent> command)` returns a `CommandResult<TContent>`, so the compiler knows the result type from the command alone. The dispatcher (`CommandDispatcher` in `Shared/Application/`) caches one small invoker per command type, resolves `BaseCommandHandler<TCommand, TContent>` from the scope, and calls `Handle`, so validation, middlewares and the transaction run exactly as before. A command with no registered handler fails at the first `Send` (the DI container cannot resolve its handler); `ApplicationModule` is the one place to look.

## The user on a command

There is no `IUserContext`. The user is data on the command, filled by the host:

- `IUserCommand` has `UserId` and `UserEmail`. A writing command implements it and declares both properties with `[JsonIgnore]` (a client can never send them); `ToAuditUser()` is an extension method on the interface.
- Api: `UserCommandFilter` runs before every action and writes the claims into every `IUserCommand` argument, clearing what the client sent. See [hosts.md](hosts.md).
- Console: `CommandScenario` sets the `console` user; a scenario that builds commands itself calls `.AsConsoleUser()`.
- Event handlers: a sync handler passes `@event.UserId` and `@event.UserEmail` on, so the whole chain is audited as the original user. An async handler uses `SystemUser.Id` and `SystemUser.Email`.
- Queries (`Get*` commands) write nothing and implement only `ICommand<TDto>`.

## The pipeline

`CommandPipeline<TCommand>` is what `BaseCommandHandler.Handle` runs:

1. Validate with every registered `IValidator<TCommand>`; failures throw `ValidationFailedException`.
2. `ICommandHandlerMiddleware.Before` for each middleware (`LoggingCommandHandlerMiddleware` logs the start).
3. If the command is `ITransactionalCommand`: begin a transaction keyed by the command's type name.
4. `OnHandle`.
5. Commit; any exception rolls back, clears events and is rethrown.
6. `After` for each middleware.

A command called from inside another command's transaction (an event handler sending a command) joins it. The first to begin owns the commit.

A command result is mapped **before** commit, so it does not show what a sync event handler changes afterwards. Read the aggregate again to see it.

## Rules

- A handler calls one service method and holds no business rule.
- A controller only sends a command. An event handler sends a command, never calls a service or a repository, so validation, logging and the transaction apply to a reaction as they do to a request.
- No private methods or local functions in a `*CommandHandler` or `*EventHandler`.
- Name the handler and locals after the thing: `createProductCommandHandler`, `productService`.

## Adding a command

1. Test first: a handler test in `test/Tests/Application/<Plural>/`, with the builders from `TestSupport` ([testing.md](testing.md)).
2. The command file: record, validator, handler. `ICommand<TDto>, IUserCommand, ITransactionalCommand` if it writes (plus the two `[JsonIgnore]` user properties).
3. Two lines in `ApplicationModule.cs` (`AddCommandHandler<...>()`, validator).
4. A controller action that sends the command, and an `AddCommandScenario<TCommand, TContent>()` line in `ScenarioModule.cs` if you want to run it from the Console.
