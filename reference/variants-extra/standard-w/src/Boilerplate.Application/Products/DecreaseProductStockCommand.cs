using System.Text.Json.Serialization;
using Boilerplate.Domain.Aggregates.Products;
using Boilerplate.Shared.Application;
using Boilerplate.Shared.Exceptions;
using FluentValidation;

namespace Boilerplate.Application.Products;

public record DecreaseProductStockCommand : ICommand<ProductDto>, IContextCommand, ITransactionalCommand
{
    [JsonIgnore] public CommandContext Context { get; set; }

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
    IProductRepository productRepository,
    CommandPipeline<DecreaseProductStockCommand> commandPipeline) : BaseCommandHandler<DecreaseProductStockCommand, ProductDto>(commandPipeline)
{
    protected override async Task<CommandResult<ProductDto>> OnHandle(DecreaseProductStockCommand command)
    {
        var product = await productRepository.FindBySkuTrackingAsync(command.Sku)
            ?? throw new NotFoundException(nameof(Product));

        product.DecreaseStock(command.Quantity);
        product.SetUpdatedAudit(command.Context.User);

        await productRepository.UpdateAsync(product);

        return CommandResult.FromContent(ProductDto.Map(product));
    }
}
