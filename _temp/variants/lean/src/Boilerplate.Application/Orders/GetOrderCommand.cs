using Boilerplate.Domain.Aggregates.Orders;
using Boilerplate.Shared.Application;
using Boilerplate.Shared.Exceptions;

namespace Boilerplate.Application.Orders;

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
