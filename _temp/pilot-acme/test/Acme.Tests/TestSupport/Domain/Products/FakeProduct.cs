using Acme.Domain.Aggregates.Products;
using Acme.Tests.TestSupport.Shared.Domain;

namespace Acme.Tests.TestSupport.Domain.Products;

public class FakeProduct : Product
{
    private FakeProduct(Guid id, string sku, int stock) : base(id, sku, "Default Product", stock, FakeAuditUser.Create())
    {
    }

    private FakeProduct() { }

    public static FakeProduct Create(Guid id, string sku, int stock) => new(id, sku, stock);

    public static FakeProduct Create(string sku = "SKU-1", int stock = 10) => Create(Guid.CreateVersion7(), sku, stock);

    public static FakeProduct CreateEmpty() => new();

    public FakeProduct WithoutEvents()
    {
        ClearEvents();
        return this;
    }
}
