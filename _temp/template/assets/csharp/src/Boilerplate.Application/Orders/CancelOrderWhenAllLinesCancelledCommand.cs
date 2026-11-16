using System.Text.Json.Serialization;
using Boilerplate.Domain.Aggregates.Orders;
using Boilerplate.Shared.Application;

namespace Boilerplate.Application.Orders;

public record CancelOrderWhenAllLinesCancelledCommand(Guid Id) : ICommand<OrderDto>, IUserCommand, ITransactionalCommand
{
    [JsonIgnore] public string UserId { get; set; }
    [JsonIgnore] public string UserEmail { get; set; }
}

public class CancelOrderWhenAllLinesCancelledCommandHandler(
    IOrderService orderService,
    CommandPipeline<CancelOrderWhenAllLinesCancelledCommand> commandPipeline) : BaseCommandHandler<CancelOrderWhenAllLinesCancelledCommand, OrderDto>(commandPipeline)
{
    protected override async Task<CommandResult<OrderDto>> OnHandle(CancelOrderWhenAllLinesCancelledCommand command)
    {
        var order = await orderService.CancelWhenAllLinesCancelledAsync(command.ToAuditUser(), command.Id);

        return CommandResult.FromContent(OrderDto.Map(order));
    }
}
