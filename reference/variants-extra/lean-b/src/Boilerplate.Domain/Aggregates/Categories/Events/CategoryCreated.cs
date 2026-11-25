using Boilerplate.Shared.Domain;

namespace Boilerplate.Domain.Aggregates.Categories.Events;

public record CategoryCreated(string Id, string Name) : EventBase;
