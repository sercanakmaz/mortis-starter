using Boilerplate.Tests.TestSupport.Shared.Application;
using Boilerplate.Shared.Exceptions;
using Boilerplate.Console;

namespace Boilerplate.Tests.Console;

public class ConsoleCommandRunnerTests
{
    [Fact]
    public async Task RunAsync_WithJson_Should_FindTheCommandByItsNameIgnoringCaseAndReadItsFieldsIgnoringCase()
    {
        var handled = new List<FakeCommand>();
        var consoleCommandRunner = new ConsoleCommandRunner(CommandDispatcherBuilder.Create().WithCommandHandler(new FakeCommandHandler(CommandPipelineBuilder<FakeCommand>.Create().Build(), handled)).Build(), typeof(FakeCommand).Assembly);

        await consoleCommandRunner.RunAsync("fake", "{\"NAME\":\"from json\"}");

        Assert.Equal("from json", Assert.Single(handled).Name);
    }

    [Fact]
    public async Task RunAsync_WithoutJson_Should_RunTheCommandWithItsDefaults()
    {
        var handled = new List<FakeCommand>();
        var consoleCommandRunner = new ConsoleCommandRunner(CommandDispatcherBuilder.Create().WithCommandHandler(new FakeCommandHandler(CommandPipelineBuilder<FakeCommand>.Create().Build(), handled)).Build(), typeof(FakeCommand).Assembly);

        await consoleCommandRunner.RunAsync("Fake", null);

        Assert.Equal("Default name", Assert.Single(handled).Name);
    }

    [Fact]
    public async Task RunAsync_WithAFileReference_Should_ReadTheJsonFromTheFile()
    {
        var handled = new List<FakeCommand>();
        var path = Path.GetTempFileName();
        await File.WriteAllTextAsync(path, "{\"name\":\"from file\"}");
        var consoleCommandRunner = new ConsoleCommandRunner(CommandDispatcherBuilder.Create().WithCommandHandler(new FakeCommandHandler(CommandPipelineBuilder<FakeCommand>.Create().Build(), handled)).Build(), typeof(FakeCommand).Assembly);

        await consoleCommandRunner.RunAsync("Fake", ConsoleConstants.FilePrefix + path);

        File.Delete(path);
        Assert.Equal("from file", Assert.Single(handled).Name);
    }

    [Fact]
    public async Task RunAsync_WithAnUnknownName_Should_ThrowInvalidParameterForTheCommand()
    {
        var consoleCommandRunner = new ConsoleCommandRunner(CommandDispatcherBuilder.Create().Build(), typeof(FakeCommand).Assembly);

        var exception = await Assert.ThrowsAsync<InvalidParameterException>(() => consoleCommandRunner.RunAsync("NoSuch", null));

        Assert.StartsWith("command is invalid", exception.Message);
    }

    [Fact]
    public async Task RunAsync_WithBrokenJson_Should_ThrowInvalidParameterForTheArgumentsAndNotHandle()
    {
        var handled = new List<FakeCommand>();
        var consoleCommandRunner = new ConsoleCommandRunner(CommandDispatcherBuilder.Create().WithCommandHandler(new FakeCommandHandler(CommandPipelineBuilder<FakeCommand>.Create().Build(), handled)).Build(), typeof(FakeCommand).Assembly);

        var exception = await Assert.ThrowsAsync<InvalidParameterException>(() => consoleCommandRunner.RunAsync("Fake", "{not json"));

        Assert.StartsWith("arguments is invalid", exception.Message);
        Assert.Empty(handled);
    }

    [Fact]
    public async Task RunAsync_WithAContextCommand_Should_RunItAsTheConsoleUserWhateverTheJsonSays()
    {
        var handled = new List<FakeContextCommand>();
        var consoleCommandRunner = new ConsoleCommandRunner(CommandDispatcherBuilder.Create().WithCommandHandler(new FakeContextCommandHandler(CommandPipelineBuilder<FakeContextCommand>.Create().Build(), handled)).Build(), typeof(FakeContextCommand).Assembly);

        await consoleCommandRunner.RunAsync("FakeContext", "{\"context\":{\"user\":{\"id\":\"spoofed\"}}}");

        var contextCommand = Assert.Single(handled);
        Assert.Equal([ConsoleConstants.UserId, ConsoleConstants.UserEmail], [contextCommand.Context.User.Id, contextCommand.Context.User.Email]);
        Assert.NotEqual(Guid.Empty, contextCommand.Context.CorrelationId);
    }
}
