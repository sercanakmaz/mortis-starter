# Testing, C#

One test project, `test/{Name}.Tests` (xUnit, Moq). Its folders mirror `src/`: a test of `src/{Name}.Domain/Aggregates/Orders/Order.cs` is `test/{Name}.Tests/Domain/Orders/OrderTests.cs`. Fakes, builders and recorders live only under `TestSupport/`, mirrored the same way. The namespace follows the path: `{Name}.Tests.Domain.Orders`, `{Name}.Tests.TestSupport.Domain.Orders`. Every file of this page is in the table at the end, with its namespace and usings.

## Rules

- A test class holds only methods marked `[Fact]` or `[Theory]`. No fields, no constructor, no helper method: what a test needs comes from `TestSupport`.
- Assertions use xUnit `Assert` only.
- Names: `Method_Condition_Should_Result` or `Method_Should_Result`.
- Arrange, act, assert, separated by one blank line.
- A mock is wrapped in a builder: `{Type}Builder.Create().With...(...).Build()`, and `BuildMock()` when a test verifies a call.
- A fake aggregate is a subclass, `Fake{Aggregate}`, with `Create()` and `With...()` methods that call the real operations.
- No test talks to a database, the network or the clock. Persistence tests build the context against a connection string that is never opened.
- `Guid.NewGuid()`, `DateTime.Now`, `.Result` and `.Wait()` are build errors here too.

## Test support for the kernel

Every type below is written in every project, example or not.

### Shared/Application

File `TestSupport/Shared/Application/FakeCommand.cs` holds four types:

```csharp
public record FakeCommand : ICommand<string>
```

```csharp
public string Name { get; set; } = "Default name";
```

```csharp
public record FakeTransactionalCommand : ICommand<string>, ITransactionalCommand
```

```csharp
public FakeTransactionalCommandValidator()
{
    RuleFor(command => command.Name).NotEmpty();
}
```

```csharp
public record FakeContextCommand : ICommand<string>, IContextCommand
```

```csharp
[JsonIgnore] public CommandContext Context { get; set; }
```

```csharp
public string Name { get; set; } = "Default name";
```

`FakeTransactionalCommand` has the same `Name` property; `FakeTransactionalCommandValidator` is `AbstractValidator<FakeTransactionalCommand>`.

Three handlers record what they handled. `FakeCommandHandler.cs`:

```csharp
public class FakeCommandHandler(CommandPipeline<FakeCommand> commandPipeline, List<FakeCommand> handled) : BaseCommandHandler<FakeCommand, string>(commandPipeline)
```

```csharp
protected override Task<CommandResult<string>> OnHandle(FakeCommand command)
{
    handled.Add(command);

    return Task.FromResult(CommandResult.FromContent(command.Name));
}
```

`FakeTransactionalCommandHandler.cs` and `FakeContextCommandHandler.cs` are the same for `FakeTransactionalCommand` and `FakeContextCommand`.

```csharp
public class CommandPipelineBuilder<TCommand> where TCommand : ICommand
```

```csharp
private readonly List<IValidator<TCommand>> _validators = [];
```

```csharp
private readonly List<ICommandHandlerMiddleware> _commandHandlerMiddlewares = [];
```

```csharp
private IUnitOfWork _unitOfWork = UnitOfWorkBuilder.Create().Build();
```

```csharp
private EventContext _eventContext = new();
```

```csharp
public static CommandPipelineBuilder<TCommand> Create() => new();
```

```csharp
public CommandPipelineBuilder<TCommand> WithValidator(IValidator<TCommand> validator)
{
    _validators.Add(validator);
    return this;
}
```

```csharp
public CommandPipelineBuilder<TCommand> WithCommandHandlerMiddleware(ICommandHandlerMiddleware commandHandlerMiddleware)
{
    _commandHandlerMiddlewares.Add(commandHandlerMiddleware);
    return this;
}
```

```csharp
public CommandPipelineBuilder<TCommand> WithUnitOfWork(IUnitOfWork unitOfWork)
{
    _unitOfWork = unitOfWork;
    return this;
}
```

```csharp
public CommandPipelineBuilder<TCommand> WithEventContext(EventContext eventContext)
{
    _eventContext = eventContext;

    return this;
}
```

```csharp
public CommandPipeline<TCommand> Build()
{
    return new CommandPipeline<TCommand>(_validators, _commandHandlerMiddlewares, _unitOfWork, _eventContext);
}
```

```csharp
public class CommandDispatcherBuilder
```

```csharp
private readonly ServiceCollection _services = new();
```

```csharp
public static CommandDispatcherBuilder Create() => new();
```

