namespace Boilerplate.Domain.Aggregates.Orders;

public record OrderLineModel(string Sku, decimal Price, string Currency, int Quantity);
