using Boilerplate.Domain.Aggregates.Products;

namespace Boilerplate.Application.Products;

public record ProductDto
{
    public int Id { get; set; }
    public string Sku { get; set; }
    public string Name { get; set; }
    public int Stock { get; set; }
    public int? CategoryId { get; set; }

    public static ProductDto Map(Product product) => new()
    {
        Id = product.Id,
        Sku = product.Sku,
        Name = product.Name,
        Stock = product.Stock,
        CategoryId = product.CategoryId
    };
}
