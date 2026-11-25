using Boilerplate.Contracts.Orders;
using Boilerplate.Tests.TestSupport.Infrastructure.Persistence;
using Boilerplate.Tests.TestSupport.Shared.Persistence;
using Boilerplate.Infrastructure.Persistence.Orders;
using Boilerplate.Tests.TestSupport.Domain.Orders;
using Boilerplate.Domain.Aggregates.Orders.Events;
using Boilerplate.Domain.Aggregates.Orders;
using Boilerplate.Shared.Exceptions;
using Microsoft.EntityFrameworkCore;
using Boilerplate.Shared.Domain;
using Moq;

namespace Boilerplate.Tests.Infrastructure.Persistence.Orders;

public class OrderRepositoryTests
{
    [Fact]
    public async Task CreateAsync_Should_HandTheRaisedEventsToTheEventContextBeforeSaving()
    {
        var raisedCountAtSave = -1;
        var eventContext = new EventContext();
        var order = FakeOrder.Create();
        var unitOfWork = UnitOfWorkBuilder.Create()
            .WithHasTransaction(true)
            .WithSaveChangesAsync(() => raisedCountAtSave = eventContext.RaisedCount)
            .Build();
        var orderRepository = new OrderRepository(AppDbContextBuilder.Create().Build(), unitOfWork, eventContext);

        await orderRepository.CreateAsync(order);

        Assert.Equal(1, raisedCountAtSave);
        Assert.IsType<Created>(eventContext.TakeRaised());
        Assert.Empty(order.DomainEvents);
    }

    [Fact]
    public async Task CreateAsync_WithoutATransaction_Should_ThrowTransactionRequiredAndNotSave()
    {
        var unitOfWorkBuilder = UnitOfWorkBuilder.Create().WithHasTransaction(false).WithSaveChangesAsync();
        var orderRepository = new OrderRepository(AppDbContextBuilder.Create().Build(), unitOfWorkBuilder.Build(), new EventContext());

        var exception = await Assert.ThrowsAsync<TransactionRequiredException>(() => orderRepository.CreateAsync(FakeOrder.Create()));

        Assert.Contains(nameof(Order), exception.Message);
        unitOfWorkBuilder.BuildMock().Verify(unitOfWork => unitOfWork.SaveChangesAsync(), Times.Never);
    }

    [Fact]
    public async Task UpdateAsync_WithAnOrderThatWasNotLoadedForTracking_Should_ThrowAndKeepItsEvents()
    {
        var eventContext = new EventContext();
        var order = FakeOrder.Create();
        var unitOfWork = UnitOfWorkBuilder.Create().WithHasTransaction(true).WithSaveChangesAsync().Build();
        var orderRepository = new OrderRepository(AppDbContextBuilder.Create().Build(), unitOfWork, eventContext);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => orderRepository.UpdateAsync(order));

        Assert.Contains("FindByIdTrackingAsync", exception.Message);
        Assert.Equal(0, eventContext.RaisedCount);
        Assert.Single(order.DomainEvents);
    }

    [Fact]
    public async Task UpdateAsync_WithALineAddedToATrackedOrder_Should_InsertTheLineInsteadOfUpdatingIt()
    {
        var stateOfNewLineAtSave = EntityState.Detached;
        var context = AppDbContextBuilder.Create().Build();
        var order = FakeOrder.Create().WithLine("SKU-1").WithoutEvents();
        context.Attach(order);
        var newLine = order.AddLine("SKU-2", new Money(2m, "EUR"), 1);
        var unitOfWork = UnitOfWorkBuilder.Create()
            .WithHasTransaction(true)
            .WithSaveChangesAsync(() =>
            {
                context.ChangeTracker.DetectChanges();
                stateOfNewLineAtSave = context.Entry(newLine).State;
            })
            .Build();
        var orderRepository = new OrderRepository(context, unitOfWork, new EventContext());

        await orderRepository.UpdateAsync(order);

        Assert.Equal(EntityState.Added, stateOfNewLineAtSave);
        Assert.Equal(EntityState.Unchanged, context.Entry(order.Lines.First(orderLine => orderLine.Sku == "SKU-1")).State);
    }

    [Fact]
    public async Task UpdateAsync_WithACompletedTrackedOrder_Should_HandCompletedToTheEventContext()
    {
        var eventContext = new EventContext();
        var context = AppDbContextBuilder.Create().Build();
        var order = FakeOrder.Create().WithLine("SKU-1").WithoutEvents();
        context.Attach(order);
        order.Complete();
        var unitOfWork = UnitOfWorkBuilder.Create().WithHasTransaction(true).WithSaveChangesAsync().Build();
        var orderRepository = new OrderRepository(context, unitOfWork, eventContext);

        await orderRepository.UpdateAsync(order);

        Assert.IsType<Completed>(eventContext.TakeRaised());
        Assert.Empty(order.DomainEvents);
    }
}
