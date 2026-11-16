using System.Text.Json.Serialization;
using Boilerplate.Domain.Aggregates.Products;
using Boilerplate.Shared.Application;
using FluentValidation;

namespace Boilerplate.Application.Products;

public record IncreaseProductStockCommand : ICommand<ProductDto>, IUserCommand, ITransactionalCommand
{
    [JsonIgnore] public string UserId { get; set; }
    [JsonIgnore] public string UserEmail { get; set; }

    public string Sku { get; set; }
    public int Quantity { get; set; }
}

public class IncreaseProductStockCommandValidator : AbstractValidator<IncreaseProductStockCommand>
{
    public IncreaseProductStockCommandValidator()
    {
        RuleFor(command => command.Sku).NotEmpty().MaximumLength(ProductConstants.SkuMaxLength);
        RuleFor(command => command.Quantity).GreaterThan(0);
    }
}

public class IncreaseProductStockCommandHandler(
    IProductService productService,
    CommandPipeline<IncreaseProductStockCommand> commandPipeline) : BaseCommandHandler<IncreaseProductStockCommand, ProductDto>(commandPipeline)
{
    protected override async Task<CommandResult<ProductDto>> OnHandle(IncreaseProductStockCommand command)
    {
        var product = await productService.IncreaseStockAsync(command.ToAuditUser(), command.Sku, command.Quantity);

        return CommandResult.FromContent(ProductDto.Map(product));
    }
}
