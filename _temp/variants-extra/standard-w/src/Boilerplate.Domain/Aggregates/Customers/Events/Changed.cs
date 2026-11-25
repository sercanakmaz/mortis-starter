using Boilerplate.Shared.Domain;

namespace Boilerplate.Domain.Aggregates.Customers.Events;

public record Changed(string Id) : EventBase;
