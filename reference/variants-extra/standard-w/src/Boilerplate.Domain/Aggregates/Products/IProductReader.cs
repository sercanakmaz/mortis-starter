using Boilerplate.Shared.Application;

namespace Boilerplate.Domain.Aggregates.Products;

public interface IProductReader
{
    Task<ProductView> FindAsync(Guid id);
    Task<PagedResult<ProductView>> ListAsync(ProductListFilter filter);
}
