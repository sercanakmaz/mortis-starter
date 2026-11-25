using Boilerplate.Shared.Domain;

namespace Boilerplate.Domain.Aggregates.Products.Events;

public record Deleted(string Id, string Sku) : EventBase;
