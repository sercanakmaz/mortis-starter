using Boilerplate.Domain.Aggregates.Customers;

namespace Boilerplate.Application.Customers;

public record CustomerDto
{
    public Guid Id { get; set; }
    public string Name { get; set; }
    public string Email { get; set; }
    public bool IsBlocked { get; set; }
    public int CompletedOrderCount { get; set; }

    public static CustomerDto Map(Customer customer) => new()
    {
        Id = customer.Id,
        Name = customer.Name,
        Email = customer.Email,
        IsBlocked = customer.IsBlocked,
        CompletedOrderCount = customer.CompletedOrderCount
    };
}
