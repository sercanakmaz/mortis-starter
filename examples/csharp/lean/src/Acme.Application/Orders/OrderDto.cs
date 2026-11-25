using Acme.Domain.Aggregates.Orders;

namespace Acme.Application.Orders;

public record OrderDto
{
    public Guid Id { get; set; }

    public string OrderNumber { get; set; }

    public Guid CustomerId { get; set; }

    public string Status { get; set; }

    public decimal TotalAmount { get; set; }

    public List<OrderLineDto> Lines { get; set; }

    public static OrderDto Map(Order order) => new()
    {
        Id = order.Id,
        OrderNumber = order.OrderNumber,
        CustomerId = order.CustomerId,
        Status = order.Status.ToString(),
        TotalAmount = order.TotalAmount,
        Lines = order.Lines.Select(orderLine => OrderLineDto.Map(orderLine)).ToList()
    };
}

public record OrderLineDto
{
    public Guid Id { get; set; }

    public string Sku { get; set; }

    public decimal Price { get; set; }

    public string Currency { get; set; }

    public int Quantity { get; set; }

    public bool IsCancelled { get; set; }

    public string CancellationReason { get; set; }

    public static OrderLineDto Map(OrderLine orderLine) => new()
    {
        Id = orderLine.Id,
        Sku = orderLine.Sku,
        Price = orderLine.Price.Amount,
        Currency = orderLine.Price.Currency,
        Quantity = orderLine.Quantity,
        IsCancelled = orderLine.IsCancelled,
        CancellationReason = orderLine.CancellationReason
    };
}
