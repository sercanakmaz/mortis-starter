using System.Text.Json.Serialization;
using Boilerplate.Domain.Aggregates.Orders;
using Boilerplate.Shared.Application;
using Boilerplate.Shared.Exceptions;
using FluentValidation;

namespace Boilerplate.Application.Orders;

public record CancelOrderLineCommand : ICommand<OrderDto>, IContextCommand, ITransactionalCommand
{
    [JsonIgnore] public CommandContext Context { get; set; }

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
        order.SetUpdatedAudit(command.Context.User);

        await orderRepository.UpdateAsync(order);

        return CommandResult.FromContent(OrderDto.Map(order));
    }
}
