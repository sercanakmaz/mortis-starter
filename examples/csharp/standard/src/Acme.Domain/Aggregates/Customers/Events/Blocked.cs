using Acme.Shared.Domain;

namespace Acme.Domain.Aggregates.Customers.Events;

public record Blocked(string Id) : EventBase;
