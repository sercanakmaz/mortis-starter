# The kernel tests, C#

Every project has these tests, written exactly so. Each class lives in the folder of the type it tests (`testing.md`). Each test method is shown in order.

## Shared/Domain

```csharp
public class AggregateRootTests
```

```csharp
[Fact]
public void Constructor_Should_SetAuditFromTheUser()
{
    var fakeAggregate = FakeAggregate.Create();

    Assert.Equal(FakeAuditUser.Id, fakeAggregate.CreatedUserId);
    Assert.Equal(FakeAuditUser.Email, fakeAggregate.CreatedUserName);
    Assert.Equal(DateTimeKind.Utc, fakeAggregate.CreatedAt.Kind);
    Assert.Equal(fakeAggregate.CreatedAt, fakeAggregate.UpdatedAt);
}
```

```csharp
[Fact]
public void RaiseEvent_Should_KeepEventsInRaisedOrder()
{
    var fakeAggregate = FakeAggregate.Create().WithRaisedEvent(new FakeEvent("first")).WithRaisedEvent(new FakeEvent("second"));

    Assert.Equal(["first", "second"], fakeAggregate.DomainEvents.Cast<FakeEvent>().Select(raisedEvent => raisedEvent.Id));
}
```

```csharp
[Fact]
public void ClearEvents_Should_RemoveEveryRaisedEvent()
{
    var fakeAggregate = FakeAggregate.Create().WithRaisedEvent(new FakeEvent("first"));

    fakeAggregate.ClearEvents();

    Assert.Empty(fakeAggregate.DomainEvents);
}
```

```csharp
[Fact]
public void SetUpdatedAudit_Should_MoveUpdatedAtForwardAndKeepCreatedAt()
{
    var createdAt = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    var fakeAggregate = FakeAggregate.Create().WithUpdatedAt(createdAt);

    fakeAggregate.SetUpdatedAudit(FakeAuditUser.Create());

    Assert.True(fakeAggregate.UpdatedAt > createdAt);
    Assert.Equal("test-user-id", fakeAggregate.UpdatedUserId);
}
```

```csharp
[Fact]
public void RaiseEvent_Should_StampTheEventWithTheCurrentAuditUser()
{
    var fakeAggregate = FakeAggregate.Create().WithRaisedEvent(new FakeEvent("first"));

    var raisedEvent = Assert.Single(fakeAggregate.DomainEvents);

    Assert.Equal([FakeAuditUser.Id, FakeAuditUser.Email], [raisedEvent.UserId, raisedEvent.UserEmail]);
}
```

```csharp
[Fact]
public void SetUpdatedAudit_Should_RestampEventsRaisedBeforeIt()
{
    var fakeAggregate = FakeAggregate.Create().WithRaisedEvent(new FakeEvent("first"));
    var other = FakeAuditUser.CreateOther();

    fakeAggregate.SetUpdatedAudit(other);

    var raisedEvent = Assert.Single(fakeAggregate.DomainEvents);

    Assert.Equal([other.Id, other.Email], [raisedEvent.UserId, raisedEvent.UserEmail]);
}
```

```csharp
[Fact]
public void SetUpdatedAudit_Should_KeepTheEventIdentityWhenRestamping()
{
    var fakeAggregate = FakeAggregate.Create().WithRaisedEvent(new FakeEvent("first"));
    var eventId = Assert.Single(fakeAggregate.DomainEvents).EventId;

    fakeAggregate.SetUpdatedAudit(FakeAuditUser.CreateOther());

    Assert.Equal(eventId, Assert.Single(fakeAggregate.DomainEvents).EventId);
}
```

```csharp
[Fact]
public void Version_WithSameMicrosecond_Should_IgnoreSmallerTicks()
{
    var updatedAt = new DateTime(2026, 1, 1, 10, 0, 0, DateTimeKind.Utc);

    var storedAggregate = FakeAggregate.Create().WithUpdatedAt(updatedAt);
    var inMemoryAggregate = FakeAggregate.Create().WithUpdatedAt(updatedAt.AddTicks(7));

    Assert.Equal(storedAggregate.Version, inMemoryAggregate.Version);
}
```

```csharp
public class EventContextTests
```

```csharp
[Fact]
public void TakeRaised_Should_ReturnEventsInRaisedOrder()
{
    var firstEvent = new FakeEvent("first");
    var secondEvent = new FakeEvent("second");
    var eventContext = new EventContext();

    eventContext.AddRaised(firstEvent);
    eventContext.AddRaised(secondEvent);

    Assert.Same(firstEvent, eventContext.TakeRaised());
    Assert.Same(secondEvent, eventContext.TakeRaised());
    Assert.Null(eventContext.TakeRaised());
}
```

