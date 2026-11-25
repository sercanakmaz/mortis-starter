using System.Text.Json.Serialization;
using Boilerplate.Domain.Aggregates.Orders;
using Boilerplate.Shared.Application;
using Boilerplate.Shared.Exceptions;

namespace Boilerplate.Application.Orders;

public record CancelOrderWhenAllLinesCancelledCommand(int Id) : ICommand<OrderDto>, IContextCommand, ITransactionalCommand
{
    [JsonIgnore] public CommandContext Context { get; set; }
}

public class CancelOrderWhenAllLinesCancelledCommandHandler(
    IOrderRepository orderRepository,
    CommandPipeline<CancelOrderWhenAllLinesCancelledCommand> commandPipeline) : BaseCommandHandler<CancelOrderWhenAllLinesCancelledCommand, OrderDto>(commandPipeline)
{
    protected override async Task<CommandResult<OrderDto>> OnHandle(CancelOrderWhenAllLinesCancelledCommand command)
    {
        var order = await orderRepository.FindByIdTrackingAsync(command.Id)
            ?? throw new NotFoundException(nameof(Order));

        if (order.CancelWhenAllLinesCancelled())
        {
            order.SetUpdatedAudit(command.Context.User);

            await orderRepository.UpdateAsync(order);
        }

        return CommandResult.FromContent(OrderDto.Map(order));
    }
}
