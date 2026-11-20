using Boilerplate.Shared.Domain;

namespace Boilerplate.Domain.Aggregates.Orders.Events;

public record LineAdded(string Id, string LineId, string Sku, int Quantity) : EventBase, IQueueEvent;