```csharp
[Fact]
public void Clear_Should_DropRaisedAndDispatchedEvents()
{
    var eventContext = new EventContext();
    eventContext.AddRaised(new FakeEvent("raised"));
    eventContext.AddDispatched(new FakeEvent("dispatched"));

    eventContext.Clear();

    Assert.Equal(0, eventContext.RaisedCount);
    Assert.Null(eventContext.TakeRaised());
    Assert.Null(eventContext.TakeDispatched());
}
```

```csharp
[Fact]
public void AddRaised_InsideACommand_Should_StampTheCommandContextOnTheEvent()
{
    var commandContext = new CommandContext { CorrelationId = Guid.CreateVersion7(), CausationId = Guid.CreateVersion7(), TenantId = "tenant-1", Language = "tr" };
    var eventContext = new EventContext();
    eventContext.EnterCommand(commandContext);

    eventContext.AddRaised(new FakeEvent("first"));

    var raisedEvent = eventContext.TakeRaised();
    Assert.Equal(commandContext.CorrelationId, raisedEvent.CorrelationId);
    Assert.Equal(commandContext.CausationId, raisedEvent.CausationId);
    Assert.Equal(["tenant-1", "tr"], [raisedEvent.TenantId, raisedEvent.Language]);
}
```

```csharp
[Fact]
public void LeaveCommand_Should_RestoreTheContextOfTheOuterCommand()
{
    var outerContext = new CommandContext { CorrelationId = Guid.CreateVersion7() };
    var eventContext = new EventContext();
    eventContext.EnterCommand(outerContext);
    eventContext.EnterCommand(new CommandContext { CorrelationId = Guid.CreateVersion7() });

    eventContext.LeaveCommand();
    eventContext.AddRaised(new FakeEvent("after-nested"));

    Assert.Equal(outerContext.CorrelationId, eventContext.TakeRaised().CorrelationId);
}
```

```csharp
public class EventHandlerExecutorTests
```

```csharp
[Fact]
public async Task ExecuteRaisedAsync_WithTwoHandlers_Should_AwaitThemInSequenceOrder()
{
    var handledBy = new List<string>();
    var eventContext = new EventContext();
    eventContext.AddRaised(new FakeEvent("first"));
    var eventHandlerExecutor = EventHandlerExecutorBuilder.Create()
        .WithEventContext(eventContext)
        .WithEventHandler(new RecordingEventHandler<FakeEvent>("second", 2, handledBy))
        .WithEventHandler(new RecordingEventHandler<FakeEvent>("first", 1, handledBy))
        .Build();

    await eventHandlerExecutor.ExecuteRaisedAsync();

    Assert.Equal(["first", "second"], handledBy);
}
```

```csharp
[Fact]
public async Task ExecuteRaisedAsync_WhenAHandlerRaisesAnotherEvent_Should_HandleThatEventToo()
{
    var handledBy = new List<string>();
    var eventContext = new EventContext();
    eventContext.AddRaised(new FakeEvent("first"));
    var eventHandlerExecutor = EventHandlerExecutorBuilder.Create()
        .WithEventContext(eventContext)
        .WithEventHandler(new RecordingEventHandler<FakeEvent>("source", 0, handledBy, eventContext, new FakeCascadedEvent("cascaded")))
        .WithEventHandler(new RecordingEventHandler<FakeCascadedEvent>("cascaded", 0, handledBy))
        .Build();

    await eventHandlerExecutor.ExecuteRaisedAsync();

    Assert.Equal(["source", "cascaded"], handledBy);
}
```

```csharp
[Fact]
public async Task ExecuteRaisedAsync_Should_MoveHandledEventsToDispatchedInOrder()
{
    var firstEvent = new FakeEvent("first");
    var secondEvent = new FakeEvent("second");
    var eventContext = new EventContext();
    eventContext.AddRaised(firstEvent);
    eventContext.AddRaised(secondEvent);
    var eventHandlerExecutor = EventHandlerExecutorBuilder.Create().WithEventContext(eventContext).Build();

    await eventHandlerExecutor.ExecuteRaisedAsync();

    Assert.Equal(0, eventContext.RaisedCount);
    Assert.Same(firstEvent, eventContext.TakeDispatched());
    Assert.Same(secondEvent, eventContext.TakeDispatched());
}
```

```csharp
[Fact]
public async Task ExecuteRaisedAsync_WhenAHandlerThrows_Should_Propagate()
{
    var eventContext = new EventContext();
    eventContext.AddRaised(new FakeEvent("first"));
    var eventHandlerExecutor = EventHandlerExecutorBuilder.Create()
        .WithEventContext(eventContext)
        .WithEventHandler(new ThrowingEventHandler<FakeEvent>())
        .Build();

    await Assert.ThrowsAsync<InvalidOperationException>(() => eventHandlerExecutor.ExecuteRaisedAsync());
}
```

