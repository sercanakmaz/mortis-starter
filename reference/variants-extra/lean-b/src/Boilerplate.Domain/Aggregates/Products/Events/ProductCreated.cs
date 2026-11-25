using Boilerplate.Shared.Domain;

namespace Boilerplate.Domain.Aggregates.Products.Events;

public record ProductCreated(string Id, string Sku) : EventBase;
