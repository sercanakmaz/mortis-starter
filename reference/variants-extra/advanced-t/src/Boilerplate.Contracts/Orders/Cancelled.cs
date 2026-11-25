using Boilerplate.Shared.Domain;

namespace Boilerplate.Contracts.Orders;

public record Cancelled(string Id, string OrderNumber, string CustomerId) : EventBase, IQueueEvent;