## Shared/Application

```csharp
public class CommandPipelineTests
```

```csharp
[Fact]
public async Task ExecuteAsync_WithTransactionalCommand_Should_HandleBetweenBeginAndCommit()
{
    var calls = new List<string>();
    var commandPipeline = CommandPipelineBuilder<FakeTransactionalCommand>.Create()
        .WithUnitOfWork(UnitOfWorkBuilder.Create().WithRecordedCalls(calls).Build())
        .Build();

    var commandResult = await commandPipeline.ExecuteAsync(new FakeTransactionalCommand(), command =>
    {
        calls.Add("OnHandle");
        return Task.FromResult(CommandResult.FromContent(command.Name));
    });

    Assert.Equal([nameof(IUnitOfWork.BeginTransactionAsync), "OnHandle", nameof(IUnitOfWork.CommitTransactionAsync)], calls);
    Assert.Equal("Default name", commandResult.Content);
}
```

```csharp
[Fact]
public async Task ExecuteAsync_WhenTheHandlerThrows_Should_RollbackWithoutCommitAndRethrow()
{
    var calls = new List<string>();
    var commandPipeline = CommandPipelineBuilder<FakeTransactionalCommand>.Create()
        .WithUnitOfWork(UnitOfWorkBuilder.Create().WithRecordedCalls(calls).Build())
        .Build();

    var exception = await Assert.ThrowsAsync<BusinessRuleException>(() => commandPipeline.ExecuteAsync<string>(new FakeTransactionalCommand(), _ => throw new BusinessRuleException("fake_rule", "The fake rule was broken.")));

    Assert.Equal("fake_rule", exception.Code);
    Assert.Equal([nameof(IUnitOfWork.BeginTransactionAsync), nameof(IUnitOfWork.RollbackTransactionAsync)], calls);
}
```

```csharp
[Fact]
public async Task ExecuteAsync_WithNonTransactionalCommand_Should_NotTouchTheUnitOfWork()
{
    var calls = new List<string>();
    var commandPipeline = CommandPipelineBuilder<FakeCommand>.Create()
        .WithUnitOfWork(UnitOfWorkBuilder.Create().WithRecordedCalls(calls).Build())
        .Build();

    await commandPipeline.ExecuteAsync(new FakeCommand(), command => Task.FromResult(CommandResult.FromContent(command.Name)));

    Assert.Empty(calls);
}
```

```csharp
[Fact]
public async Task ExecuteAsync_WithInvalidCommand_Should_ThrowValidationFailedBeforeOpeningATransaction()
{
    var calls = new List<string>();
    var commandPipeline = CommandPipelineBuilder<FakeTransactionalCommand>.Create()
        .WithValidator(new FakeTransactionalCommandValidator())
        .WithUnitOfWork(UnitOfWorkBuilder.Create().WithRecordedCalls(calls).Build())
        .Build();

    var exception = await Assert.ThrowsAsync<ValidationFailedException>(() => commandPipeline.ExecuteAsync(new FakeTransactionalCommand { Name = "" }, command =>
    {
        calls.Add("OnHandle");
        return Task.FromResult(CommandResult.FromContent(command.Name));
    }));

    Assert.Equal([nameof(FakeTransactionalCommand.Name)], exception.Errors.Keys);
    Assert.Empty(calls);
}
```

```csharp
[Fact]
public async Task ExecuteAsync_Should_RunMiddlewareBeforeTheTransactionAndAfterTheCommit()
{
    var calls = new List<string>();
    var commandPipeline = CommandPipelineBuilder<FakeTransactionalCommand>.Create()
        .WithCommandHandlerMiddleware(CommandHandlerMiddlewareBuilder.Create().WithRecordedCalls(calls).Build())
        .WithUnitOfWork(UnitOfWorkBuilder.Create().WithRecordedCalls(calls).Build())
        .Build();

    await commandPipeline.ExecuteAsync(new FakeTransactionalCommand(), command => Task.FromResult(CommandResult.FromContent(command.Name)));

    Assert.Equal([nameof(ICommandHandlerMiddleware.Before), nameof(IUnitOfWork.BeginTransactionAsync), nameof(IUnitOfWork.CommitTransactionAsync), nameof(ICommandHandlerMiddleware.After)], calls);
}
```

```csharp
[Fact]
public async Task ExecuteAsync_WithAContextCommandWithoutAContext_Should_GiveItACorrelationId()
{
    var commandPipeline = CommandPipelineBuilder<FakeContextCommand>.Create().Build();
    var fakeContextCommand = new FakeContextCommand();

    await commandPipeline.ExecuteAsync(fakeContextCommand, command => Task.FromResult(CommandResult.FromContent(command.Name)));

    Assert.NotEqual(Guid.Empty, fakeContextCommand.Context.CorrelationId);
}
```

