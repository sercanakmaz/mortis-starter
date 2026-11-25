using Boilerplate.Shared.Domain;

namespace Boilerplate.Domain.Aggregates.Categories.Events;

public record Created(string Id, string Name) : EventBase;
