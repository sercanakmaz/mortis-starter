using Boilerplate.Domain.Aggregates.Products.Events;
using Boilerplate.Shared.Exceptions;
using Boilerplate.Shared.Domain;
using System.Diagnostics;

namespace Boilerplate.Domain.Aggregates.Products;

public class Product : AggregateRoot<Guid>, IMultiTenant
{
    public Guid TenantId { get; private set; }
    public string Sku { get; protected set; }
    public LocalizedText Name { get; protected set; }
    public int Stock { get; protected set; }
    public int? CategoryId { get; protected set; }

    [DebuggerStepThrough]
    protected Product() { }

    public Product(Guid id, string sku, LocalizedText name, int stock, AuditUser user = null, int? categoryId = null) : base(user)
    {
        if (stock < 0)
        {
            throw new InvalidParameterException(nameof(stock), "it cannot be negative");
        }

        Id = id;
        Sku = sku;
        Name = name;
        Stock = stock;
        CategoryId = categoryId;

        RaiseEvent(new Created(Id.ToString(), Sku));
    }

    public void Rename(string language, string name)
    {
        Name = Name.With(language, name);
    }

    public void IncreaseStock(int quantity)
    {
        if (quantity <= 0)
        {
            throw new InvalidParameterException(nameof(quantity), "it must be greater than zero");
        }

        Stock += quantity;

        RaiseEvent(new StockIncreased(Id.ToString(), Sku, quantity, Stock));
    }

    public void DecreaseStock(int quantity)
    {
        if (quantity <= 0)
        {
            throw new InvalidParameterException(nameof(quantity), "it must be greater than zero");
        }

        if (Stock < quantity)
        {
            throw new BusinessRuleException(ProductConstants.InsufficientStockCode, "The product does not have enough stock.");
        }

        Stock -= quantity;

        RaiseEvent(new StockDecreased(Id.ToString(), Sku, quantity, Stock));
    }
}
