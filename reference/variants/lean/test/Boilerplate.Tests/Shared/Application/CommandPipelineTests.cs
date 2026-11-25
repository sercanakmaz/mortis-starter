using Boilerplate.Shared.Domain;
using Boilerplate.Tests.TestSupport.Shared.Domain;
using Boilerplate.Tests.TestSupport.Shared.Application;
using Boilerplate.Tests.TestSupport.Shared.Persistence;
using Boilerplate.Shared.Application;
using Boilerplate.Shared.Persistence;
using Boilerplate.Shared.Exceptions;

namespace Boilerplate.Tests.Shared.Application;

public class CommandPipelineTests
{
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

    [Fact]
    public async Task ExecuteAsync_WithAContextCommandWithoutAContext_Should_GiveItACorrelationId()
    {
        var commandPipeline = CommandPipelineBuilder<FakeContextCommand>.Create().Build();
        var fakeContextCommand = new FakeContextCommand();

        await commandPipeline.ExecuteAsync(fakeContextCommand, command => Task.FromResult(CommandResult.FromContent(command.Name)));

        Assert.NotEqual(Guid.Empty, fakeContextCommand.Context.CorrelationId);
    }

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

    [Fact]
    public async Task ExecuteAsync_AfterTheCommand_Should_StopStampingWithItsContext()
    {
        var eventContext = new EventContext();
        var commandPipeline = CommandPipelineBuilder<FakeContextCommand>.Create().WithEventContext(eventContext).Build();

        await commandPipeline.ExecuteAsync(new FakeContextCommand { Context = new CommandContext { CorrelationId = Guid.CreateVersion7() } }, command => Task.FromResult(CommandResult.FromContent(command.Name)));
        eventContext.AddRaised(new FakeEvent("after"));

        Assert.Equal(Guid.Empty, eventContext.TakeRaised().CorrelationId);
    }
}
