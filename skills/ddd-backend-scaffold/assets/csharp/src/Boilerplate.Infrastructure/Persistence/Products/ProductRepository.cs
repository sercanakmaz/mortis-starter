using Boilerplate.Domain.Aggregates.Products;
using Boilerplate.Shared.Persistence;
using Microsoft.EntityFrameworkCore;
using Boilerplate.Shared.Domain;

namespace Boilerplate.Infrastructure.Persistence.Products;

public class ProductRepository(AppDbContext context, IUnitOfWork unitOfWork, EventContext eventContext) : EfRepository<Product, Guid>(context, unitOfWork, eventContext), IProductRepository
{
    public Task<bool> IsSkuExistsAsync(string sku)
    {
        return Query().AnyAsync(product => product.Sku == sku);
    }

    public Task<Product> FindBySkuTrackingAsync(string sku)
    {
        return DbSet.FirstOrDefaultAsync(product => product.Sku == sku);
    }
}
