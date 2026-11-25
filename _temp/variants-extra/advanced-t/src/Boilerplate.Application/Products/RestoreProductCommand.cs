using System.Text.Json.Serialization;
using Boilerplate.Domain.Aggregates.Products;
using Boilerplate.Shared.Application;
using Boilerplate.Shared.Exceptions;

namespace Boilerplate.Application.Products;

public record RestoreProductCommand(Guid Id) : ICommand<ProductDto>, IContextCommand, ITransactionalCommand
{
    [JsonIgnore] public CommandContext Context { get; set; }
}

public class RestoreProductCommandHandler(
    IProductRepository productRepository,
    CommandPipeline<RestoreProductCommand> commandPipeline) : BaseCommandHandler<RestoreProductCommand, ProductDto>(commandPipeline)
{
    protected override async Task<CommandResult<ProductDto>> OnHandle(RestoreProductCommand command)
    {
        var product = await productRepository.FindByIdIncludingDeletedTrackingAsync(command.Id)
            ?? throw new NotFoundException(nameof(Product));

        product.Restore(command.Context.User);

        await productRepository.UpdateAsync(product);

        return CommandResult.FromContent(ProductDto.Map(product, command.Context.Language));
    }
}
