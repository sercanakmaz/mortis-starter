using Acme.Shared.Domain;

namespace Acme.Domain.Aggregates.Orders.Events;

public record LineAdded(string Id, string LineId, string Sku, int Quantity) : EventBase, IQueueEvent;
