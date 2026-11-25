using Boilerplate.Shared.Persistence;

namespace Boilerplate.Domain.Aggregates.Products;

public interface IProductRepository : IRepository<Product, int>
{
    Task<Product> FindBySkuTrackingAsync(string sku);
}
