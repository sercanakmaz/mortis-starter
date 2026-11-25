using Boilerplate.Shared.Domain;

namespace Boilerplate.Contracts.Orders;

public record LineCancelled(string Id, string LineId, string Sku, int Quantity, string Reason) : EventBase, IQueueEvent;
