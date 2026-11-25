using Boilerplate.Domain.Aggregates.Orders.Events;
using Boilerplate.Shared.Exceptions;
using Boilerplate.Shared.Domain;
using System.Diagnostics;

namespace Boilerplate.Domain.Aggregates.Orders;

public class Order : AggregateRoot<int>
{
    private readonly List<OrderLine> _lines = [];

    public string OrderNumber { get; protected set; }
    public int CustomerId { get; protected set; }
    public OrderStatus Status { get; protected set; }
    public IReadOnlyCollection<OrderLine> Lines => _lines.AsReadOnly();
    public decimal TotalAmount => _lines.Where(line => !line.IsCancelled).Sum(line => line.Price.Amount * line.Quantity);

    [DebuggerStepThrough]
    protected Order() { }

    public Order(string orderNumber, int customerId, AuditUser user = null) : base(user)
    {
        OrderNumber = orderNumber;
        CustomerId = customerId;
        Status = OrderStatus.Open;
    }

    public void MarkCreated()
    {
        if (Id == 0)
        {
            throw new InvalidOperationException("A order is marked created only after the database has assigned its id.");
        }

        RaiseEvent(new OrderCreated(Id.ToString(), OrderNumber, CustomerId.ToString()));
    }

    public OrderLine AddLine(string sku, Money price, int quantity)
    {
        EnsureOpen();

        if (quantity <= 0)
        {
            throw new InvalidParameterException(nameof(quantity), "it must be greater than zero");
        }

        if (_lines.Any(line => !line.IsCancelled && line.Sku == sku))
        {
            throw new DuplicateException(nameof(OrderLine));
        }

        if (_lines.Count > 0 && _lines[0].Price.Currency != price.Currency)
        {
            throw new BusinessRuleException(OrderConstants.MixedCurrencyCode, "Every line of an order uses the same currency.");
        }

        var orderLine = new OrderLine(Guid.CreateVersion7(), Id, sku, price, quantity);

        _lines.Add(orderLine);

        RaiseEvent(new OrderLineAdded(Id.ToString(), orderLine.Id.ToString(), sku, quantity));

        return orderLine;
    }

    public void CancelLine(Guid lineId, string reason)
    {
        EnsureOpen();

        var orderLine = _lines.FirstOrDefault(line => line.Id == lineId)
            ?? throw new NotFoundException(nameof(OrderLine));

        orderLine.Cancel(reason);

        RaiseEvent(new OrderLineCancelled(Id.ToString(), lineId.ToString(), orderLine.Sku, orderLine.Quantity, reason));
    }

    public bool CancelWhenAllLinesCancelled()
    {
        if (Status != OrderStatus.Open || _lines.Any(line => !line.IsCancelled))
        {
            return false;
        }

        Status = OrderStatus.Cancelled;

        RaiseEvent(new OrderCancelled(Id.ToString(), OrderNumber, CustomerId.ToString()));

        return true;
    }

    public void Complete()
    {
        EnsureOpen();

        if (_lines.All(line => line.IsCancelled))
        {
            throw new BusinessRuleException(OrderConstants.NothingToFulfilCode, "An order without an active line cannot be fulfilled.");
        }

        Status = OrderStatus.Completed;

        RaiseEvent(new OrderCompleted(Id.ToString(), OrderNumber, CustomerId.ToString()));
    }

    private void EnsureOpen()
    {
        if (Status != OrderStatus.Open)
        {
            throw new BusinessRuleException(OrderConstants.NotOpenCode, "The order is not open.");
        }
    }
}
