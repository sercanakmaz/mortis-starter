using System.Text.Json.Serialization;
using FluentValidation;
using Boilerplate.Domain.Aggregates.Products;
using Boilerplate.Shared.Application;
using Boilerplate.Shared.Exceptions;

namespace Boilerplate.Application.Products;

public record GetProductQuery : IQuery<ProductDto>
{
    [JsonIgnore] public CommandContext Context { get; set; }

    public Guid Id { get; set; }
}

public class GetProductQueryHandler(IProductReader productReader, ICacheService cacheService, IEnumerable<IValidator<GetProductQuery>> validators) : BaseQueryHandler<GetProductQuery, ProductDto>(validators)
{
    protected override async Task<ProductDto> OnHandle(GetProductQuery query)
    {
        var tenantId = query.Context?.TenantId;
        var language = query.Context?.Language;

        return await cacheService.GetOrCreateAsync(ProductCacheKeys.ById(tenantId, query.Id, language), [ProductCacheKeys.Tag(tenantId, query.Id.ToString())], async _ =>
        {
            var productView = await productReader.FindAsync(query.Id) ?? throw new NotFoundException(nameof(Product));

            return ProductDto.From(productView, language);
        });
    }
}
