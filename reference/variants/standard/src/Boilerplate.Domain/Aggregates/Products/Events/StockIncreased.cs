using Boilerplate.Shared.Domain;

namespace Boilerplate.Domain.Aggregates.Products.Events;

public record StockIncreased(string Id, string Sku, int Quantity, int Stock) : EventBase, IQueueEvent;
