using Boilerplate.Tests.TestSupport.Infrastructure.Persistence;
using Boilerplate.Domain.Aggregates.Customers;
using Microsoft.EntityFrameworkCore.Metadata;
using Boilerplate.Domain.Aggregates.Orders;
using Microsoft.EntityFrameworkCore;

namespace Boilerplate.Tests.Infrastructure.Persistence.Orders;

public class OrderConfigurationTests
{
    [Fact]
    public void Model_Should_MapOrdersAndTheirLinesToSnakeCaseTables()
    {
        var model = AppDbContextBuilder.Create().Build().Model;

        Assert.Equal("orders", model.FindEntityType(typeof(Order)).GetTableName());
        Assert.Equal("order_lines", model.FindEntityType(typeof(OrderLine)).GetTableName());
        Assert.Equal("order_number", model.FindEntityType(typeof(Order)).FindProperty(nameof(Order.OrderNumber)).GetColumnName());
    }

    [Fact]
    public void Model_Should_ReferenceTheCustomerThroughARestrictingForeignKey()
    {
        var model = AppDbContextBuilder.Create().Build().Model;

        var foreignKey = Assert.Single(model.FindEntityType(typeof(Order)).GetForeignKeys());

        Assert.Equal(typeof(Customer), foreignKey.PrincipalEntityType.ClrType);
        Assert.Equal(nameof(Order.CustomerId), Assert.Single(foreignKey.Properties).Name);
        Assert.Equal(DeleteBehavior.Restrict, foreignKey.DeleteBehavior);
    }

    [Fact]
    public void Model_Should_NeverLetTheDatabaseGenerateIds()
    {
        var model = AppDbContextBuilder.Create().Build().Model;

        Assert.Equal(ValueGenerated.Never, model.FindEntityType(typeof(Order)).FindProperty(nameof(Order.Id)).ValueGenerated);
        Assert.Equal(ValueGenerated.Never, model.FindEntityType(typeof(OrderLine)).FindProperty(nameof(OrderLine.Id)).ValueGenerated);
    }

    [Fact]
    public void Model_Should_NotMapDomainEventsOrTheVersion()
    {
        var orderEntityType = AppDbContextBuilder.Create().Build().Model.FindEntityType(typeof(Order));

        Assert.Null(orderEntityType.FindProperty(nameof(Order.Version)));
        Assert.Null(orderEntityType.FindNavigation(nameof(Order.DomainEvents)));
        Assert.Null(orderEntityType.FindProperty(nameof(Order.TotalAmount)));
    }
}