```csharp
public CommandDispatcherBuilder WithCommandHandler<TCommand, TContent>(BaseCommandHandler<TCommand, TContent> commandHandler) where TCommand : ICommand<TContent>
{
    _services.AddSingleton(commandHandler);
    return this;
}
```

```csharp
public ICommandDispatcher Build()
{
    return new CommandDispatcher(_services.BuildServiceProvider());
}
```

```csharp
public class CommandHandlerMiddlewareBuilder
```

```csharp
private readonly Mock<ICommandHandlerMiddleware> _mock = new();
```

```csharp
public static CommandHandlerMiddlewareBuilder Create() => new();
```

```csharp
public CommandHandlerMiddlewareBuilder WithRecordedCalls(List<string> calls)
{
    _mock.Setup(commandHandlerMiddleware => commandHandlerMiddleware.Before(It.IsAny<ICommand>()))
        .Callback<ICommand>(_ => calls.Add(nameof(ICommandHandlerMiddleware.Before)));

    _mock.Setup(commandHandlerMiddleware => commandHandlerMiddleware.After(It.IsAny<ICommand>(), It.IsAny<CommandResult>()))
        .Callback<ICommand, CommandResult>((_, _) => calls.Add(nameof(ICommandHandlerMiddleware.After)));

    return this;
}
```

```csharp
public ICommandHandlerMiddleware Build() => _mock.Object;
```

### Shared/Domain

```csharp
public static class FakeAuditUser
```

```csharp
public const string Id = "test-user-id";
```

```csharp
public const string Email = "test-user@localhost";
```

```csharp
public static AuditUser Create() => new(Id, Email);
```

```csharp
public static AuditUser CreateOther() => new("other-user-id", "other-user@localhost");
```

File `FakeEvent.cs`, three records:

```csharp
public record FakeEvent(string Id) : EventBase;
```

```csharp
public record FakeCascadedEvent(string Id) : EventBase;
```

```csharp
public record FakeQueueEvent(string Id) : EventBase, IQueueEvent;
```

```csharp
public class FakeAggregate : AggregateRoot<Guid>
```

```csharp
private FakeAggregate(Guid id, AuditUser user) : base(user)
{
    Id = id;
}
```

```csharp
private FakeAggregate() { }
```

```csharp
public static FakeAggregate Create(Guid id) => new(id, FakeAuditUser.Create());
```

```csharp
public static FakeAggregate Create() => Create(Guid.CreateVersion7());
```

```csharp
public static FakeAggregate CreateEmpty() => new();
```

```csharp
public FakeAggregate WithRaisedEvent(EventBase @event)
{
    RaiseEvent(@event);
    return this;
}
```

```csharp
public FakeAggregate WithUpdatedAt(DateTime updatedAt)
{
    UpdatedAt = updatedAt;
    return this;
}
```

File `RecordingEventHandler.cs` holds two handlers:

```csharp
public class RecordingEventHandler<TEvent>(string name, int sequence, List<string> handledBy, EventContext eventContext = null, EventBase cascadedEvent = null) : BaseSyncEventHandler<TEvent> where TEvent : EventBase
```

```csharp
public override int Sequence => sequence;
```

```csharp
protected override async Task OnHandle(TEvent @event)
{
    await Task.Delay(TimeSpan.FromMilliseconds(20));

    handledBy.Add(name);

    if (cascadedEvent is not null) eventContext.AddRaised(cascadedEvent);
}
```

```csharp
public class ThrowingEventHandler<TEvent> : BaseSyncEventHandler<TEvent> where TEvent : EventBase
```

```csharp
protected override Task OnHandle(TEvent @event)
{
    throw new InvalidOperationException("a sync handler vetoes the transaction");
}
```

File `RecordingEventConsumer.cs` holds two consumers of `FakeQueueEvent`; each records its class name:

```csharp
public class RecordingEventConsumer(List<string> calls, int failures = 0, Exception failure = null) : BaseEventConsumer<FakeQueueEvent>
```

```csharp
private int _failed;
```

```csharp
protected override Task OnHandle(FakeQueueEvent @event)
{
    calls.Add(GetType().Name);

    if (_failed >= failures) return Task.CompletedTask;

    _failed++;

    throw failure;
}
```

```csharp
public class OtherRecordingEventConsumer(List<string> calls) : RecordingEventConsumer(calls);
```

```csharp
public class EventHandlerExecutorBuilder
```

```csharp
private readonly ServiceCollection _services = new();
```

```csharp
private EventContext _eventContext = new();
```

```csharp
public static EventHandlerExecutorBuilder Create() => new();
```

```csharp
public EventHandlerExecutorBuilder WithEventContext(EventContext eventContext)
{
    _eventContext = eventContext;
    return this;
}
```

