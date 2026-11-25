using System.Diagnostics;
using Acme.Domain.Aggregates.Products.Events;
using Acme.Shared.Domain;
using Acme.Shared.Exceptions;

namespace Acme.Domain.Aggregates.Products;

public class Product : AggregateRoot<Guid>
{
    public string Sku { get; protected set; }

    public string Name { get; protected set; }

    public int Stock { get; protected set; }

    public int? CategoryId { get; protected set; }

    [DebuggerStepThrough]
    protected Product() { }

    public Product(Guid id, string sku, string name, int stock, AuditUser user = null, int? categoryId = null) : base(user)
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
