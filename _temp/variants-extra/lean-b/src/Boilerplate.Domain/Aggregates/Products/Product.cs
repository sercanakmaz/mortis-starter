using Boilerplate.Domain.Aggregates.Products.Events;
using Boilerplate.Shared.Exceptions;
using Boilerplate.Shared.Domain;
using System.Diagnostics;

namespace Boilerplate.Domain.Aggregates.Products;

public class Product : AggregateRoot<int>
{
    public string Sku { get; protected set; }
    public string Name { get; protected set; }
    public int Stock { get; protected set; }
    public int? CategoryId { get; protected set; }

    [DebuggerStepThrough]
    protected Product() { }

    public Product(string sku, string name, int stock, AuditUser user = null, int? categoryId = null) : base(user)
    {
        if (stock < 0)
        {
            throw new InvalidParameterException(nameof(stock), "it cannot be negative");
        }

        Sku = sku;
        Name = name;
        Stock = stock;
        CategoryId = categoryId;
    }

    public void MarkCreated()
    {
        if (Id == 0)
        {
            throw new InvalidOperationException("A product is marked created only after the database has assigned its id.");
        }

        RaiseEvent(new ProductCreated(Id.ToString(), Sku));
    }

    public void IncreaseStock(int quantity)
    {
        if (quantity <= 0)
        {
            throw new InvalidParameterException(nameof(quantity), "it must be greater than zero");
        }

        Stock += quantity;

        RaiseEvent(new ProductStockIncreased(Id.ToString(), Sku, quantity, Stock));
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

        RaiseEvent(new ProductStockDecreased(Id.ToString(), Sku, quantity, Stock));
    }
}
