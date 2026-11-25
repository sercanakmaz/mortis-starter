using Boilerplate.Domain.Aggregates.Products;
using Boilerplate.Shared.Persistence;
using Microsoft.EntityFrameworkCore;
using Boilerplate.Shared.Domain;

namespace Boilerplate.Infrastructure.Persistence.Products;

public class ProductRepository(AppDbContext context, IUnitOfWork unitOfWork, EventContext eventContext) : EfRepository<Product, int>(context, unitOfWork, eventContext), IProductRepository
{
    public Task<Product> FindBySkuTrackingAsync(string sku)
    {
        return DbSet.FirstOrDefaultAsync(product => product.Sku == sku);
    }
}
