using System.Text.Json.Serialization;
using Acme.Domain.Aggregates.Orders;
using Acme.Shared.Application;
using Acme.Shared.Exceptions;
using FluentValidation;

namespace Acme.Application.Orders;

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
    IOrderRepository orderRepository,
    CommandPipeline<CancelOrderLineCommand> commandPipeline) : BaseCommandHandler<CancelOrderLineCommand, OrderDto>(commandPipeline)
{
    protected override async Task<CommandResult<OrderDto>> OnHandle(CancelOrderLineCommand command)
    {
        var order = await orderRepository.FindByIdTrackingAsync(command.Id)
            ?? throw new NotFoundException(nameof(Order));

        order.CancelLine(command.LineId, command.Reason);
        order.SetUpdatedAudit(command.ToAuditUser());

        await orderRepository.UpdateAsync(order);

        return CommandResult.FromContent(OrderDto.Map(order));
    }
}
