using Boilerplate.Domain.Aggregates.Orders.Events;
using Boilerplate.Tests.TestSupport.Domain.Orders;
using Boilerplate.Domain.Aggregates.Orders;
using Boilerplate.Shared.Exceptions;

namespace Boilerplate.Tests.Domain.Orders;

public class OrderTests
{
    [Fact]
    public void Empty_Constructor_Should_Create()
    {
        var order = FakeOrder.CreateEmpty();

        Assert.NotNull(order);
        Assert.Empty(order.Lines);
    }

    [Fact]
    public void Constructor_Should_OpenTheOrderAndRaiseCreated()
    {
        var id = Guid.CreateVersion7();
        var customerId = Guid.CreateVersion7();

        var order = FakeOrder.Create(id).WithCustomerId(customerId);

        var created = Assert.IsType<Created>(Assert.Single(order.DomainEvents));
        Assert.Equal(id.ToString(), created.Id);
        Assert.Equal("ORD-DEFAULT", created.OrderNumber);
        Assert.Equal(OrderStatus.Open, order.Status);
    }

    [Fact]
    public void AddLine_Should_AddALineThatBelongsToTheOrder()
    {
        var order = FakeOrder.Create();

        var orderLine = order.AddLine("SKU-1", new Money(10.5m, "EUR"), 2);

        Assert.Same(orderLine, Assert.Single(order.Lines));
        Assert.Equal(order.Id, orderLine.OrderId);
        Assert.NotEqual(Guid.Empty, orderLine.Id);
        Assert.Equal(21m, order.TotalAmount);
    }

    [Fact]
    public void AddLine_Should_RaiseLineAddedWithTheSkuAndTheQuantity()
    {
        var order = FakeOrder.Create().WithoutEvents();

        var orderLine = order.AddLine("SKU-1", new Money(10.5m, "EUR"), 3);

        var lineAdded = Assert.IsType<LineAdded>(Assert.Single(order.DomainEvents));
        Assert.Equal(order.Id.ToString(), lineAdded.Id);
        Assert.Equal(orderLine.Id.ToString(), lineAdded.LineId);
        Assert.Equal("SKU-1", lineAdded.Sku);
        Assert.Equal(3, lineAdded.Quantity);
    }

    [Fact]
    public void AddLine_WithSkuOfAnActiveLine_Should_ThrowDuplicate()
    {
        var order = FakeOrder.Create().WithLine("SKU-1");

        var exception = Assert.Throws<DuplicateException>(() => order.AddLine("SKU-1", new Money(1m, "EUR"), 1));

        Assert.Equal("OrderLine already exists.", exception.Message);
        Assert.Single(order.Lines);
    }

    [Fact]
    public void AddLine_WithAnotherCurrency_Should_ThrowMixedCurrency()
    {
        var order = FakeOrder.Create().WithLine("SKU-1", currency: "EUR");

        var exception = Assert.Throws<BusinessRuleException>(() => order.AddLine("SKU-2", new Money(1m, "USD"), 1));

        Assert.Equal(OrderConstants.MixedCurrencyCode, exception.Code);
    }

    [Fact]
    public void AddLine_WithZeroQuantity_Should_ThrowInvalidParameterForQuantity()
    {
        var order = FakeOrder.Create();

        var exception = Assert.Throws<InvalidParameterException>(() => order.AddLine("SKU-1", new Money(1m, "EUR"), 0));

        Assert.StartsWith("quantity is invalid", exception.Message);
    }

    [Fact]
    public void AddLine_WhenTheOrderIsCompleted_Should_ThrowNotOpen()
    {
        var order = FakeOrder.Create().WithLine("SKU-1").WithCompleted();

        var exception = Assert.Throws<BusinessRuleException>(() => order.AddLine("SKU-2", new Money(1m, "EUR"), 1));

        Assert.Equal(OrderConstants.NotOpenCode, exception.Code);
    }

    [Fact]
    public void CancelLine_WithAnotherActiveLine_Should_KeepTheOrderOpenAndDropTheLineFromTheTotal()
    {
        var order = FakeOrder.Create().WithLine("SKU-1", price: 10m).WithLine("SKU-2", price: 4m).WithoutEvents();
        var cancelledLine = order.Lines.First(orderLine => orderLine.Sku == "SKU-1");

        order.CancelLine(cancelledLine.Id, "out of stock");

        Assert.True(cancelledLine.IsCancelled);
        Assert.Equal("out of stock", cancelledLine.CancellationReason);
        Assert.Equal(4m, order.TotalAmount);
        Assert.Equal(OrderStatus.Open, order.Status);
        Assert.IsType<LineCancelled>(Assert.Single(order.DomainEvents));
    }

    [Fact]
    public void CancelLine_Should_RaiseLineCancelledWithTheSkuAndTheQuantityOfTheLine()
    {
        var order = FakeOrder.Create().WithLine("SKU-1", quantity: 4).WithoutEvents();
        var orderLine = order.Lines.First();

        order.CancelLine(orderLine.Id, "out of stock");

        var lineCancelled = Assert.IsType<LineCancelled>(Assert.Single(order.DomainEvents));
        Assert.Equal(order.Id.ToString(), lineCancelled.Id);
        Assert.Equal(orderLine.Id.ToString(), lineCancelled.LineId);
        Assert.Equal("SKU-1", lineCancelled.Sku);
        Assert.Equal(4, lineCancelled.Quantity);
        Assert.Equal("out of stock", lineCancelled.Reason);
    }

