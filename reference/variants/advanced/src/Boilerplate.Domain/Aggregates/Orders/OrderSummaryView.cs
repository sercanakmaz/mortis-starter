namespace Boilerplate.Domain.Aggregates.Orders;

public record OrderSummaryView(Guid OrderId, string OrderNumber, string CustomerName, string Status, decimal TotalAmount, int LineCount);
