using System.Text.Json.Serialization;
using Acme.Domain.Aggregates.Orders;
using Acme.Shared.Application;
using Acme.Shared.Exceptions;

namespace Acme.Application.Orders;

public record CompleteOrderCommand(Guid Id) : ICommand<OrderDto>, IContextCommand, ITransactionalCommand
{
    [JsonIgnore] public CommandContext Context { get; set; }
}

public class CompleteOrderCommandHandler(
    IOrderRepository orderRepository,
    CommandPipeline<CompleteOrderCommand> commandPipeline) : BaseCommandHandler<CompleteOrderCommand, OrderDto>(commandPipeline)
{
    protected override async Task<CommandResult<OrderDto>> OnHandle(CompleteOrderCommand command)
    {
        var order = await orderRepository.FindByIdTrackingAsync(command.Id)
            ?? throw new NotFoundException(nameof(Order));

        order.Complete();
        order.SetUpdatedAudit(command.Context.User);

        await orderRepository.UpdateAsync(order);

        return CommandResult.FromContent(OrderDto.Map(order));
    }
}
