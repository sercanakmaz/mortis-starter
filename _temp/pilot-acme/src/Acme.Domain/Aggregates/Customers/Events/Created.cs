using Acme.Shared.Domain;

namespace Acme.Domain.Aggregates.Customers.Events;

public record Created(string Id, string Email) : EventBase;