```csharp
[Fact]
public async Task ExecuteAsync_WithAContextCommand_Should_StampEventsRaisedWhileItRuns()
{
    var eventContext = new EventContext();
    var commandContext = new CommandContext { CorrelationId = Guid.CreateVersion7() };
    var commandPipeline = CommandPipelineBuilder<FakeContextCommand>.Create().WithEventContext(eventContext).Build();

    await commandPipeline.ExecuteAsync(new FakeContextCommand { Context = commandContext }, command =>
    {
        eventContext.AddRaised(new FakeEvent("raised"));

        return Task.FromResult(CommandResult.FromContent(command.Name));
    });

    Assert.Equal(commandContext.CorrelationId, eventContext.TakeRaised().CorrelationId);
}
```

```csharp
[Fact]
public async Task ExecuteAsync_AfterTheCommand_Should_StopStampingWithItsContext()
{
    var eventContext = new EventContext();
    var commandPipeline = CommandPipelineBuilder<FakeContextCommand>.Create().WithEventContext(eventContext).Build();

    await commandPipeline.ExecuteAsync(new FakeContextCommand { Context = new CommandContext { CorrelationId = Guid.CreateVersion7() } }, command => Task.FromResult(CommandResult.FromContent(command.Name)));
    eventContext.AddRaised(new FakeEvent("after"));

    Assert.Equal(Guid.Empty, eventContext.TakeRaised().CorrelationId);
}
```

```csharp
public class CommandDispatcherTests
```

```csharp
[Fact]
public async Task Send_Should_RunTheHandlerOfTheCommandAndReturnItsContent()
{
    var handled = new List<FakeCommand>();
    var commandDispatcher = CommandDispatcherBuilder.Create()
        .WithCommandHandler(new FakeCommandHandler(CommandPipelineBuilder<FakeCommand>.Create().Build(), handled))
        .Build();

    var commandResult = await commandDispatcher.Send(new FakeCommand { Name = "sent" });

    Assert.Equal("sent", commandResult.Content);
    Assert.Equal("sent", Assert.Single(handled).Name);
}
```

```csharp
[Fact]
public async Task Send_WithTwoKindsOfCommand_Should_ReachTheHandlerOfEachKind()
{
    var handledFakes = new List<FakeCommand>();
    var handledContexts = new List<FakeContextCommand>();
    var commandDispatcher = CommandDispatcherBuilder.Create()
        .WithCommandHandler(new FakeCommandHandler(CommandPipelineBuilder<FakeCommand>.Create().Build(), handledFakes))
        .WithCommandHandler(new FakeContextCommandHandler(CommandPipelineBuilder<FakeContextCommand>.Create().Build(), handledContexts))
        .Build();

    await commandDispatcher.Send(new FakeContextCommand { Name = "user" });
    await commandDispatcher.Send(new FakeCommand { Name = "plain" });

    Assert.Equal(["plain"], handledFakes.Select(command => command.Name));
    Assert.Equal(["user"], handledContexts.Select(command => command.Name));
}
```

```csharp
[Fact]
public async Task Send_TheSameKindTwice_Should_RunTheHandlerTwice()
{
    var handled = new List<FakeCommand>();
    var commandDispatcher = CommandDispatcherBuilder.Create()
        .WithCommandHandler(new FakeCommandHandler(CommandPipelineBuilder<FakeCommand>.Create().Build(), handled))
        .Build();

    await commandDispatcher.Send(new FakeCommand { Name = "first" });
    await commandDispatcher.Send(new FakeCommand { Name = "second" });

    Assert.Equal(["first", "second"], handled.Select(command => command.Name));
}
```

```csharp
[Fact]
public async Task Send_WithoutARegisteredHandler_Should_Throw()
{
    var commandDispatcher = CommandDispatcherBuilder.Create().Build();

    await Assert.ThrowsAsync<InvalidOperationException>(() => commandDispatcher.Send(new FakeCommand()));
}
```

```csharp
[Fact]
public async Task Send_WithAnInvalidCommand_Should_FailValidationInThePipelineAndNotHandle()
{
    var handled = new List<FakeTransactionalCommand>();
    var commandPipeline = CommandPipelineBuilder<FakeTransactionalCommand>.Create().WithValidator(new FakeTransactionalCommandValidator()).Build();
    var commandDispatcher = CommandDispatcherBuilder.Create()
        .WithCommandHandler(new FakeTransactionalCommandHandler(commandPipeline, handled))
        .Build();

    await Assert.ThrowsAsync<ValidationFailedException>(() => commandDispatcher.Send(new FakeTransactionalCommand { Name = "" }));

    Assert.Empty(handled);
}
```

