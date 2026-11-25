using Boilerplate.Shared.Domain;

namespace Boilerplate.Contracts.Orders;

public record LineAdded(string Id, string LineId, string Sku, int Quantity) : EventBase, IQueueEvent;
