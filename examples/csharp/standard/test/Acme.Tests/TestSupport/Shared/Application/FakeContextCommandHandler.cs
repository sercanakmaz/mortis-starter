using Acme.Shared.Application;

namespace Acme.Tests.TestSupport.Shared.Application;

public class FakeContextCommandHandler(CommandPipeline<FakeContextCommand> commandPipeline, List<FakeContextCommand> handled) : BaseCommandHandler<FakeContextCommand, string>(commandPipeline)
{
    protected override Task<CommandResult<string>> OnHandle(FakeContextCommand command)
    {
        handled.Add(command);

        return Task.FromResult(CommandResult.FromContent(command.Name));
    }
}
