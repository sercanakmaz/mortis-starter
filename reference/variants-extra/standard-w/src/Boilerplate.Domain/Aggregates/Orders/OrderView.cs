namespace Boilerplate.Domain.Aggregates.Orders;

public record OrderView(Guid Id, string OrderNumber, Guid CustomerId, string Status, decimal TotalAmount, List<OrderLineView> Lines);

public record OrderLineView(Guid Id, string Sku, decimal Price, string Currency, int Quantity, bool IsCancelled, string CancellationReason);
