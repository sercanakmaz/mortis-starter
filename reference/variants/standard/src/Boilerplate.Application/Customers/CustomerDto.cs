using Boilerplate.Domain.Aggregates.Customers;

namespace Boilerplate.Application.Customers;

public record CustomerDto
{
    public Guid Id { get; set; }
    public string Name { get; set; }
    public string Email { get; set; }
    public bool IsBlocked { get; set; }
    public int CompletedOrderCount { get; set; }
    public string Note { get; set; }

    public static CustomerDto Map(Customer customer) => new()
    {
        Id = customer.Id,
        Name = customer.Name,
        Email = customer.Email,
        IsBlocked = customer.IsBlocked,
        CompletedOrderCount = customer.CompletedOrderCount,
        Note = customer.Note
    };

    public static CustomerDto From(CustomerView view) => new()
    {
        Id = view.Id,
        Name = view.Name,
        Email = view.Email,
        IsBlocked = view.IsBlocked,
        CompletedOrderCount = view.CompletedOrderCount,
        Note = view.Note
    };
}
