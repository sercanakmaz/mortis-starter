using Acme.Shared.Domain;

namespace Acme.Domain.Aggregates.Products.Events;

public record StockDecreased(string Id, string Sku, int Quantity, int Stock) : EventBase, IQueueEvent;
