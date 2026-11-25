using Boilerplate.Domain.Aggregates.Products;
using Boilerplate.Shared.Application;
using Boilerplate.Shared.Exceptions;

namespace Boilerplate.Application.Products;

public record GetProductCommand(int Id) : ICommand<ProductDto>;

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
