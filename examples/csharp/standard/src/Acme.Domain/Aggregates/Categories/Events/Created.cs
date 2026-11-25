using Acme.Shared.Domain;

namespace Acme.Domain.Aggregates.Categories.Events;

public record Created(string Id, string Name) : EventBase;
