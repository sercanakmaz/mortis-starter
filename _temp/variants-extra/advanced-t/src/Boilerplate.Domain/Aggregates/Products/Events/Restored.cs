using Boilerplate.Shared.Domain;

namespace Boilerplate.Domain.Aggregates.Products.Events;

public record Restored(string Id, string Sku) : EventBase;
