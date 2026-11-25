using System.Text.Json.Serialization;
using Boilerplate.Domain.Aggregates.Products;
using Boilerplate.Shared.Application;
using Boilerplate.Shared.Domain;
using FluentValidation;

namespace Boilerplate.Application.Products;

public record CreateProductCommand : ICommand<ProductDto>, IContextCommand, ITransactionalCommand
{
    [JsonIgnore] public CommandContext Context { get; set; }

    public Guid Id { get; set; } = Guid.CreateVersion7();
    public string Sku { get; set; }
    public string Name { get; set; }
    public int Stock { get; set; }
    public int? CategoryId { get; set; }
}

public class CreateProductCommandValidator : AbstractValidator<CreateProductCommand>
{
    public CreateProductCommandValidator()
    {
        RuleFor(command => command.Id).NotEmpty();
        RuleFor(command => command.Sku).NotEmpty().MaximumLength(ProductConstants.SkuMaxLength);
        RuleFor(command => command.Name).NotEmpty().MaximumLength(ProductConstants.NameMaxLength);
        RuleFor(command => command.Stock).GreaterThanOrEqualTo(0);
        RuleFor(command => command.CategoryId).GreaterThan(0).When(command => command.CategoryId.HasValue);
    }
}

public class CreateProductCommandHandler(
    IProductRepository productRepository,
    CommandPipeline<CreateProductCommand> commandPipeline) : BaseCommandHandler<CreateProductCommand, ProductDto>(commandPipeline)
{
    protected override async Task<CommandResult<ProductDto>> OnHandle(CreateProductCommand command)
    {
        var product = new Product(command.Id, command.Sku, LocalizedText.Of(command.Context.Language ?? Languages.Default, command.Name), command.Stock, command.Context.User, command.CategoryId);

        await productRepository.CreateAsync(product);

        return CommandResult.FromContent(ProductDto.Map(product, command.Context.Language));
    }
}
