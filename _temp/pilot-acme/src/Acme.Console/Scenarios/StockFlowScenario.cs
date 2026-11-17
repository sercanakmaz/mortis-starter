using System.Text.Json;
using Acme.Application.Customers;
using Acme.Application.Orders;
using Acme.Application.Products;
using Acme.Domain.Aggregates.Orders;
using Acme.Shared.Application;

namespace Acme.Console.Scenarios;

public class StockFlowScenario(ICommandDispatcher commandDispatcher) : IScenario
{
    public string Name => "stock-flow";

    public async Task RunAsync(string[] args)
    {
        var suffix = Guid.CreateVersion7().ToString("N")[^8..];

        var bolt = (await commandDispatcher.Send(new CreateProductCommand { Sku = $"BOLT-{suffix}", Name = "Bolt", Stock = 10 }.AsConsoleUser())).Content;
        var nut = (await commandDispatcher.Send(new CreateProductCommand { Sku = $"NUT-{suffix}", Name = "Nut", Stock = 5 }.AsConsoleUser())).Content;

        var customer = (await commandDispatcher.Send(new CreateCustomerCommand
        {
            Name = "Stock Customer",
            Email = $"stock-{suffix}@example.com"
        }.AsConsoleUser())).Content;

        var order = (await commandDispatcher.Send(new CreateOrderCommand
        {
            OrderNumber = $"STK-{suffix}",
            CustomerId = customer.Id,
            Lines = [new OrderLineModel(bolt.Sku, 1.00m, "EUR", 3), new OrderLineModel(nut.Sku, 0.50m, "EUR", 2)]
        }.AsConsoleUser())).Content;

        var boltAfterOrder = (await commandDispatcher.Send(new GetProductCommand(bolt.Id))).Content;
        var nutAfterOrder = (await commandDispatcher.Send(new GetProductCommand(nut.Id))).Content;

        await commandDispatcher.Send(new CancelOrderLineCommand
        {
            Id = order.Id,
            LineId = order.Lines.First(orderLine => orderLine.Sku == bolt.Sku).Id,
            Reason = "changed mind"
        }.AsConsoleUser());

        var orderAfterFirstCancel = (await commandDispatcher.Send(new GetOrderCommand(order.Id))).Content;
        var boltAfterFirstCancel = (await commandDispatcher.Send(new GetProductCommand(bolt.Id))).Content;

        await commandDispatcher.Send(new CancelOrderLineCommand
        {
            Id = order.Id,
            LineId = order.Lines.First(orderLine => orderLine.Sku == nut.Sku).Id,
            Reason = "changed mind"
        }.AsConsoleUser());

        var orderAfterLastCancel = (await commandDispatcher.Send(new GetOrderCommand(order.Id))).Content;
        var nutAfterLastCancel = (await commandDispatcher.Send(new GetProductCommand(nut.Id))).Content;

        var oversoldOrder = (await commandDispatcher.Send(new CreateOrderCommand
        {
            OrderNumber = $"OVR-{suffix}",
            CustomerId = customer.Id,
            Lines = [new OrderLineModel(bolt.Sku, 1.00m, "EUR", 20)]
        }.AsConsoleUser())).Content;

        var oversoldOrderAfterwards = (await commandDispatcher.Send(new GetOrderCommand(oversoldOrder.Id))).Content;
        var boltAfterOversell = (await commandDispatcher.Send(new GetProductCommand(bolt.Id))).Content;

        System.Console.WriteLine(JsonSerializer.Serialize(new
        {
            AfterOrder = new { Bolt = boltAfterOrder.Stock, Nut = nutAfterOrder.Stock },
            AfterFirstCancel = new { OrderStatus = orderAfterFirstCancel.Status, Bolt = boltAfterFirstCancel.Stock },
            AfterLastCancel = new { OrderStatus = orderAfterLastCancel.Status, Nut = nutAfterLastCancel.Stock },
            Oversell = new { OrderStatus = oversoldOrderAfterwards.Status, Bolt = boltAfterOversell.Stock }
        }, ScenarioConstants.OutputOptions));
    }
}
