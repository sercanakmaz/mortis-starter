using Boilerplate.Shared.Domain;

namespace Boilerplate.Domain.Aggregates.Products.Events;

public record ProductStockDecreased(string Id, string Sku, int Quantity, int Stock) : EventBase, IQueueEvent;
