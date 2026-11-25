using Acme.Shared.Application;

namespace Acme.Domain.Aggregates.Products;

public record ProductView(Guid Id, string Sku, string Name, int Stock, int? CategoryId);

public enum ProductSort
{
    SkuAscending,
    NameAscending,
    StockDescending
}

public record ProductListFilter(int Page, int Size, ProductSort Sort, string NameContains, int? CategoryId);
