using Boilerplate.Tests.TestSupport.Shared.Application;
using Boilerplate.Shared.Exceptions;

namespace Boilerplate.Tests.Shared.Application;

public class CommandDispatcherTests
{
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

    [Fact]
    public async Task Send_WithoutARegisteredHandler_Should_Throw()
    {
        var commandDispatcher = CommandDispatcherBuilder.Create().Build();

        await Assert.ThrowsAsync<InvalidOperationException>(() => commandDispatcher.Send(new FakeCommand()));
    }

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

    [Fact]
    public async Task Send_WithNull_Should_Throw()
    {
        var commandDispatcher = CommandDispatcherBuilder.Create().Build();

        await Assert.ThrowsAsync<ArgumentNullException>(() => commandDispatcher.Send<string>(null));
    }
}
