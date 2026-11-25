using Acme.Shared.Domain;

namespace Acme.Domain.Aggregates.Orders.Events;

public record Created(string Id, string OrderNumber, string CustomerId) : EventBase;
