# Shared: commands, pipeline and dispatcher, C#

Project `{Name}.Shared`, folder `Application/`, namespace `{Name}.Shared.Application`. Layer 1: `building-blocks.md` sections Command, Validator, Command handler, Command pipeline, Dispatcher; `request-lifecycle.md` for the order.

## Command contracts

File `Application/ICommand.cs`, three marker interfaces, in this order: `ICommand`, `ICommand<TContent>`, `ITransactionalCommand`.

```csharp
public interface ICommand;
```

```csharp
public interface ITransactionalCommand;
```

with `public interface ICommand<TContent> : ICommand;` between the two.

- A command is a `record` that implements `ICommand<TContent>`, where `TContent` is its view. It has no base class.
- A command that writes also implements `ITransactionalCommand` and `IUserCommand`.

File `Application/IUserCommand.cs` holds the interface and its extension:

```csharp
public interface IUserCommand
```

```csharp
string UserId { get; set; }
```

```csharp
string UserEmail { get; set; }
```

```csharp
public static class UserCommandExtensions
```

```csharp
public static AuditUser ToAuditUser(this IUserCommand userCommand) => new(userCommand.UserId, userCommand.UserEmail);
```

- A command declares the two properties itself, each marked `[JsonIgnore]` so a client can neither send nor read them. The host fills them.

## CommandResult

File `Application/CommandResult.cs`, two classes:

```csharp
public class CommandResult
```

```csharp
public Dictionary<string, object> Meta { get; set; }
```

```csharp
public static CommandResult<TContent> FromContent<TContent>(TContent content)
{
    return new CommandResult<TContent>(content);
}
```

```csharp
public sealed class CommandResult<TContent>(TContent content) : CommandResult
```

```csharp
public TContent Content { get; } = content;
```

## The pipeline

File `Application/ICommandHandlerMiddleware.cs`:

```csharp
public interface ICommandHandlerMiddleware
```

```csharp
void Before(ICommand command);
```

```csharp
void After(ICommand command, CommandResult commandResult);
```

File `Application/CommandPipeline.cs`. Needs `using FluentValidation;` and the Shared `Exceptions` and `Persistence` namespaces.

```csharp
public sealed class CommandPipeline<TCommand>(
    IEnumerable<IValidator<TCommand>> validators,
    IEnumerable<ICommandHandlerMiddleware> commandHandlerMiddlewares,
    IUnitOfWork unitOfWork) where TCommand : ICommand
```

```csharp
public async Task<CommandResult<TContent>> ExecuteAsync<TContent>(TCommand command, Func<TCommand, Task<CommandResult<TContent>>> onHandle)
{
    var validationFailures = validators
        .SelectMany(validator => validator.Validate(command).Errors)
        .ToList();

    if (validationFailures.Count > 0)
    {
        throw new ValidationFailedException(validationFailures
            .GroupBy(validationFailure => validationFailure.PropertyName)
            .ToDictionary(group => group.Key, group => group.Select(validationFailure => validationFailure.ErrorMessage).ToArray()));
    }

    foreach (var commandHandlerMiddleware in commandHandlerMiddlewares)
    {
        commandHandlerMiddleware.Before(command);
    }

    CommandResult<TContent> commandResult;

    if (command is ITransactionalCommand)
    {
        var transactionKey = typeof(TCommand).FullName;

        try
        {
            await unitOfWork.BeginTransactionAsync(transactionKey);

            commandResult = await onHandle(command);

            await unitOfWork.CommitTransactionAsync(transactionKey);
        }
        catch (Exception)
        {
            await unitOfWork.RollbackTransactionAsync();

            throw;
        }
    }
    else
    {
        commandResult = await onHandle(command);
    }

    foreach (var commandHandlerMiddleware in commandHandlerMiddlewares)
    {
        commandHandlerMiddleware.After(command, commandResult);
    }

    return commandResult;
}
```

- Order: validate, middlewares before, begin (transactional only), handle, commit, middlewares after. Validation failure opens no transaction.
- The transaction key is the command's type name: a nested command's commit is ignored by the unit of work, so the outer command owns the commit.
- Any exception rolls back and is rethrown unchanged.

## The handler base

File `Application/BaseCommandHandler.cs`:

```csharp
public abstract class BaseCommandHandler<TCommand, TContent>(CommandPipeline<TCommand> commandPipeline) where TCommand : ICommand<TContent>
```

