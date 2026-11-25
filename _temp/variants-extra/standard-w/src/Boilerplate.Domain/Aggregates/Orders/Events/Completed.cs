using Boilerplate.Shared.Domain;

namespace Boilerplate.Domain.Aggregates.Orders.Events;

public record Completed(string Id, string OrderNumber, string CustomerId) : EventBase, IQueueEvent;
