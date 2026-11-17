using System.Text.Json.Serialization;
using Acme.Domain.Aggregates.Products;
using Acme.Shared.Application;
using FluentValidation;

namespace Acme.Application.Products;

public record CreateProductCommand : ICommand<ProductDto>, IUserCommand, ITransactionalCommand
{
    [JsonIgnore] public string UserId { get; set; }

    [JsonIgnore] public string UserEmail { get; set; }

    public Guid Id { get; set; } = Guid.CreateVersion7();

    public string Sku { get; set; }

    public string Name { get; set; }

    public int Stock { get; set; }
}

public class CreateProductCommandValidator : AbstractValidator<CreateProductCommand>
{
    public CreateProductCommandValidator()
    {
        RuleFor(command => command.Id).NotEmpty();
        RuleFor(command => command.Sku).NotEmpty().MaximumLength(ProductConstants.SkuMaxLength);
        RuleFor(command => command.Name).NotEmpty().MaximumLength(ProductConstants.NameMaxLength);
        RuleFor(command => command.Stock).GreaterThanOrEqualTo(0);
    }
}

public class CreateProductCommandHandler(
    IProductRepository productRepository,
    CommandPipeline<CreateProductCommand> commandPipeline) : BaseCommandHandler<CreateProductCommand, ProductDto>(commandPipeline)
{
    protected override async Task<CommandResult<ProductDto>> OnHandle(CreateProductCommand command)
    {
        var product = new Product(command.Id, command.Sku, command.Name, command.Stock, command.ToAuditUser());

        await productRepository.CreateAsync(product);

        return CommandResult.FromContent(ProductDto.Map(product));
    }
}