```csharp
protected abstract Task<CommandResult<TContent>> OnHandle(TCommand command);
```

```csharp
public Task<CommandResult<TContent>> Handle(TCommand command)
{
    return commandPipeline.ExecuteAsync(command, OnHandle);
}
```

File `Application/CommandHandlerExtensions.cs`, the only way a handler is registered:

```csharp
public static class CommandHandlerExtensions
```

```csharp
public static IServiceCollection AddCommandHandler<TCommand, TContent, TCommandHandler>(this IServiceCollection services)
    where TCommand : ICommand<TContent>
    where TCommandHandler : BaseCommandHandler<TCommand, TContent>
{
    services.AddScoped<BaseCommandHandler<TCommand, TContent>, TCommandHandler>();

    return services;
}
```

## The dispatcher

File `Application/ICommandDispatcher.cs`:

```csharp
public interface ICommandDispatcher
```

```csharp
Task<CommandResult<TContent>> Send<TContent>(ICommand<TContent> command);
```

File `Application/CommandDispatcher.cs` holds three types: the dispatcher and the two invokers it caches per command type. Needs `using Microsoft.Extensions.DependencyInjection;` and `using System.Collections.Concurrent;`.

```csharp
public sealed class CommandDispatcher(IServiceProvider serviceProvider) : ICommandDispatcher
```

```csharp
private static readonly ConcurrentDictionary<Type, object> CommandHandlerInvokers = new();
```

```csharp
public Task<CommandResult<TContent>> Send<TContent>(ICommand<TContent> command)
{
    ArgumentNullException.ThrowIfNull(command);

    var commandHandlerInvoker = (CommandHandlerInvoker<TContent>)CommandHandlerInvokers.GetOrAdd(
        command.GetType(),
        commandType => Activator.CreateInstance(typeof(CommandHandlerInvoker<,>).MakeGenericType(commandType, typeof(TContent))));

    return commandHandlerInvoker.Invoke(serviceProvider, command);
}
```

```csharp
public abstract class CommandHandlerInvoker<TContent>
```

```csharp
public abstract Task<CommandResult<TContent>> Invoke(IServiceProvider serviceProvider, ICommand<TContent> command);
```

```csharp
public sealed class CommandHandlerInvoker<TCommand, TContent> : CommandHandlerInvoker<TContent> where TCommand : ICommand<TContent>
```

```csharp
public override Task<CommandResult<TContent>> Invoke(IServiceProvider serviceProvider, ICommand<TContent> command)
{
    return serviceProvider.GetRequiredService<BaseCommandHandler<TCommand, TContent>>().Handle((TCommand)command);
}
```

- Callers name the command, never the handler. A command without a registered handler throws.

## Tests

`CommandPipelineTests`, `CommandDispatcherTests` and `UserCommandTests` in `testing-kernel.md`.

## Namespaces and usings

Every file of this page, with its namespace and the `using` lines it needs (`ImplicitUsings` covers `System`, `System.Linq`, `System.Threading.Tasks` and the like). A file the table does not list follows the namespace rule in `naming.md`.

| File | Namespace | Usings |
|---|---|---|
| `src/{Name}.Shared/Application/BaseCommandHandler.cs` | `{Name}.Shared.Application` | none |
| `src/{Name}.Shared/Application/CommandDispatcher.cs` | `{Name}.Shared.Application` | Microsoft.Extensions.DependencyInjection, System.Collections.Concurrent |
| `src/{Name}.Shared/Application/CommandHandlerExtensions.cs` | `{Name}.Shared.Application` | Microsoft.Extensions.DependencyInjection |
| `src/{Name}.Shared/Application/CommandPipeline.cs` | `{Name}.Shared.Application` | FluentValidation, {Name}.Shared.Exceptions, {Name}.Shared.Persistence |
| `src/{Name}.Shared/Application/CommandResult.cs` | `{Name}.Shared.Application` | none |
| `src/{Name}.Shared/Application/ICommand.cs` | `{Name}.Shared.Application` | none |
| `src/{Name}.Shared/Application/ICommandDispatcher.cs` | `{Name}.Shared.Application` | none |
| `src/{Name}.Shared/Application/ICommandHandlerMiddleware.cs` | `{Name}.Shared.Application` | none |
| `src/{Name}.Shared/Application/IUserCommand.cs` | `{Name}.Shared.Application` | {Name}.Shared.Domain |
