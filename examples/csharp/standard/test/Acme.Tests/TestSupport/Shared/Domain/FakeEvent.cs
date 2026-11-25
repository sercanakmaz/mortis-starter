using Acme.Shared.Domain;

namespace Acme.Tests.TestSupport.Shared.Domain;

public record FakeEvent(string Id) : EventBase;

public record FakeCascadedEvent(string Id) : EventBase;

public record FakeQueueEvent(string Id) : EventBase, IQueueEvent;
