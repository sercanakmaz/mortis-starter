using System.Text.Json.Serialization;
using Acme.Domain.Aggregates.Orders;
using Acme.Shared.Application;
using Acme.Shared.Exceptions;

namespace Acme.Application.Orders;

public record CancelOrderWhenAllLinesCancelledCommand(Guid Id) : ICommand<OrderDto>, IUserCommand, ITransactionalCommand
{
    [JsonIgnore] public string UserId { get; set; }

    [JsonIgnore] public string UserEmail { get; set; }
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
            order.SetUpdatedAudit(command.ToAuditUser());

            await orderRepository.UpdateAsync(order);
        }

        return CommandResult.FromContent(OrderDto.Map(order));
    }
}
