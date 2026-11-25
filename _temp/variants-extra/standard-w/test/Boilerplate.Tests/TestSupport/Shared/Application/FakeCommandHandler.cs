using Boilerplate.Shared.Application;

namespace Boilerplate.Tests.TestSupport.Shared.Application;

public class FakeCommandHandler(CommandPipeline<FakeCommand> commandPipeline, List<FakeCommand> handled) : BaseCommandHandler<FakeCommand, string>(commandPipeline)
{
    protected override Task<CommandResult<string>> OnHandle(FakeCommand command)
    {
        handled.Add(command);

        return Task.FromResult(CommandResult.FromContent(command.Name));
    }
}
