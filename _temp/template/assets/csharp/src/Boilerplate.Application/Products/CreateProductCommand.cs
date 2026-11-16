using System.Text.Json.Serialization;
using Boilerplate.Domain.Aggregates.Products;
using Boilerplate.Shared.Application;
using FluentValidation;

namespace Boilerplate.Application.Products;

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
    IProductService productService,
    CommandPipeline<CreateProductCommand> commandPipeline) : BaseCommandHandler<CreateProductCommand, ProductDto>(commandPipeline)
{
    protected override async Task<CommandResult<ProductDto>> OnHandle(CreateProductCommand command)
    {
        var product = await productService.CreateAsync(command.ToAuditUser(), command.Id, command.Sku, command.Name, command.Stock);

        return CommandResult.FromContent(ProductDto.Map(product));
    }
}
