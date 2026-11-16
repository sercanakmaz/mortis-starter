using Boilerplate.Shared.Exceptions;
using Boilerplate.Shared.Domain;

namespace Boilerplate.Domain.Aggregates.Products;

public interface IProductService
{
    Task<Product> CreateAsync(AuditUser user, Guid id, string sku, string name, int stock);
    Task<Product> GetAsync(Guid id);
    Task<Product> IncreaseStockAsync(AuditUser user, string sku, int quantity);
    Task<Product> DecreaseStockAsync(AuditUser user, string sku, int quantity);
}

public class ProductService(IProductRepository productRepository) : IProductService
{
    public async Task<Product> CreateAsync(AuditUser user, Guid id, string sku, string name, int stock)
    {
        if (await productRepository.IsSkuExistsAsync(sku))
        {
            throw new DuplicateException(nameof(Product));
        }

        var product = new Product(id, sku, name, stock, user);

        await productRepository.CreateAsync(product);

        return product;
    }

    public async Task<Product> GetAsync(Guid id)
    {
        return await productRepository.FindByIdAsync(id)
            ?? throw new NotFoundException(nameof(Product));
    }

    public async Task<Product> IncreaseStockAsync(AuditUser user, string sku, int quantity)
    {
        var product = await productRepository.FindBySkuTrackingAsync(sku)
            ?? throw new NotFoundException(nameof(Product));

        product.IncreaseStock(quantity);
        product.SetUpdatedAudit(user);

        await productRepository.UpdateAsync(product);

        return product;
    }

    public async Task<Product> DecreaseStockAsync(AuditUser user, string sku, int quantity)
    {
        var product = await productRepository.FindBySkuTrackingAsync(sku)
            ?? throw new NotFoundException(nameof(Product));

        product.DecreaseStock(quantity);
        product.SetUpdatedAudit(user);

        await productRepository.UpdateAsync(product);

        return product;
    }
}
