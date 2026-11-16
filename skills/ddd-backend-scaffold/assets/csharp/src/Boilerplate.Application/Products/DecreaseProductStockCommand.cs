using System.Text.Json.Serialization;
using Boilerplate.Domain.Aggregates.Products;
using Boilerplate.Shared.Application;
using FluentValidation;

namespace Boilerplate.Application.Products;

public record DecreaseProductStockCommand : ICommand<ProductDto>, IUserCommand, ITransactionalCommand
{
    [JsonIgnore] public string UserId { get; set; }
    [JsonIgnore] public string UserEmail { get; set; }

    public string Sku { get; set; }
    public int Quantity { get; set; }
}

public class DecreaseProductStockCommandValidator : AbstractValidator<DecreaseProductStockCommand>
{
    public DecreaseProductStockCommandValidator()
    {
        RuleFor(command => command.Sku).NotEmpty().MaximumLength(ProductConstants.SkuMaxLength);
        RuleFor(command => command.Quantity).GreaterThan(0);
    }
}

public class DecreaseProductStockCommandHandler(
    IProductService productService,
    CommandPipeline<DecreaseProductStockCommand> commandPipeline) : BaseCommandHandler<DecreaseProductStockCommand, ProductDto>(commandPipeline)
{
    protected override async Task<CommandResult<ProductDto>> OnHandle(DecreaseProductStockCommand command)
    {
        var product = await productService.DecreaseStockAsync(command.ToAuditUser(), command.Sku, command.Quantity);

        return CommandResult.FromContent(ProductDto.Map(product));
    }
}
