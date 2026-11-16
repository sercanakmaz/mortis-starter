using Boilerplate.Domain.Aggregates.Orders;
using Boilerplate.Shared.Application;

namespace Boilerplate.Application.Orders;

public record GetOrderCommand(Guid Id) : ICommand<OrderDto>;

public class GetOrderCommandHandler(
    IOrderService orderService,
    CommandPipeline<GetOrderCommand> commandPipeline) : BaseCommandHandler<GetOrderCommand, OrderDto>(commandPipeline)
{
    protected override async Task<CommandResult<OrderDto>> OnHandle(GetOrderCommand command)
    {
        var order = await orderService.GetAsync(command.Id);

        return CommandResult.FromContent(OrderDto.Map(order));
    }
}
