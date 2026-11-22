using Boilerplate.Shared.Domain;
using Boilerplate.Tests.TestSupport.Infrastructure.Persistence;
using Boilerplate.Tests.TestSupport.Infrastructure.Tenancy;
using Boilerplate.Domain.Aggregates.Categories;
using Boilerplate.Domain.Aggregates.Customers;
using Boilerplate.Domain.Aggregates.Products;
using Boilerplate.Domain.Aggregates.Orders;
using Boilerplate.Shared.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace Boilerplate.Tests.Infrastructure.Persistence;

public class AppDbContextTests
{
    [Fact]
    public void Model_Should_FilterEveryTenantOwnedTypeAndNoGlobalOne()
    {
        var model = AppDbContextBuilder.Create().Build().Model;

        Assert.All([typeof(Product), typeof(Customer), typeof(Order), typeof(OrderLine)], type => Assert.NotEmpty(model.FindEntityType(type).GetDeclaredQueryFilters()));
        Assert.Empty(model.FindEntityType(typeof(Category)).GetDeclaredQueryFilters());
    }

    [Fact]
    public async Task Query_OfATenantOwnedTypeWithoutATenant_Should_ThrowTenantRequiredBeforeAnySql()
    {
        var context = AppDbContextBuilder.Create().Build();

        await Assert.ThrowsAsync<TenantRequiredException>(() => context.Set<Product>().ToListAsync());
    }

    [Fact]
    public async Task SaveChangesAsync_AddingATenantOwnedRowWithoutATenant_Should_ThrowTenantRequiredBeforeAnySql()
    {
        var context = AppDbContextBuilder.Create().Build();

        context.Add(new Product(Guid.CreateVersion7(), "SKU-1", LocalizedText.Of("en", "Product"), 1));

        await Assert.ThrowsAsync<TenantRequiredException>(() => context.SaveChangesAsync());
    }

    [Fact]
    public async Task SaveChangesAsync_DeletingARowOfAnotherTenant_Should_ThrowForbidden()
    {
        var context = AppDbContextBuilder.Create().WithTenantScope(TenantScopeBuilder.Create().WithTenant(Guid.CreateVersion7()).Build()).Build();
        var product = new Product(Guid.CreateVersion7(), "SKU-1", LocalizedText.Of("en", "Product"), 1);

        context.Attach(product);
        context.Remove(product);

        await Assert.ThrowsAsync<ForbiddenException>(() => context.SaveChangesAsync());
    }
}
