using Boilerplate.Shared.Domain;

namespace Boilerplate.Domain.Aggregates.Customers.Events;

public record CustomerBlocked(string Id) : EventBase;
