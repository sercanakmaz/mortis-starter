using System.Text.Json.Serialization;
using Boilerplate.Domain.Aggregates.Orders;
using Boilerplate.Shared.Application;

namespace Boilerplate.Application.Orders;

public record CancelEmptyOrdersCommand : ICommand<int>, IContextCommand, ITransactionalCommand
{
    [JsonIgnore] public CommandContext Context { get; set; }
}

public class CancelEmptyOrdersCommandHandler(
    IOrderRepository orderRepository,
    CommandPipeline<CancelEmptyOrdersCommand> commandPipeline) : BaseCommandHandler<CancelEmptyOrdersCommand, int>(commandPipeline)
{
    protected override async Task<CommandResult<int>> OnHandle(CancelEmptyOrdersCommand command)
    {
        var orders = await orderRepository.FindOpenWithAllLinesCancelledTrackingAsync(OrderConstants.SweepBatchSize);

        foreach (var order in orders.Where(order => order.CancelWhenAllLinesCancelled()))
        {
            order.SetUpdatedAudit(command.Context.User);

            await orderRepository.UpdateAsync(order);
        }

        return CommandResult.FromContent(orders.Count);
    }
}
