using Microsoft.EntityFrameworkCore;
using Acme.Domain.Aggregates.Products;
using Acme.Shared.Domain;
using Acme.Shared.Persistence;

namespace Acme.Infrastructure.Persistence.Products;

public class ProductRepository(AppDbContext context, IUnitOfWork unitOfWork, EventContext eventContext) : EfRepository<Product, Guid>(context, unitOfWork, eventContext), IProductRepository
{
    public Task<Product> FindBySkuTrackingAsync(string sku)
    {
        return DbSet.FirstOrDefaultAsync(product => product.Sku == sku);
    }
}
