using Boilerplate.Shared.Domain;

namespace Boilerplate.Domain.Aggregates.Orders.Events;

public record OrderCancelled(string Id, string OrderNumber, string CustomerId) : EventBase, IQueueEvent;