```csharp
[Fact]
public async Task Send_WithNull_Should_Throw()
{
    var commandDispatcher = CommandDispatcherBuilder.Create().Build();

    await Assert.ThrowsAsync<ArgumentNullException>(() => commandDispatcher.Send<string>(null));
}
```

```csharp
public class ContextCommandTests
```

```csharp
[Fact]
public void Deserialize_WithAContextInTheJson_Should_IgnoreIt()
{
    var fakeContextCommand = JsonSerializer.Deserialize<FakeContextCommand>("{\"name\":\"n\",\"context\":{\"user\":{\"id\":\"spoofed\"}},\"userId\":\"spoofed\"}", new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

    Assert.Equal("n", fakeContextCommand.Name);
    Assert.Null(fakeContextCommand.Context);
}
```

```csharp
[Fact]
public void Serialize_Should_NotWriteTheContext()
{
    var json = JsonSerializer.Serialize(new FakeContextCommand { Context = new CommandContext { User = new AuditUser("user-1", "user-1@localhost") } });

    Assert.DoesNotContain("user-1", json);
}
```

```csharp
public class CommandContextTests
```

```csharp
[Fact]
public void Complete_WithoutAContext_Should_CreateOneWithACorrelationId()
{
    var commandContext = CommandContext.Complete(null);

    Assert.NotEqual(Guid.Empty, commandContext.CorrelationId);
    Assert.Null(commandContext.User);
}
```

```csharp
[Fact]
public void Complete_WithACorrelationId_Should_KeepIt()
{
    var correlationId = Guid.CreateVersion7();

    var commandContext = CommandContext.Complete(new CommandContext { CorrelationId = correlationId });

    Assert.Equal(correlationId, commandContext.CorrelationId);
}
```

```csharp
[Fact]
public void FromEvent_Should_KeepCorrelationTenantAndLanguageAndMakeTheEventTheCause()
{
    var fakeEvent = new FakeEvent("first") { CorrelationId = Guid.CreateVersion7(), TenantId = "tenant-1", Language = "tr" };

    var commandContext = CommandContext.FromEvent(fakeEvent, FakeAuditUser.Create());

    Assert.Equal(fakeEvent.CorrelationId, commandContext.CorrelationId);
    Assert.Equal(fakeEvent.EventId, commandContext.CausationId);
    Assert.Equal(["tenant-1", "tr"], [commandContext.TenantId, commandContext.Language]);
    Assert.Equal(FakeAuditUser.Id, commandContext.User.Id);
}
```

## Infrastructure/Messaging

```csharp
public class AsyncEventHandlerDispatcherTests
```

```csharp
[Fact]
public async Task Publish_WithTwoHandlers_Should_RunEachInItsOwnTransactionInSequenceOrder()
{
    var calls = new List<string>();
    var asyncEventHandlerDispatcher = AsyncEventHandlerDispatcherBuilder.Create()
        .WithUnitOfWork(UnitOfWorkBuilder.Create().WithRecordedCalls(calls).Build())
        .WithEventHandler(new RecordingAsyncEventHandler<FakeQueueEvent>("second", 2, calls))
        .WithEventHandler(new RecordingAsyncEventHandler<FakeQueueEvent>("first", 1, calls))
        .Build();

    await asyncEventHandlerDispatcher.Publish(new FakeQueueEvent("event"));

    Assert.Equal(["BeginTransactionAsync", "first", "CommitTransactionAsync", "BeginTransactionAsync", "second", "CommitTransactionAsync"], calls);
}
```

```csharp
[Fact]
public async Task Publish_WhenAHandlerThrows_Should_RollBackLogAndStillRunTheNextHandler()
{
    var calls = new List<string>();
    var asyncEventHandlerDispatcherBuilder = AsyncEventHandlerDispatcherBuilder.Create()
        .WithUnitOfWork(UnitOfWorkBuilder.Create().WithRecordedCalls(calls).Build())
        .WithEventHandler(new RecordingAsyncEventHandler<FakeQueueEvent>("failing", 1, calls, 1, new InvalidOperationException("boom")))
        .WithEventHandler(new RecordingAsyncEventHandler<FakeQueueEvent>("healthy", 2, calls));
    var asyncEventHandlerDispatcher = asyncEventHandlerDispatcherBuilder.Build();

    await asyncEventHandlerDispatcher.Publish(new FakeQueueEvent("event"));

    Assert.Equal(["BeginTransactionAsync", "failing", "RollbackTransactionAsync", "BeginTransactionAsync", "healthy", "CommitTransactionAsync"], calls);
    var entry = Assert.Single(asyncEventHandlerDispatcherBuilder.BuildLogger().Entries);
    Assert.Equal(LogLevel.Error, entry.Level);
    Assert.IsType<InvalidOperationException>(entry.Exception);
}
```

