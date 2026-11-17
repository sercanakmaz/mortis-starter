using Acme.Domain.Aggregates.Products;
using Acme.Shared.Application;
using Acme.Shared.Exceptions;

namespace Acme.Application.Products;

public record GetProductCommand(Guid Id) : ICommand<ProductDto>;

public class GetProductCommandHandler(
    IProductRepository productRepository,
    CommandPipeline<GetProductCommand> commandPipeline) : BaseCommandHandler<GetProductCommand, ProductDto>(commandPipeline)
{
    protected override async Task<CommandResult<ProductDto>> OnHandle(GetProductCommand command)
    {
        var product = await productRepository.FindByIdAsync(command.Id)
            ?? throw new NotFoundException(nameof(Product));

        return CommandResult.FromContent(ProductDto.Map(product));
    }
}
