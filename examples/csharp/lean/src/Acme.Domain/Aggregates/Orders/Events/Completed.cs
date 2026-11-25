using Acme.Shared.Domain;

namespace Acme.Domain.Aggregates.Orders.Events;

public record Completed(string Id, string OrderNumber, string CustomerId) : EventBase, IQueueEvent;
