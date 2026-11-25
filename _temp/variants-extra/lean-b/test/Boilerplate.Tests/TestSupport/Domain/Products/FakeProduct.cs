using Boilerplate.Tests.TestSupport.Shared.Domain;
using Boilerplate.Domain.Aggregates.Products;

namespace Boilerplate.Tests.TestSupport.Domain.Products;

public class FakeProduct : Product
{
    private static int _nextId;

    private FakeProduct(int id, string sku, int stock) : base(sku, "Default Product", stock, FakeAuditUser.Create())
    {
        Id = id;
        MarkCreated();
    }

    private FakeProduct() { }

    public static FakeProduct Create(int id, string sku, int stock) => new(id, sku, stock);
    public static FakeProduct Create(string sku = "SKU-1", int stock = 10) => Create(Interlocked.Increment(ref _nextId), sku, stock);
    public static FakeProduct CreateEmpty() => new();

    public FakeProduct WithoutEvents()
    {
        ClearEvents();
        return this;
    }
}