```csharp
[Fact]
public async Task Publish_WhenTheHandlerHitsAConcurrencyConflict_Should_RetryInAFreshTransaction()
{
    var calls = new List<string>();
    var asyncEventHandlerDispatcherBuilder = AsyncEventHandlerDispatcherBuilder.Create()
        .WithUnitOfWork(UnitOfWorkBuilder.Create().WithRecordedCalls(calls).Build())
        .WithEventHandler(new RecordingAsyncEventHandler<FakeQueueEvent>("contended", 0, calls, 2, new DbUpdateConcurrencyException("stale")));
    var asyncEventHandlerDispatcher = asyncEventHandlerDispatcherBuilder.Build();

    await asyncEventHandlerDispatcher.Publish(new FakeQueueEvent("event"));

    Assert.Equal(
        ["BeginTransactionAsync", "contended", "RollbackTransactionAsync", "BeginTransactionAsync", "contended", "RollbackTransactionAsync", "BeginTransactionAsync", "contended", "CommitTransactionAsync"],
        calls);
    Assert.Empty(asyncEventHandlerDispatcherBuilder.BuildLogger().Entries);
}
```

```csharp
[Fact]
public async Task Publish_WhenTheConflictNeverEnds_Should_GiveUpAfterTheMaximumAttemptsAndLog()
{
    var calls = new List<string>();
    var asyncEventHandlerDispatcherBuilder = AsyncEventHandlerDispatcherBuilder.Create()
        .WithUnitOfWork(UnitOfWorkBuilder.Create().WithRecordedCalls(calls).Build())
        .WithEventHandler(new RecordingAsyncEventHandler<FakeQueueEvent>("contended", 0, calls, int.MaxValue, new DbUpdateConcurrencyException("stale")));
    var asyncEventHandlerDispatcher = asyncEventHandlerDispatcherBuilder.Build();

    await asyncEventHandlerDispatcher.Publish(new FakeQueueEvent("event"));

    Assert.Equal(MessagingConstants.MaxAttempts, calls.Count(call => call == "contended"));
    Assert.Equal(MessagingConstants.MaxAttempts, calls.Count(call => call == "RollbackTransactionAsync"));
    Assert.Single(asyncEventHandlerDispatcherBuilder.BuildLogger().Entries);
}
```

```csharp
[Fact]
public async Task Publish_WithoutHandlersForTheEvent_Should_NotOpenATransaction()
{
    var calls = new List<string>();
    var asyncEventHandlerDispatcher = AsyncEventHandlerDispatcherBuilder.Create()
        .WithUnitOfWork(UnitOfWorkBuilder.Create().WithRecordedCalls(calls).Build())
        .Build();

    await asyncEventHandlerDispatcher.Publish(new FakeQueueEvent("event"));

    Assert.Empty(calls);
}
```

```csharp
[Fact]
public async Task Publish_WithAnEventThatIsNotAQueueEvent_Should_DoNothing()
{
    var calls = new List<string>();
    var asyncEventHandlerDispatcher = AsyncEventHandlerDispatcherBuilder.Create()
        .WithUnitOfWork(UnitOfWorkBuilder.Create().WithRecordedCalls(calls).Build())
        .WithEventHandler(new RecordingAsyncEventHandler<FakeQueueEvent>("handler", 0, calls))
        .Build();

    await asyncEventHandlerDispatcher.Publish(new FakeEvent("event"));

    Assert.Empty(calls);
}
```

## Api/Filters

```csharp
public class ContextCommandFilterTests
```

```csharp
[Fact]
public async Task OnActionExecutionAsync_WithAnAuthenticatedUser_Should_FillTheContextFromTheClaims()
{
    var fakeContextCommand = new FakeContextCommand();
    var context = ActionExecutingContextBuilder.Create().WithUser("user-1", "user-1@localhost").WithArgument("command", fakeContextCommand).Build();

    await new ContextCommandFilter().OnActionExecutionAsync(context, ActionExecutingContextBuilder.NextOf(context));

    Assert.Equal(["user-1", "user-1@localhost"], [fakeContextCommand.Context.User.Id, fakeContextCommand.Context.User.Email]);
}
```

```csharp
[Fact]
public async Task OnActionExecutionAsync_Should_TakeTheCorrelationIdOfTheRequest()
{
    var correlationId = Guid.CreateVersion7();
    var fakeContextCommand = new FakeContextCommand();
    var context = ActionExecutingContextBuilder.Create().WithCorrelationId(correlationId).WithArgument("command", fakeContextCommand).Build();

    await new ContextCommandFilter().OnActionExecutionAsync(context, ActionExecutingContextBuilder.NextOf(context));

    Assert.Equal(correlationId, fakeContextCommand.Context.CorrelationId);
}
```

