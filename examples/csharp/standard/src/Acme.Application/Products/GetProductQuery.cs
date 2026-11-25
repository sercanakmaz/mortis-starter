using System.Text.Json.Serialization;
using Acme.Domain.Aggregates.Products;
using Acme.Shared.Application;
using Acme.Shared.Exceptions;
using FluentValidation;

namespace Acme.Application.Products;

public record GetProductQuery : IQuery<ProductDto>
{
    [JsonIgnore] public CommandContext Context { get; set; }

    public Guid Id { get; set; }
}

public class GetProductQueryHandler(IProductReader productReader, IEnumerable<IValidator<GetProductQuery>> validators) : BaseQueryHandler<GetProductQuery, ProductDto>(validators)
{
    protected override async Task<ProductDto> OnHandle(GetProductQuery query)
    {
        var productView = await productReader.FindAsync(query.Id) ?? throw new NotFoundException(nameof(Product));

        return ProductDto.From(productView);
    }
}
