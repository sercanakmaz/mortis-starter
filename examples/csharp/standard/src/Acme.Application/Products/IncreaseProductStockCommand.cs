using System.Text.Json.Serialization;
using Acme.Domain.Aggregates.Products;
using Acme.Shared.Application;
using Acme.Shared.Exceptions;
using FluentValidation;

namespace Acme.Application.Products;

public record IncreaseProductStockCommand : ICommand<ProductDto>, IContextCommand, ITransactionalCommand
{
    [JsonIgnore] public CommandContext Context { get; set; }

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
    IProductRepository productRepository,
    CommandPipeline<IncreaseProductStockCommand> commandPipeline) : BaseCommandHandler<IncreaseProductStockCommand, ProductDto>(commandPipeline)
{
    protected override async Task<CommandResult<ProductDto>> OnHandle(IncreaseProductStockCommand command)
    {
        var product = await productRepository.FindBySkuTrackingAsync(command.Sku)
            ?? throw new NotFoundException(nameof(Product));

        product.IncreaseStock(command.Quantity);
        product.SetUpdatedAudit(command.Context.User);

        await productRepository.UpdateAsync(product);

        return CommandResult.FromContent(ProductDto.Map(product));
    }
}
