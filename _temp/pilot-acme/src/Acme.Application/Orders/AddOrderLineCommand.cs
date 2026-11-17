using System.Text.Json.Serialization;
using Acme.Domain.Aggregates.Orders;
using Acme.Shared.Application;
using Acme.Shared.Exceptions;
using FluentValidation;

namespace Acme.Application.Orders;

public record AddOrderLineCommand : ICommand<OrderDto>, IUserCommand, ITransactionalCommand
{
    [JsonIgnore] public string UserId { get; set; }

    [JsonIgnore] public string UserEmail { get; set; }

    public Guid Id { get; set; }

    public OrderLineModel Line { get; set; }
}

public class AddOrderLineCommandValidator : AbstractValidator<AddOrderLineCommand>
{
    public AddOrderLineCommandValidator()
    {
        RuleFor(command => command.Id).NotEmpty();
        RuleFor(command => command.Line).NotNull();

        When(command => command.Line is not null, () =>
        {
            RuleFor(command => command.Line.Sku).NotEmpty().MaximumLength(OrderConstants.SkuMaxLength);
            RuleFor(command => command.Line.Price).GreaterThanOrEqualTo(0);
            RuleFor(command => command.Line.Currency).NotEmpty().Length(OrderConstants.CurrencyLength);
            RuleFor(command => command.Line.Quantity).GreaterThan(0);
        });
    }
}

public class AddOrderLineCommandHandler(
    IOrderRepository orderRepository,
    CommandPipeline<AddOrderLineCommand> commandPipeline) : BaseCommandHandler<AddOrderLineCommand, OrderDto>(commandPipeline)
{
    protected override async Task<CommandResult<OrderDto>> OnHandle(AddOrderLineCommand command)
    {
        var order = await orderRepository.FindByIdTrackingAsync(command.Id)
            ?? throw new NotFoundException(nameof(Order));

        order.AddLine(command.Line.Sku, new Money(command.Line.Price, command.Line.Currency), command.Line.Quantity);
        order.SetUpdatedAudit(command.ToAuditUser());

        await orderRepository.UpdateAsync(order);

        return CommandResult.FromContent(OrderDto.Map(order));
    }
}
