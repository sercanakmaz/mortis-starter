using Boilerplate.Domain.Aggregates.Orders;

namespace Boilerplate.Application.Orders;

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

    public static OrderDto From(OrderView view) => new()
    {
        Id = view.Id,
        OrderNumber = view.OrderNumber,
        CustomerId = view.CustomerId,
        Status = view.Status,
        TotalAmount = view.TotalAmount,
        Lines = view.Lines.Select(OrderLineDto.From).ToList()
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

    public static OrderLineDto From(OrderLineView view) => new()
    {
        Id = view.Id,
        Sku = view.Sku,
        Price = view.Price,
        Currency = view.Currency,
        Quantity = view.Quantity,
        IsCancelled = view.IsCancelled,
        CancellationReason = view.CancellationReason
    };
}
