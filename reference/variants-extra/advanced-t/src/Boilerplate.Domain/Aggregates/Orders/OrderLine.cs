using Boilerplate.Shared.Exceptions;
using Boilerplate.Shared.Domain;
using System.Diagnostics;

namespace Boilerplate.Domain.Aggregates.Orders;

public class OrderLine : Entity<Guid>, IMultiTenant
{
    public string TenantId { get; private set; } = string.Empty;
    public Guid OrderId { get; private init; }
    public string Sku { get; protected set; }
    public Money Price { get; protected set; }
    public int Quantity { get; protected set; }
    public bool IsCancelled { get; protected set; }
    public string CancellationReason { get; protected set; }

    [DebuggerStepThrough]
    protected OrderLine() { }

    internal OrderLine(Guid id, Guid orderId, string sku, Money price, int quantity)
    {
        Id = id;
        OrderId = orderId;
        Sku = sku;
        Price = price;
        Quantity = quantity;
    }

    internal void Cancel(string reason)
    {
        if (IsCancelled)
        {
            throw new BusinessRuleException(OrderConstants.LineAlreadyCancelledCode, "The order line is already cancelled.");
        }

        IsCancelled = true;
        CancellationReason = reason;
    }
}
