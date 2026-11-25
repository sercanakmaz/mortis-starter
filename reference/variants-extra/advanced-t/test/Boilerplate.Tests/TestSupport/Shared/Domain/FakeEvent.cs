using Boilerplate.Shared.Domain;

namespace Boilerplate.Tests.TestSupport.Shared.Domain;

public record FakeEvent(string Id) : EventBase;

public record FakeCascadedEvent(string Id) : EventBase;

public record FakeQueueEvent(string Id) : EventBase, IQueueEvent;