```csharp
public EventHandlerExecutorBuilder WithEventHandler<TEvent>(BaseSyncEventHandler<TEvent> eventHandler) where TEvent : EventBase
{
    _services.AddSingleton(eventHandler);
    return this;
}
```

```csharp
public EventHandlerExecutor Build()
{
    return new EventHandlerExecutor(_services.BuildServiceProvider(), _eventContext);
}
```

### Shared/Persistence

```csharp
public class UnitOfWorkBuilder
```

```csharp
private readonly Mock<IUnitOfWork> _mock = new();
```

```csharp
public static UnitOfWorkBuilder Create() => new();
```

```csharp
public UnitOfWorkBuilder WithRecordedCalls(List<string> calls)
{
    _mock.Setup(unitOfWork => unitOfWork.BeginTransactionAsync(It.IsAny<string>()))
        .Callback<string>(_ => calls.Add(nameof(IUnitOfWork.BeginTransactionAsync)))
        .Returns(Task.CompletedTask);

    _mock.Setup(unitOfWork => unitOfWork.CommitTransactionAsync(It.IsAny<string>()))
        .Callback<string>(_ => calls.Add(nameof(IUnitOfWork.CommitTransactionAsync)))
        .Returns(Task.CompletedTask);

    _mock.Setup(unitOfWork => unitOfWork.RollbackTransactionAsync())
        .Callback(() => calls.Add(nameof(IUnitOfWork.RollbackTransactionAsync)))
        .Returns(Task.CompletedTask);

    return this;
}
```

```csharp
public UnitOfWorkBuilder WithHasTransaction(bool hasTransaction)
{
    _mock.Setup(unitOfWork => unitOfWork.HasTransaction).Returns(hasTransaction);

    return this;
}
```

```csharp
public UnitOfWorkBuilder WithSaveChangesAsync(Action callback = null)
{
    _mock.Setup(unitOfWork => unitOfWork.SaveChangesAsync())
        .Callback(() => callback?.Invoke())
        .ReturnsAsync(1);

    return this;
}
```

```csharp
public IUnitOfWork Build() => _mock.Object;
```

```csharp
public Mock<IUnitOfWork> BuildMock() => _mock;
```

### Infrastructure

```csharp
public class AppDbContextBuilder
```

```csharp
public static AppDbContextBuilder Create() => new();
```

```csharp
public AppDbContext Build()
{
    var options = new DbContextOptionsBuilder<AppDbContext>()
        .UseNpgsql("Host=never-connected;Database=unit-tests")
        .UseSnakeCaseNamingConvention()
        .Options;

    return new AppDbContext(options);
}
```

```csharp
public class RecordingLogger<TCategory> : ILogger<TCategory>
```

```csharp
public List<(LogLevel Level, Exception Exception)> Entries { get; } = [];
```

```csharp
public IDisposable BeginScope<TState>(TState state) where TState : notnull => null;
```

```csharp
public bool IsEnabled(LogLevel logLevel) => true;
```

```csharp
public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
{
    Entries.Add((logLevel, exception));
}
```

`EventPublisherBuilder` is on `events-inprocess.md`.

### Api

```csharp
public class ActionExecutingContextBuilder
```

```csharp
private readonly DefaultHttpContext _httpContext = new();
```

```csharp
private readonly Dictionary<string, object> _arguments = [];
```

```csharp
public static ActionExecutingContextBuilder Create() => new();
```

```csharp
public ActionExecutingContextBuilder WithUser(string id, string email)
{
    _httpContext.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, id), new Claim(ClaimTypes.Email, email)], "test"));
    return this;
}
```

```csharp
public ActionExecutingContextBuilder WithCorrelationId(Guid correlationId)
{
    _httpContext.Items[ApiConstants.CorrelationIdItem] = correlationId;

    return this;
}
```

```csharp
public ActionExecutingContextBuilder WithArgument(string name, object value)
{
    _arguments[name] = value;
    return this;
}
```

```csharp
public ActionExecutingContext Build()
{
    var actionContext = new ActionContext(_httpContext, new RouteData(), new ActionDescriptor());

    return new ActionExecutingContext(actionContext, [], _arguments, new object());
}
```

```csharp
public static ActionExecutionDelegate NextOf(ActionExecutingContext context) =>
    () => Task.FromResult(new ActionExecutedContext(context, [], new object()));
```

## The kernel tests

Every project has them; each is written out in `testing-kernel.md`.

