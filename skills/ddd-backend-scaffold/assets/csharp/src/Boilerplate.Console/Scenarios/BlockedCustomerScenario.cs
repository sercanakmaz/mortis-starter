using Boilerplate.Shared.Application;
using Boilerplate.Domain.Aggregates.Orders;
using Boilerplate.Application.Customers;
using Boilerplate.Application.Products;
using Boilerplate.Application.Orders;
using Boilerplate.Shared.Exceptions;
using System.Text.Json;

namespace Boilerplate.Console.Scenarios;

public class BlockedCustomerScenario(ICommandDispatcher commandDispatcher) : IScenario
{
    public string Name => "blocked-customer";

    public async Task RunAsync(string[] args)
    {
        var suffix = Guid.CreateVersion7().ToString("N")[^8..];

        var product = (await commandDispatcher.Send(new CreateProductCommand { Sku = $"SKU-{suffix}", Name = "Product", Stock = 10 }.AsConsoleUser())).Content;

        var customer = (await commandDispatcher.Send(new CreateCustomerCommand
        {
            Name = "Blocked Customer",
            Email = $"blocked-{suffix}@example.com"
        }.AsConsoleUser())).Content;

        var order = (await commandDispatcher.Send(new CreateOrderCommand
        {
            OrderNumber = $"BLK-{suffix}",
            CustomerId = customer.Id,
            Lines = [new OrderLineModel(product.Sku, 25.00m, "EUR", 1)]
        }.AsConsoleUser())).Content;

        await commandDispatcher.Send(new BlockCustomerCommand(customer.Id).AsConsoleUser());

        string rejectedWith = null;

        try
        {
            await commandDispatcher.Send(new CompleteOrderCommand(order.Id).AsConsoleUser());
        }
        catch (BusinessRuleException exception)
        {
            rejectedWith = exception.Code;
        }

        var orderAfterRejection = (await commandDispatcher.Send(new GetOrderCommand(order.Id))).Content;
        var customerAfterRejection = (await commandDispatcher.Send(new GetCustomerCommand(customer.Id))).Content;

        System.Console.WriteLine(JsonSerializer.Serialize(new { RejectedWith = rejectedWith, Order = orderAfterRejection, Customer = customerAfterRejection }, ScenarioConstants.OutputOptions));
    }
}