```csharp
[Fact]
public async Task OnActionExecutionAsync_WithoutAUser_Should_ReplaceWhatTheClientSent()
{
    var fakeContextCommand = new FakeContextCommand { Context = new CommandContext { User = new AuditUser("spoofed", "spoofed@localhost") } };
    var context = ActionExecutingContextBuilder.Create().WithArgument("command", fakeContextCommand).Build();

    await new ContextCommandFilter().OnActionExecutionAsync(context, ActionExecutingContextBuilder.NextOf(context));

    Assert.Equal([null, null], [fakeContextCommand.Context.User.Id, fakeContextCommand.Context.User.Email]);
}
```

```csharp
[Fact]
public async Task OnActionExecutionAsync_Should_LeaveOtherArgumentsUntouchedAndRunTheAction()
{
    var context = ActionExecutingContextBuilder.Create().WithUser("user-1", "user-1@localhost").WithArgument("id", Guid.Empty).WithArgument("command", new FakeCommand()).Build();
    var ran = false;

    await new ContextCommandFilter().OnActionExecutionAsync(context, () =>
    {
        ran = true;
        return ActionExecutingContextBuilder.NextOf(context)();
    });

    Assert.True(ran);
    Assert.Equal(Guid.Empty, context.ActionArguments["id"]);
}
```

## Console

```csharp
public class CommandScenarioTests
```

```csharp
[Fact]
public void Name_Should_BeTheCommandNameWithoutItsSuffix()
{
    var commandScenario = new CommandScenario<FakeCommand, string>(CommandDispatcherBuilder.Create().WithCommandHandler(new FakeCommandHandler(CommandPipelineBuilder<FakeCommand>.Create().Build(), [])).Build());

    Assert.Equal("Fake", commandScenario.Name);
}
```

```csharp
[Fact]
public async Task RunAsync_WithJson_Should_HandleTheCommandReadFromItIgnoringTheCaseOfTheNames()
{
    var handled = new List<FakeCommand>();
    var commandScenario = new CommandScenario<FakeCommand, string>(CommandDispatcherBuilder.Create().WithCommandHandler(new FakeCommandHandler(CommandPipelineBuilder<FakeCommand>.Create().Build(), handled)).Build());

    await commandScenario.RunAsync(["{\"NAME\":\"from json\"}"]);

    Assert.Equal("from json", Assert.Single(handled).Name);
}
```

```csharp
[Fact]
public async Task RunAsync_WithEmptyArguments_Should_HandleTheCommandWithItsDefaults()
{
    var handled = new List<FakeCommand>();
    var commandScenario = new CommandScenario<FakeCommand, string>(CommandDispatcherBuilder.Create().WithCommandHandler(new FakeCommandHandler(CommandPipelineBuilder<FakeCommand>.Create().Build(), handled)).Build());

    await commandScenario.RunAsync([""]);

    Assert.Equal("Default name", Assert.Single(handled).Name);
}
```

```csharp
[Fact]
public async Task RunAsync_WithAFileReference_Should_ReadTheJsonFromTheFile()
{
    var handled = new List<FakeCommand>();
    var path = Path.GetTempFileName();
    await File.WriteAllTextAsync(path, "{\"name\":\"from file\"}");
    var commandScenario = new CommandScenario<FakeCommand, string>(CommandDispatcherBuilder.Create().WithCommandHandler(new FakeCommandHandler(CommandPipelineBuilder<FakeCommand>.Create().Build(), handled)).Build());

    await commandScenario.RunAsync([ScenarioConstants.FilePrefix + path]);

    File.Delete(path);
    Assert.Equal("from file", Assert.Single(handled).Name);
}
```

```csharp
[Fact]
public async Task RunAsync_WithBrokenJson_Should_ThrowInvalidParameterForArgumentsAndNotHandle()
{
    var handled = new List<FakeCommand>();
    var commandScenario = new CommandScenario<FakeCommand, string>(CommandDispatcherBuilder.Create().WithCommandHandler(new FakeCommandHandler(CommandPipelineBuilder<FakeCommand>.Create().Build(), handled)).Build());

    var exception = await Assert.ThrowsAsync<InvalidParameterException>(() => commandScenario.RunAsync(["{not json"]));

    Assert.StartsWith("arguments is invalid", exception.Message);
    Assert.Empty(handled);
}
```

