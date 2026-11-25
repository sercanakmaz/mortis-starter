using Boilerplate.Shared.Domain;

namespace Boilerplate.Contracts.Orders;

public record Completed(string Id, string OrderNumber, string CustomerId) : EventBase, IQueueEvent;
