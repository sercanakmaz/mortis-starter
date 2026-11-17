using Acme.Shared.Domain;

namespace Acme.Domain.Aggregates.Orders.Events;

public record LineCancelled(string Id, string LineId, string Sku, int Quantity, string Reason) : EventBase, IQueueEvent;
