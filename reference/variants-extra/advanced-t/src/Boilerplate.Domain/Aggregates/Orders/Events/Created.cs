using Boilerplate.Shared.Domain;

namespace Boilerplate.Domain.Aggregates.Orders.Events;

public record Created(string Id, string OrderNumber, string CustomerId) : EventBase;
