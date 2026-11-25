using System.Text.Json.Serialization;
using FluentValidation;
using Boilerplate.Domain.Aggregates.Products;
using Boilerplate.Shared.Application;

namespace Boilerplate.Application.Products;

public record ListProductsQuery : IQuery<PagedResult<ProductDto>>
{
    [JsonIgnore] public CommandContext Context { get; set; }

    public int Page { get; set; } = 1;
    public int Size { get; set; } = PageRequest.DefaultSize;
    public ProductSort Sort { get; set; }
    public string NameContains { get; set; }
    public int? CategoryId { get; set; }
}

public class ListProductsQueryValidator : AbstractValidator<ListProductsQuery>
{
    public ListProductsQueryValidator()
    {
        RuleFor(query => query.Page).GreaterThan(0);
        RuleFor(query => query.Size).InclusiveBetween(1, PageRequest.MaxSize);
        RuleFor(query => query.Sort).IsInEnum();
        RuleFor(query => query.NameContains).MaximumLength(ProductConstants.NameMaxLength);
    }
}

public class ListProductsQueryHandler(IProductReader productReader, IEnumerable<IValidator<ListProductsQuery>> validators) : BaseQueryHandler<ListProductsQuery, PagedResult<ProductDto>>(validators)
{
    protected override async Task<PagedResult<ProductDto>> OnHandle(ListProductsQuery query)
    {
        var page = await productReader.ListAsync(new ProductListFilter(query.Page, query.Size, query.Sort, query.NameContains, query.CategoryId));

        return new PagedResult<ProductDto>(page.Items.Select(ProductDto.From).ToList(), page.Page, page.Size, page.Total);
    }
}
