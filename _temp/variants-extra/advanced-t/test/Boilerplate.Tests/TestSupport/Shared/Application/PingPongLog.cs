using System.Text.Json.Serialization;
using Boilerplate.Shared.Application;
using Boilerplate.Shared.Domain;

namespace Boilerplate.Tests.TestSupport.Shared.Application;

public record PingCommand : ICommand<int>, IContextCommand
{
    [JsonIgnore] public CommandContext Context { get; set; }
}

public record PongCommand : ICommand<int>, IContextCommand
{
    [JsonIgnore] public CommandContext Context { get; set; }
}

public record Pinged : EventBase;

public record Ponged : EventBase;

public class PingPongLog
{
    public List<string> Handled { get; } = [];
}

public class PingCommandHandler(CommandPipeline<PingCommand> commandPipeline, EventContext eventContext, EventHandlerExecutor eventHandlerExecutor, PingPongLog pingPongLog) : BaseCommandHandler<PingCommand, int>(commandPipeline)
{
    protected override async Task<CommandResult<int>> OnHandle(PingCommand command)
    {
        pingPongLog.Handled.Add(nameof(PingCommand));
        eventContext.AddRaised(new Pinged());
        await eventHandlerExecutor.ExecuteRaisedAsync();

        return CommandResult.FromContent(pingPongLog.Handled.Count);
    }
}

public class PongCommandHandler(CommandPipeline<PongCommand> commandPipeline, EventContext eventContext, EventHandlerExecutor eventHandlerExecutor, PingPongLog pingPongLog) : BaseCommandHandler<PongCommand, int>(commandPipeline)
{
    protected override async Task<CommandResult<int>> OnHandle(PongCommand command)
    {
        pingPongLog.Handled.Add(nameof(PongCommand));
        eventContext.AddRaised(new Ponged());
        await eventHandlerExecutor.ExecuteRaisedAsync();

        return CommandResult.FromContent(pingPongLog.Handled.Count);
    }
}

public class SendPongWhenPinged(ICommandDispatcher commandDispatcher) : BaseSyncEventHandler<Pinged>
{
    protected override Task OnHandle(Pinged @event) => commandDispatcher.Send(new PongCommand { Context = ContextOf(@event) });
}

public class SendPingWhenPonged(ICommandDispatcher commandDispatcher) : BaseSyncEventHandler<Ponged>
{
    protected override Task OnHandle(Ponged @event) => commandDispatcher.Send(new PingCommand { Context = ContextOf(@event) });
}
