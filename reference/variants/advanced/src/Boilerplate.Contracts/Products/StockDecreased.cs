using Boilerplate.Shared.Domain;

namespace Boilerplate.Contracts.Products;

public record StockDecreased(string Id, string Sku, int Quantity, int Stock) : EventBase, IQueueEvent;