| Class | File |
|---|---|
| `AggregateRootTests`, `EventContextTests`, `EventHandlerExecutorTests` | `Shared/Domain/` |
| `CommandPipelineTests`, `CommandDispatcherTests`, `ContextCommandTests`, `CommandContextTests` | `Shared/Application/` |
| `EventPublisherTests` (only with `events.outbox` = `none`) | `Infrastructure/Messaging/` |
| `ContextCommandFilterTests` | `Api/Filters/` |
| `ConsoleCommandRunnerTests` | `Console/` |
| `JsonLineFormatterTests` | `Infrastructure/Logging/` |

## Namespaces and usings

Every file of this page, with its namespace and the `using` lines it needs (`ImplicitUsings` covers `System`, `System.Linq`, `System.Threading.Tasks` and the like). A file the table does not list follows the namespace rule in `naming.md`.

| File | Namespace | Usings |
|---|---|---|
| `test/{Name}.Tests/TestSupport/Api/ActionExecutingContextBuilder.cs` | `{Name}.Tests.TestSupport.Api` | Microsoft.AspNetCore.Http, Microsoft.AspNetCore.Mvc, Microsoft.AspNetCore.Mvc.Abstractions, Microsoft.AspNetCore.Mvc.Filters, Microsoft.AspNetCore.Routing, System.Security.Claims, {Name}.Api.Middlewares |
| `test/{Name}.Tests/TestSupport/Infrastructure/Messaging/RecordingLogger.cs` | `{Name}.Tests.TestSupport.Infrastructure.Messaging` | Microsoft.Extensions.Logging |
| `test/{Name}.Tests/TestSupport/Infrastructure/Persistence/AppDbContextBuilder.cs` | `{Name}.Tests.TestSupport.Infrastructure.Persistence` | Microsoft.EntityFrameworkCore, {Name}.Infrastructure.Persistence |
| `test/{Name}.Tests/TestSupport/Shared/Application/CommandDispatcherBuilder.cs` | `{Name}.Tests.TestSupport.Shared.Application` | Microsoft.Extensions.DependencyInjection, {Name}.Shared.Application |
| `test/{Name}.Tests/TestSupport/Shared/Application/CommandHandlerMiddlewareBuilder.cs` | `{Name}.Tests.TestSupport.Shared.Application` | Moq, {Name}.Shared.Application |
| `test/{Name}.Tests/TestSupport/Shared/Application/CommandPipelineBuilder.cs` | `{Name}.Tests.TestSupport.Shared.Application` | FluentValidation, {Name}.Shared.Application, {Name}.Shared.Domain, {Name}.Shared.Persistence, {Name}.Tests.TestSupport.Shared.Persistence |
| `test/{Name}.Tests/TestSupport/Shared/Application/FakeCommand.cs` | `{Name}.Tests.TestSupport.Shared.Application` | FluentValidation, System.Text.Json.Serialization, {Name}.Shared.Application |
| `test/{Name}.Tests/TestSupport/Shared/Application/FakeCommandHandler.cs` | `{Name}.Tests.TestSupport.Shared.Application` | {Name}.Shared.Application |
| `test/{Name}.Tests/TestSupport/Shared/Application/FakeContextCommandHandler.cs` | `{Name}.Tests.TestSupport.Shared.Application` | {Name}.Shared.Application |
| `test/{Name}.Tests/TestSupport/Shared/Application/FakeTransactionalCommandHandler.cs` | `{Name}.Tests.TestSupport.Shared.Application` | {Name}.Shared.Application |
| `test/{Name}.Tests/TestSupport/Shared/Domain/EventHandlerExecutorBuilder.cs` | `{Name}.Tests.TestSupport.Shared.Domain` | Microsoft.Extensions.DependencyInjection, {Name}.Shared.Domain |
| `test/{Name}.Tests/TestSupport/Shared/Domain/FakeAggregate.cs` | `{Name}.Tests.TestSupport.Shared.Domain` | {Name}.Shared.Domain |
| `test/{Name}.Tests/TestSupport/Shared/Domain/FakeAuditUser.cs` | `{Name}.Tests.TestSupport.Shared.Domain` | {Name}.Shared.Domain |
| `test/{Name}.Tests/TestSupport/Shared/Domain/FakeEvent.cs` | `{Name}.Tests.TestSupport.Shared.Domain` | {Name}.Shared.Domain |
| `test/{Name}.Tests/TestSupport/Shared/Domain/RecordingEventConsumer.cs` | `{Name}.Tests.TestSupport.Shared.Domain` | {Name}.Shared.Domain |
| `test/{Name}.Tests/TestSupport/Shared/Domain/RecordingEventHandler.cs` | `{Name}.Tests.TestSupport.Shared.Domain` | {Name}.Shared.Domain |
| `test/{Name}.Tests/TestSupport/Shared/Persistence/UnitOfWorkBuilder.cs` | `{Name}.Tests.TestSupport.Shared.Persistence` | Moq, {Name}.Shared.Persistence |
