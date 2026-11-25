using Acme.Shared.Domain;

namespace Acme.Domain.Aggregates.Customers.Events;

public record Changed(string Id) : EventBase;