    [Fact]
    public void CancelLine_WithTheLastActiveLine_Should_LeaveCancellingTheOrderToTheEventHandler()
    {
        var order = FakeOrder.Create().WithLine("SKU-1").WithoutEvents();

        order.CancelLine(order.Lines.First().Id, "out of stock");

        Assert.Equal(OrderStatus.Open, order.Status);
        Assert.IsType<LineCancelled>(Assert.Single(order.DomainEvents));
    }

    [Fact]
    public void CancelWhenAllLinesCancelled_WithEveryLineCancelled_Should_CancelTheOrderAndRaiseCancelled()
    {
        var customerId = Guid.CreateVersion7();
        var order = FakeOrder.Create().WithCustomerId(customerId).WithLine("SKU-1").WithLine("SKU-2").WithCancelledLine("SKU-1").WithCancelledLine("SKU-2").WithoutEvents();

        var isCancelled = order.CancelWhenAllLinesCancelled();

        Assert.True(isCancelled);
        Assert.Equal(OrderStatus.Cancelled, order.Status);
        var cancelled = Assert.IsType<Cancelled>(Assert.Single(order.DomainEvents));
        Assert.Equal(order.Id.ToString(), cancelled.Id);
        Assert.Equal(customerId.ToString(), cancelled.CustomerId);
    }

    [Fact]
    public void CancelWhenAllLinesCancelled_WithAnActiveLine_Should_KeepTheOrderOpenAndRaiseNothing()
    {
        var order = FakeOrder.Create().WithLine("SKU-1").WithLine("SKU-2").WithCancelledLine("SKU-1").WithoutEvents();

        var isCancelled = order.CancelWhenAllLinesCancelled();

        Assert.False(isCancelled);
        Assert.Equal(OrderStatus.Open, order.Status);
        Assert.Null(order.DomainEvents?.FirstOrDefault());
    }

    [Fact]
    public void CancelWhenAllLinesCancelled_WhenAlreadyCancelled_Should_DoNothingTheSecondTime()
    {
        var order = FakeOrder.Create().WithLine("SKU-1").WithCancelled().WithoutEvents();

        var isCancelled = order.CancelWhenAllLinesCancelled();

        Assert.False(isCancelled);
        Assert.Equal(OrderStatus.Cancelled, order.Status);
        Assert.Null(order.DomainEvents?.FirstOrDefault());
    }

    [Fact]
    public void CancelWhenAllLinesCancelled_WhenTheOrderIsCompleted_Should_NotCancelIt()
    {
        var order = FakeOrder.Create().WithLine("SKU-1").WithCompleted().WithoutEvents();

        var isCancelled = order.CancelWhenAllLinesCancelled();

        Assert.False(isCancelled);
        Assert.Equal(OrderStatus.Completed, order.Status);
    }

    [Fact]
    public void CancelLine_WithUnknownLine_Should_ThrowNotFoundForOrderLine()
    {
        var order = FakeOrder.Create().WithLine("SKU-1");

        var exception = Assert.Throws<NotFoundException>(() => order.CancelLine(Guid.CreateVersion7(), "out of stock"));

        Assert.Equal("OrderLine was not found.", exception.Message);
    }

    [Fact]
    public void Complete_Should_RaiseCompletedWithTheCustomer()
    {
        var customerId = Guid.CreateVersion7();
        var order = FakeOrder.Create().WithCustomerId(customerId).WithLine("SKU-1").WithoutEvents();

        order.Complete();

        var completed = Assert.IsType<Completed>(Assert.Single(order.DomainEvents));
        Assert.Equal(order.Id.ToString(), completed.Id);
        Assert.Equal(customerId.ToString(), completed.CustomerId);
        Assert.Equal(OrderStatus.Completed, order.Status);
    }

    [Fact]
    public void Complete_AsFulfilledWithoutLines_Should_ThrowNothingToFulfil()
    {
        var order = FakeOrder.Create();

        var exception = Assert.Throws<BusinessRuleException>(() => order.Complete());

        Assert.Equal(OrderConstants.NothingToFulfilCode, exception.Code);
        Assert.Equal(OrderStatus.Open, order.Status);
    }

    [Fact]
    public void Complete_WithEveryLineCancelled_Should_ThrowNothingToFulfil()
    {
        var order = FakeOrder.Create().WithLine("SKU-1").WithCancelledLine("SKU-1");

        var exception = Assert.Throws<BusinessRuleException>(() => order.Complete());

        Assert.Equal(OrderConstants.NothingToFulfilCode, exception.Code);
    }

    [Fact]
    public void Complete_WhenTheOrderIsCancelled_Should_ThrowNotOpen()
    {
        var order = FakeOrder.Create().WithLine("SKU-1").WithCancelled();

        var exception = Assert.Throws<BusinessRuleException>(() => order.Complete());

        Assert.Equal(OrderConstants.NotOpenCode, exception.Code);
    }

    [Fact]
    public void Complete_WhenAlreadyCompleted_Should_ThrowNotOpen()
    {
        var order = FakeOrder.Create().WithLine("SKU-1").WithCompleted();

        var exception = Assert.Throws<BusinessRuleException>(() => order.Complete());

        Assert.Equal(OrderConstants.NotOpenCode, exception.Code);
    }
}
