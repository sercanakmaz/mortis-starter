using Acme.Shared.Domain;

namespace Acme.Domain.Aggregates.Orders.Events;

public record Cancelled(string Id, string OrderNumber, string CustomerId) : EventBase, IQueueEvent;
