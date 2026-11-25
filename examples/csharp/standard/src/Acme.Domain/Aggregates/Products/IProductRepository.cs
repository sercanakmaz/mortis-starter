using Acme.Shared.Persistence;

namespace Acme.Domain.Aggregates.Products;

public interface IProductRepository : IRepository<Product, Guid>
{
    Task<Product> FindBySkuTrackingAsync(string sku);
}
