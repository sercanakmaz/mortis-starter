using Boilerplate.Shared.Domain;

namespace Boilerplate.Domain.Aggregates.Customers.Events;

public record CustomerCreated(string Id, string Email) : EventBase;