```csharp
[Fact]
public async Task RunAsync_WithJsonNull_Should_ThrowInvalidParameterForArgumentsAndNotHandle()
{
    var handled = new List<FakeCommand>();
    var commandScenario = new CommandScenario<FakeCommand, string>(CommandDispatcherBuilder.Create().WithCommandHandler(new FakeCommandHandler(CommandPipelineBuilder<FakeCommand>.Create().Build(), handled)).Build());

    await Assert.ThrowsAsync<InvalidParameterException>(() => commandScenario.RunAsync(["null"]));

    Assert.Empty(handled);
}
```

```csharp
[Fact]
public async Task RunAsync_WithAContextCommand_Should_RunItAsTheConsoleUserWithTheCorrelationIdOfTheRun()
{
    var handled = new List<FakeContextCommand>();
    var commandScenario = new CommandScenario<FakeContextCommand, string>(CommandDispatcherBuilder.Create().WithCommandHandler(new FakeContextCommandHandler(CommandPipelineBuilder<FakeContextCommand>.Create().Build(), handled)).Build());

    await commandScenario.RunAsync(["{\"context\":{\"user\":{\"id\":\"spoofed\"}}}"]);

    var contextCommand = Assert.Single(handled);
    Assert.Equal([ScenarioConstants.UserId, ScenarioConstants.UserEmail], [contextCommand.Context.User.Id, contextCommand.Context.User.Email]);
    Assert.Equal(ScenarioConstants.RunCorrelationId, contextCommand.Context.CorrelationId);
}
```

## Namespaces and usings

Every file of this page, with its namespace and the `using` lines it needs (`ImplicitUsings` covers `System`, `System.Linq`, `System.Threading.Tasks` and the like). A file the table does not list follows the namespace rule in `naming.md`.

| File | Namespace | Usings |
|---|---|---|
| `test/{Name}.Tests/Api/Filters/ContextCommandFilterTests.cs` | `{Name}.Tests.Api.Filters` | {Name}.Api.Filters, {Name}.Shared.Application, {Name}.Shared.Domain, {Name}.Tests.TestSupport.Api, {Name}.Tests.TestSupport.Shared.Application |
| `test/{Name}.Tests/Console/CommandScenarioTests.cs` | `{Name}.Tests.Console` | {Name}.Console.Scenarios, {Name}.Shared.Exceptions, {Name}.Tests.TestSupport.Shared.Application |
| `test/{Name}.Tests/Infrastructure/Messaging/AsyncEventHandlerDispatcherTests.cs` | `{Name}.Tests.Infrastructure.Messaging` | Microsoft.EntityFrameworkCore, Microsoft.Extensions.DependencyInjection, Microsoft.Extensions.Logging, {Name}.Infrastructure.Messaging, {Name}.Shared.Domain, {Name}.Tests.TestSupport.Infrastructure.Messaging, {Name}.Tests.TestSupport.Shared.Domain, {Name}.Tests.TestSupport.Shared.Persistence |
| `test/{Name}.Tests/Shared/Application/CommandContextTests.cs` | `{Name}.Tests.Shared.Application` | {Name}.Shared.Application, {Name}.Tests.TestSupport.Shared.Domain |
| `test/{Name}.Tests/Shared/Application/CommandDispatcherTests.cs` | `{Name}.Tests.Shared.Application` | {Name}.Shared.Exceptions, {Name}.Tests.TestSupport.Shared.Application |
| `test/{Name}.Tests/Shared/Application/CommandPipelineTests.cs` | `{Name}.Tests.Shared.Application` | {Name}.Shared.Application, {Name}.Shared.Domain, {Name}.Shared.Exceptions, {Name}.Shared.Persistence, {Name}.Tests.TestSupport.Shared.Application, {Name}.Tests.TestSupport.Shared.Domain, {Name}.Tests.TestSupport.Shared.Persistence |
| `test/{Name}.Tests/Shared/Application/ContextCommandTests.cs` | `{Name}.Tests.Shared.Application` | System.Text.Json, {Name}.Shared.Application, {Name}.Shared.Domain, {Name}.Tests.TestSupport.Shared.Application |
| `test/{Name}.Tests/Shared/Domain/AggregateRootTests.cs` | `{Name}.Tests.Shared.Domain` | {Name}.Tests.TestSupport.Shared.Domain |
| `test/{Name}.Tests/Shared/Domain/EventContextTests.cs` | `{Name}.Tests.Shared.Domain` | {Name}.Shared.Application, {Name}.Shared.Domain, {Name}.Tests.TestSupport.Shared.Domain |
| `test/{Name}.Tests/Shared/Domain/EventHandlerExecutorTests.cs` | `{Name}.Tests.Shared.Domain` | {Name}.Shared.Domain, {Name}.Tests.TestSupport.Shared.Domain |
