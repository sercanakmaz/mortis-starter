using Boilerplate.Tests.TestSupport.Shared.Application;
using Boilerplate.Console.Scenarios;
using Boilerplate.Shared.Exceptions;

namespace Boilerplate.Tests.Console;

public class CommandScenarioTests
{
    [Fact]
    public void Name_Should_BeTheCommandNameWithoutItsSuffix()
    {
        var commandScenario = new CommandScenario<FakeCommand, string>(CommandDispatcherBuilder.Create().WithCommandHandler(new FakeCommandHandler(CommandPipelineBuilder<FakeCommand>.Create().Build(), [])).Build());

        Assert.Equal("Fake", commandScenario.Name);
    }

    [Fact]
    public async Task RunAsync_WithJson_Should_HandleTheCommandReadFromItIgnoringTheCaseOfTheNames()
    {
        var handled = new List<FakeCommand>();
        var commandScenario = new CommandScenario<FakeCommand, string>(CommandDispatcherBuilder.Create().WithCommandHandler(new FakeCommandHandler(CommandPipelineBuilder<FakeCommand>.Create().Build(), handled)).Build());

        await commandScenario.RunAsync(["{\"NAME\":\"from json\"}"]);

        Assert.Equal("from json", Assert.Single(handled).Name);
    }

    [Fact]
    public async Task RunAsync_WithEmptyArguments_Should_HandleTheCommandWithItsDefaults()
    {
        var handled = new List<FakeCommand>();
        var commandScenario = new CommandScenario<FakeCommand, string>(CommandDispatcherBuilder.Create().WithCommandHandler(new FakeCommandHandler(CommandPipelineBuilder<FakeCommand>.Create().Build(), handled)).Build());

        await commandScenario.RunAsync([""]);

        Assert.Equal("Default name", Assert.Single(handled).Name);
    }

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

    [Fact]
    public async Task RunAsync_WithBrokenJson_Should_ThrowInvalidParameterForArgumentsAndNotHandle()
    {
        var handled = new List<FakeCommand>();
        var commandScenario = new CommandScenario<FakeCommand, string>(CommandDispatcherBuilder.Create().WithCommandHandler(new FakeCommandHandler(CommandPipelineBuilder<FakeCommand>.Create().Build(), handled)).Build());

        var exception = await Assert.ThrowsAsync<InvalidParameterException>(() => commandScenario.RunAsync(["{not json"]));

        Assert.StartsWith("arguments is invalid", exception.Message);
        Assert.Empty(handled);
    }

    [Fact]
    public async Task RunAsync_WithJsonNull_Should_ThrowInvalidParameterForArgumentsAndNotHandle()
    {
        var handled = new List<FakeCommand>();
        var commandScenario = new CommandScenario<FakeCommand, string>(CommandDispatcherBuilder.Create().WithCommandHandler(new FakeCommandHandler(CommandPipelineBuilder<FakeCommand>.Create().Build(), handled)).Build());

        await Assert.ThrowsAsync<InvalidParameterException>(() => commandScenario.RunAsync(["null"]));

        Assert.Empty(handled);
    }

    [Fact]
    public async Task RunAsync_WithAUserCommand_Should_RunItAsTheConsoleUser()
    {
        var handled = new List<FakeUserCommand>();
        var commandScenario = new CommandScenario<FakeUserCommand, string>(CommandDispatcherBuilder.Create().WithCommandHandler(new FakeUserCommandHandler(CommandPipelineBuilder<FakeUserCommand>.Create().Build(), handled)).Build());

        await commandScenario.RunAsync(["{\"userId\":\"spoofed\"}"]);

        var userCommand = Assert.Single(handled);
        Assert.Equal([ScenarioConstants.UserId, ScenarioConstants.UserEmail], [userCommand.UserId, userCommand.UserEmail]);
    }
}
