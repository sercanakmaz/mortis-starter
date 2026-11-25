using Boilerplate.Shared.Domain;

namespace Boilerplate.Domain.Aggregates.Customers.Events;

public record Blocked(string Id) : EventBase;
