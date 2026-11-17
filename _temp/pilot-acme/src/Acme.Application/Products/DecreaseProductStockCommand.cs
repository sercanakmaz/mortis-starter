using System.Text.Json.Serialization;
using Acme.Domain.Aggregates.Products;
using Acme.Shared.Application;
using Acme.Shared.Exceptions;
using FluentValidation;

namespace Acme.Application.Products;

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
    IProductRepository productRepository,
    CommandPipeline<DecreaseProductStockCommand> commandPipeline) : BaseCommandHandler<DecreaseProductStockCommand, ProductDto>(commandPipeline)
{
    protected override async Task<CommandResult<ProductDto>> OnHandle(DecreaseProductStockCommand command)
    {
        var product = await productRepository.FindBySkuTrackingAsync(command.Sku)
            ?? throw new NotFoundException(nameof(Product));

        product.DecreaseStock(command.Quantity);
        product.SetUpdatedAudit(command.ToAuditUser());

        await productRepository.UpdateAsync(product);

        return CommandResult.FromContent(ProductDto.Map(product));
    }
}
