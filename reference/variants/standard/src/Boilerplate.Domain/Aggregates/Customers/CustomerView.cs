namespace Boilerplate.Domain.Aggregates.Customers;

public record CustomerView(Guid Id, string Name, string Email, bool IsBlocked, int CompletedOrderCount, string Note);
