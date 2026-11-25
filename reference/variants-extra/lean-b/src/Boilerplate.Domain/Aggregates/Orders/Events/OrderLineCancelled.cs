using Boilerplate.Shared.Domain;

namespace Boilerplate.Domain.Aggregates.Orders.Events;

public record OrderLineCancelled(string Id, string LineId, string Sku, int Quantity, string Reason) : EventBase, IQueueEvent;
