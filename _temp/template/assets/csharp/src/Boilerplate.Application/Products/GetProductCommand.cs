using Boilerplate.Domain.Aggregates.Products;
using Boilerplate.Shared.Application;

namespace Boilerplate.Application.Products;

public record GetProductCommand(Guid Id) : ICommand<ProductDto>;

public class GetProductCommandHandler(
    IProductService productService,
    CommandPipeline<GetProductCommand> commandPipeline) : BaseCommandHandler<GetProductCommand, ProductDto>(commandPipeline)
{
    protected override async Task<CommandResult<ProductDto>> OnHandle(GetProductCommand command)
    {
        var product = await productService.GetAsync(command.Id);

        return CommandResult.FromContent(ProductDto.Map(product));
    }
}
