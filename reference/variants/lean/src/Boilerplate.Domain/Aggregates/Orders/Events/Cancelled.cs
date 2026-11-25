using Boilerplate.Shared.Domain;

namespace Boilerplate.Domain.Aggregates.Orders.Events;

public record Cancelled(string Id, string OrderNumber, string CustomerId) : EventBase, IQueueEvent;
