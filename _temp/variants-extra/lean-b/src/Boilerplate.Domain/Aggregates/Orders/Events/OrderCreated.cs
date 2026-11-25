using Boilerplate.Shared.Domain;

namespace Boilerplate.Domain.Aggregates.Orders.Events;

public record OrderCreated(string Id, string OrderNumber, string CustomerId) : EventBase;
