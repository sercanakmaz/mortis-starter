using System.Text.Json.Serialization;
using Boilerplate.Domain.Aggregates.Products;
using Boilerplate.Shared.Application;
using Boilerplate.Shared.Exceptions;
using FluentValidation;

namespace Boilerplate.Application.Products;

public record RenameProductCommand : ICommand<ProductDto>, IContextCommand, ITransactionalCommand
{
    [JsonIgnore] public CommandContext Context { get; set; }

    public Guid Id { get; set; }
    public string Name { get; set; }
}

public class RenameProductCommandValidator : AbstractValidator<RenameProductCommand>
{
    public RenameProductCommandValidator()
    {
        RuleFor(command => command.Id).NotEmpty();
        RuleFor(command => command.Name).NotEmpty().MaximumLength(ProductConstants.NameMaxLength);
    }
}

public class RenameProductCommandHandler(
    IProductRepository productRepository,
    CommandPipeline<RenameProductCommand> commandPipeline) : BaseCommandHandler<RenameProductCommand, ProductDto>(commandPipeline)
{
    protected override async Task<CommandResult<ProductDto>> OnHandle(RenameProductCommand command)
    {
        var product = await productRepository.FindByIdTrackingAsync(command.Id)
            ?? throw new NotFoundException(nameof(Product));

        product.Rename(command.Context.Language ?? Languages.Default, command.Name);
        product.SetUpdatedAudit(command.Context.User);

        await productRepository.UpdateAsync(product);

        return CommandResult.FromContent(ProductDto.Map(product, command.Context.Language));
    }
}
