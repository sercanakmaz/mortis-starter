using Boilerplate.Shared.Persistence;

namespace Boilerplate.Domain.Aggregates.Products;

public interface IProductRepository : IRepository<Product, Guid>
{
    Task<Product> FindBySkuTrackingAsync(string sku);
    Task<Product> FindByIdIncludingDeletedTrackingAsync(Guid id);
}
