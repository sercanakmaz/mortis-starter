using Boilerplate.Shared.Application;
using Boilerplate.Shared.Domain;
using Boilerplate.Tests.TestSupport.Shared.Domain;
using Boilerplate.Domain.Aggregates.Products;

namespace Boilerplate.Tests.TestSupport.Domain.Products;

public class FakeProduct : Product
{
    private FakeProduct(Guid id, string sku, int stock) : base(id, sku, LocalizedText.Of(Languages.Default, "Default Product"), stock, FakeAuditUser.Create())
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
