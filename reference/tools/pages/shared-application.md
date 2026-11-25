# Shared: commands, pipeline and dispatcher, C#

Project `{Name}.Shared`, folder `Application/`, namespace `{Name}.Shared.Application`. Layer 1: `building-blocks.md` sections Command, Validator, Command handler, Command pipeline, Dispatcher; `request-lifecycle.md` for the order.

## Command contracts

File `Application/ICommand.cs`, three marker interfaces, in this order: `ICommand`, `ICommand<TContent>`, `ITransactionalCommand`.

@@decl src/Boilerplate.Shared/Application/ICommand.cs::ICommand@@

@@decl src/Boilerplate.Shared/Application/ICommand.cs::ITransactionalCommand@@

with `public interface ICommand<TContent> : ICommand;` between the two.

- A command is a `record` that implements `ICommand<TContent>`, where `TContent` is its view. It has no base class.
- A command that writes also implements `ITransactionalCommand` and `IContextCommand`.

File `Application/CommandContext.cs`: everything the host knows about the call. The host fills it; the request body never does.

@@decl src/Boilerplate.Shared/Application/CommandContext.cs::CommandContext@@

@@member src/Boilerplate.Shared/Application/CommandContext.cs::CommandContext::User@@

@@member src/Boilerplate.Shared/Application/CommandContext.cs::CommandContext::CorrelationId@@

@@member src/Boilerplate.Shared/Application/CommandContext.cs::CommandContext::CausationId@@

@@member src/Boilerplate.Shared/Application/CommandContext.cs::CommandContext::TenantId@@

@@member src/Boilerplate.Shared/Application/CommandContext.cs::CommandContext::Language@@

@@member src/Boilerplate.Shared/Application/CommandContext.cs::CommandContext::Complete@@

@@member src/Boilerplate.Shared/Application/CommandContext.cs::CommandContext::FromEvent@@

File `Application/IContextCommand.cs`:

@@decl src/Boilerplate.Shared/Application/IContextCommand.cs::IContextCommand@@

@@member src/Boilerplate.Shared/Application/IContextCommand.cs::IContextCommand::Context@@

- A command declares it once, `[JsonIgnore] public CommandContext Context { get; set; }`, so a client can neither send nor read it.
- `TenantId` and `Language` exist for the tenancy and language options and stay empty without them.

## CommandResult

File `Application/CommandResult.cs`, two classes:

@@decl src/Boilerplate.Shared/Application/CommandResult.cs::CommandResult@@

@@member src/Boilerplate.Shared/Application/CommandResult.cs::CommandResult::Meta@@

@@member src/Boilerplate.Shared/Application/CommandResult.cs::CommandResult::FromContent@@

```csharp
public sealed class CommandResult<TContent>(TContent content) : CommandResult
```

```csharp
public TContent Content { get; } = content;
```

## The pipeline

File `Application/ICommandHandlerMiddleware.cs`:

@@decl src/Boilerplate.Shared/Application/ICommandHandlerMiddleware.cs::ICommandHandlerMiddleware@@

@@member src/Boilerplate.Shared/Application/ICommandHandlerMiddleware.cs::ICommandHandlerMiddleware::Before@@

@@member src/Boilerplate.Shared/Application/ICommandHandlerMiddleware.cs::ICommandHandlerMiddleware::After@@

File `Application/CommandPipeline.cs`. Needs `using FluentValidation;` and the Shared `Exceptions` and `Persistence` namespaces.

@@decl src/Boilerplate.Shared/Application/CommandPipeline.cs::CommandPipeline@@

@@member src/Boilerplate.Shared/Application/CommandPipeline.cs::CommandPipeline::ExecuteAsync@@

@@member src/Boilerplate.Shared/Application/CommandPipeline.cs::CommandPipeline::HandleAsync@@

- A context command first gets a complete context (a correlation id when it had none), and the event context is told which command is running, so every event raised until it ends carries its context. A command sent inside it gets its own, and the outer one is restored after.
- Order: validate, middlewares before, begin (transactional only), handle, commit, middlewares after. Validation failure opens no transaction.
- The transaction key is the command's type name: a nested command's commit is ignored by the unit of work, so the outer command owns the commit.
- Any exception rolls back and is rethrown unchanged.

## The handler base

File `Application/BaseCommandHandler.cs`:

@@decl src/Boilerplate.Shared/Application/BaseCommandHandler.cs::BaseCommandHandler@@

@@member src/Boilerplate.Shared/Application/BaseCommandHandler.cs::BaseCommandHandler::OnHandle@@

@@member src/Boilerplate.Shared/Application/BaseCommandHandler.cs::BaseCommandHandler::Handle@@

File `Application/CommandHandlerExtensions.cs`, the only way a handler is registered:

@@decl src/Boilerplate.Shared/Application/CommandHandlerExtensions.cs::CommandHandlerExtensions@@

@@member src/Boilerplate.Shared/Application/CommandHandlerExtensions.cs::CommandHandlerExtensions::AddCommandHandler@@

## The dispatcher

File `Application/ICommandDispatcher.cs`:

@@decl src/Boilerplate.Shared/Application/ICommandDispatcher.cs::ICommandDispatcher@@

@@member src/Boilerplate.Shared/Application/ICommandDispatcher.cs::ICommandDispatcher::Send@@

File `Application/CommandDispatcher.cs` holds three types: the dispatcher and the two invokers it caches per command type. Needs `using Microsoft.Extensions.DependencyInjection;` and `using System.Collections.Concurrent;`.

@@decl src/Boilerplate.Shared/Application/CommandDispatcher.cs::CommandDispatcher@@

@@member src/Boilerplate.Shared/Application/CommandDispatcher.cs::CommandDispatcher::CommandHandlerInvokers@@

@@member src/Boilerplate.Shared/Application/CommandDispatcher.cs::CommandDispatcher::Send@@

@@decl src/Boilerplate.Shared/Application/CommandDispatcher.cs::CommandHandlerInvoker@@

@@member src/Boilerplate.Shared/Application/CommandDispatcher.cs::CommandHandlerInvoker::Invoke@@

```csharp
public sealed class CommandHandlerInvoker<TCommand, TContent> : CommandHandlerInvoker<TContent> where TCommand : ICommand<TContent>
```

@@member src/Boilerplate.Shared/Application/CommandDispatcher.cs::CommandHandlerInvoker#2::Invoke@@

- Callers name the command, never the handler. A command without a registered handler throws.

## Tests

`CommandPipelineTests`, `CommandDispatcherTests`, `ContextCommandTests` and `CommandContextTests` in `testing-kernel.md`.

## Namespaces and usings

Every file of this page, with its namespace and the `using` lines it needs (`ImplicitUsings` covers `System`, `System.Linq`, `System.Threading.Tasks` and the like). A file the table does not list follows the namespace rule in `naming.md`.

@@usings shared-application.md@@
