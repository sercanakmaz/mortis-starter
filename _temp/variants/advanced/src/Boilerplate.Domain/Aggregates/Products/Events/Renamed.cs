using Boilerplate.Shared.Domain;

namespace Boilerplate.Domain.Aggregates.Products.Events;

public record Renamed(string Id, string Sku) : EventBase;
