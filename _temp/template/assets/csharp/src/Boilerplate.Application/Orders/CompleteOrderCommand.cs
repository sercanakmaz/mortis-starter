using System.Text.Json.Serialization;
using Boilerplate.Domain.Aggregates.Orders;
using Boilerplate.Shared.Application;

namespace Boilerplate.Application.Orders;

public record CompleteOrderCommand(Guid Id) : ICommand<OrderDto>, IUserCommand, ITransactionalCommand
{
    [JsonIgnore] public string UserId { get; set; }
    [JsonIgnore] public string UserEmail { get; set; }
}

public class CompleteOrderCommandHandler(
    IOrderService orderService,
    CommandPipeline<CompleteOrderCommand> commandPipeline) : BaseCommandHandler<CompleteOrderCommand, OrderDto>(commandPipeline)
{
    protected override async Task<CommandResult<OrderDto>> OnHandle(CompleteOrderCommand command)
    {
        var order = await orderService.CompleteAsync(command.ToAuditUser(), command.Id);

        return CommandResult.FromContent(OrderDto.Map(order));
    }
}
