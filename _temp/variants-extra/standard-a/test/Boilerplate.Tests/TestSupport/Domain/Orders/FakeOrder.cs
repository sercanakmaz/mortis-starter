using Boilerplate.Tests.TestSupport.Shared.Domain;
using Boilerplate.Domain.Aggregates.Orders;

namespace Boilerplate.Tests.TestSupport.Domain.Orders;

public class FakeOrder : Order
{
    private FakeOrder(Guid id, Guid customerId) : base(id, "ORD-DEFAULT", customerId, FakeAuditUser.Create())
    {
    }

    private FakeOrder() { }

    public static FakeOrder Create(Guid id) => new(id, Guid.CreateVersion7());
    public static FakeOrder Create() => Create(Guid.CreateVersion7());
    public static FakeOrder CreateEmpty() => new();

    public FakeOrder WithOrderNumber(string orderNumber)
    {
        OrderNumber = orderNumber;
        return this;
    }

    public FakeOrder WithCustomerId(Guid customerId)
    {
        CustomerId = customerId;
        return this;
    }

    public FakeOrder WithLine(string sku, decimal price = 10m, string currency = "EUR", int quantity = 1)
    {
        AddLine(sku, new Money(price, currency), quantity);
        return this;
    }

    public FakeOrder WithCompleted()
    {
        Complete();
        return this;
    }

    public FakeOrder WithCancelledLine(string sku)
    {
        CancelLine(Lines.First(orderLine => orderLine.Sku == sku).Id, "test");
        return this;
    }

    public FakeOrder WithCancelled()
    {
        foreach (var orderLine in Lines)
        {
            CancelLine(orderLine.Id, "test");
        }

        CancelWhenAllLinesCancelled();
        return this;
    }

    public FakeOrder WithoutEvents()
    {
        ClearEvents();
        return this;
    }
}
