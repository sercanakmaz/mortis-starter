using System.Text.Json.Serialization;
using Boilerplate.Domain.Aggregates.Orders;
using Boilerplate.Shared.Application;
using FluentValidation;

namespace Boilerplate.Application.Orders;

public record CancelOrderLineCommand : ICommand<OrderDto>, IUserCommand, ITransactionalCommand
{
    [JsonIgnore] public string UserId { get; set; }
    [JsonIgnore] public string UserEmail { get; set; }

    public Guid Id { get; set; }
    public Guid LineId { get; set; }
    public string Reason { get; set; }
}

public class CancelOrderLineCommandValidator : AbstractValidator<CancelOrderLineCommand>
{
    public CancelOrderLineCommandValidator()
    {
        RuleFor(command => command.Id).NotEmpty();
        RuleFor(command => command.LineId).NotEmpty();
        RuleFor(command => command.Reason).NotEmpty().MaximumLength(OrderConstants.ReasonMaxLength);
    }
}

public class CancelOrderLineCommandHandler(
    IOrderService orderService,
    CommandPipeline<CancelOrderLineCommand> commandPipeline) : BaseCommandHandler<CancelOrderLineCommand, OrderDto>(commandPipeline)
{
    protected override async Task<CommandResult<OrderDto>> OnHandle(CancelOrderLineCommand command)
    {
        var order = await orderService.CancelLineAsync(command.ToAuditUser(), command.Id, command.LineId, command.Reason);

        return CommandResult.FromContent(OrderDto.Map(order));
    }
}
