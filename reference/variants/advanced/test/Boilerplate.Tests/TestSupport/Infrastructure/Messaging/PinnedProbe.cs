using Boilerplate.Shared.Domain;

namespace Boilerplate.Tests.TestSupport.Infrastructure.Messaging;

[MessageName("Old.Namespace:Pinned")]
public record PinnedProbe : EventBase, IQueueEvent;
