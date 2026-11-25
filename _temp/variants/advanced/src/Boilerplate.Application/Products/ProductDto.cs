using Boilerplate.Domain.Aggregates.Products;
using Boilerplate.Shared.Application;

namespace Boilerplate.Application.Products;

public record ProductDto
{
    public Guid Id { get; set; }
    public string Sku { get; set; }
    public string Name { get; set; }
    public IReadOnlyDictionary<string, string> Names { get; set; }
    public int Stock { get; set; }
    public int? CategoryId { get; set; }

    public static ProductDto Map(Product product, string language) => new()
    {
        Id = product.Id,
        Sku = product.Sku,
        Name = product.Name.Get(language, Languages.Default),
        Names = product.Name.Texts,
        Stock = product.Stock,
        CategoryId = product.CategoryId
    };

    public static ProductDto From(ProductView view, string language) => new()
    {
        Id = view.Id,
        Sku = view.Sku,
        Name = view.Name.Get(language, Languages.Default),
        Names = view.Name.Texts,
        Stock = view.Stock,
        CategoryId = view.CategoryId
    };
}
