using Acme.Domain.Aggregates.Orders;
using Acme.Shared.Application;
using Acme.Shared.Exceptions;

namespace Acme.Application.Orders;

public record GetOrderCommand(Guid Id) : ICommand<OrderDto>;

public class GetOrderCommandHandler(
    IOrderRepository orderRepository,
    CommandPipeline<GetOrderCommand> commandPipeline) : BaseCommandHandler<GetOrderCommand, OrderDto>(commandPipeline)
{
    protected override async Task<CommandResult<OrderDto>> OnHandle(GetOrderCommand command)
    {
        var order = await orderRepository.FindByIdAsync(command.Id)
            ?? throw new NotFoundException(nameof(Order));

        return CommandResult.FromContent(OrderDto.Map(order));
    }
}
