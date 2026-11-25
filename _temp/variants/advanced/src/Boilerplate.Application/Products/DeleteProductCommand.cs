using System.Text.Json.Serialization;
using Boilerplate.Domain.Aggregates.Products;
using Boilerplate.Shared.Application;
using Boilerplate.Shared.Exceptions;

namespace Boilerplate.Application.Products;

public record DeleteProductCommand(Guid Id) : ICommand<ProductDto>, IContextCommand, ITransactionalCommand
{
    [JsonIgnore] public CommandContext Context { get; set; }
}

public class DeleteProductCommandHandler(
    IProductRepository productRepository,
    CommandPipeline<DeleteProductCommand> commandPipeline) : BaseCommandHandler<DeleteProductCommand, ProductDto>(commandPipeline)
{
    protected override async Task<CommandResult<ProductDto>> OnHandle(DeleteProductCommand command)
    {
        var product = await productRepository.FindByIdTrackingAsync(command.Id)
            ?? throw new NotFoundException(nameof(Product));

        product.Delete(command.Context.User);

        await productRepository.UpdateAsync(product);

        return CommandResult.FromContent(ProductDto.Map(product, command.Context.Language));
    }
}
