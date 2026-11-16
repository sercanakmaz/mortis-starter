using System.Text.Json.Serialization;
using Boilerplate.Domain.Aggregates.Orders;
using Boilerplate.Shared.Application;
using FluentValidation;

namespace Boilerplate.Application.Orders;

public record CreateOrderCommand : ICommand<OrderDto>, IUserCommand, ITransactionalCommand
{
    [JsonIgnore] public string UserId { get; set; }
    [JsonIgnore] public string UserEmail { get; set; }

    public Guid Id { get; set; } = Guid.CreateVersion7();
    public string OrderNumber { get; set; }
    public Guid CustomerId { get; set; }
    public List<OrderLineModel> Lines { get; set; }
}

public class CreateOrderCommandValidator : AbstractValidator<CreateOrderCommand>
{
    public CreateOrderCommandValidator()
    {
        RuleFor(command => command.Id).NotEmpty();
        RuleFor(command => command.OrderNumber).NotEmpty().MaximumLength(OrderConstants.OrderNumberMaxLength);
        RuleFor(command => command.CustomerId).NotEmpty();
        RuleFor(command => command.Lines).NotEmpty();

        RuleForEach(command => command.Lines).ChildRules(line =>
        {
            line.RuleFor(model => model.Sku).NotEmpty().MaximumLength(OrderConstants.SkuMaxLength);
            line.RuleFor(model => model.Price).GreaterThanOrEqualTo(0);
            line.RuleFor(model => model.Currency).NotEmpty().Length(OrderConstants.CurrencyLength);
            line.RuleFor(model => model.Quantity).GreaterThan(0);
        });
    }
}

public class CreateOrderCommandHandler(
    IOrderService orderService,
    CommandPipeline<CreateOrderCommand> commandPipeline) : BaseCommandHandler<CreateOrderCommand, OrderDto>(commandPipeline)
{
    protected override async Task<CommandResult<OrderDto>> OnHandle(CreateOrderCommand command)
    {
        var order = await orderService.CreateAsync(command.ToAuditUser(), command.Id, command.OrderNumber, command.CustomerId, command.Lines);

        return CommandResult.FromContent(OrderDto.Map(order));
    }
}
