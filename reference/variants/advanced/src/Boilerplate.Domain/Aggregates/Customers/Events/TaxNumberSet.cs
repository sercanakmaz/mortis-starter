using Boilerplate.Shared.Domain;

namespace Boilerplate.Domain.Aggregates.Customers.Events;

public record TaxNumberSet(string Id) : EventBase;
