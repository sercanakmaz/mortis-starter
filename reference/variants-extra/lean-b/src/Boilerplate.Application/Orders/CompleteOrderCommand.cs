using System.Text.Json.Serialization;
using Boilerplate.Domain.Aggregates.Orders;
using Boilerplate.Shared.Application;
using Boilerplate.Shared.Exceptions;

namespace Boilerplate.Application.Orders;

public record CompleteOrderCommand(int Id) : ICommand<OrderDto>, IContextCommand, ITransactionalCommand
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
