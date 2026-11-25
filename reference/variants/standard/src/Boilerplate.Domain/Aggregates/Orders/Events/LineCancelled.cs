using Boilerplate.Shared.Domain;

namespace Boilerplate.Domain.Aggregates.Orders.Events;

public record LineCancelled(string Id, string LineId, string Sku, int Quantity, string Reason) : EventBase, IQueueEvent;
