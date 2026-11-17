using System.Text.Json;
using Acme.Application.Customers;
using Acme.Application.Orders;
using Acme.Application.Products;
using Acme.Domain.Aggregates.Orders;
using Acme.Shared.Application;

namespace Acme.Console.Scenarios;

public class OrderFlowScenario(ICommandDispatcher commandDispatcher) : IScenario
{
    public string Name => "order-flow";

    public async Task RunAsync(string[] args)
    {
        var suffix = Guid.CreateVersion7().ToString("N")[^8..];

        var firstProduct = (await commandDispatcher.Send(new CreateProductCommand { Sku = $"SKU-1-{suffix}", Name = "First product", Stock = 100 }.AsConsoleUser())).Content;
        var secondProduct = (await commandDispatcher.Send(new CreateProductCommand { Sku = $"SKU-2-{suffix}", Name = "Second product", Stock = 100 }.AsConsoleUser())).Content;
        var thirdProduct = (await commandDispatcher.Send(new CreateProductCommand { Sku = $"SKU-3-{suffix}", Name = "Third product", Stock = 100 }.AsConsoleUser())).Content;

        var customer = (await commandDispatcher.Send(new CreateCustomerCommand
        {
            Name = "Console Customer",
            Email = $"customer-{suffix}@example.com"
        }.AsConsoleUser())).Content;

        var order = (await commandDispatcher.Send(new CreateOrderCommand
        {
            OrderNumber = $"ORD-{suffix}",
            CustomerId = customer.Id,
            Lines =
            [
                new OrderLineModel(firstProduct.Sku, 10.50m, "eur", 2),
                new OrderLineModel(secondProduct.Sku, 4.00m, "EUR", 1)
            ]
        }.AsConsoleUser())).Content;

        var orderWithAddedLine = (await commandDispatcher.Send(new AddOrderLineCommand
        {
            Id = order.Id,
            Line = new OrderLineModel(thirdProduct.Sku, 1.25m, "EUR", 4)
        }.AsConsoleUser())).Content;

        await commandDispatcher.Send(new CancelOrderLineCommand
        {
            Id = order.Id,
            LineId = orderWithAddedLine.Lines.First(orderLine => orderLine.Sku == secondProduct.Sku).Id,
            Reason = "out of stock"
        }.AsConsoleUser());

        var completedOrder = (await commandDispatcher.Send(new CompleteOrderCommand(order.Id).AsConsoleUser())).Content;
        var customerAfterCompletion = (await commandDispatcher.Send(new GetCustomerCommand(customer.Id))).Content;
        var firstProductAfterCompletion = (await commandDispatcher.Send(new GetProductCommand(firstProduct.Id))).Content;
        var secondProductAfterCompletion = (await commandDispatcher.Send(new GetProductCommand(secondProduct.Id))).Content;
        var thirdProductAfterCompletion = (await commandDispatcher.Send(new GetProductCommand(thirdProduct.Id))).Content;

        System.Console.WriteLine(JsonSerializer.Serialize(new
        {
            Order = completedOrder,
            Customer = customerAfterCompletion,
            Products = new[] { firstProductAfterCompletion, secondProductAfterCompletion, thirdProductAfterCompletion }
        }, ScenarioConstants.OutputOptions));
    }
}
