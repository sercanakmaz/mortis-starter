using Acme.Shared.Domain;

namespace Acme.Domain.Aggregates.Products.Events;

public record Created(string Id, string Sku) : EventBase;
