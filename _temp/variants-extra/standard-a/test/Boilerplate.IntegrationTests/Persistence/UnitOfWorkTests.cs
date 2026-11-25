using Boilerplate.Domain.Aggregates.Products;
using Boilerplate.IntegrationTests.TestSupport;
using Boilerplate.Shared.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace Boilerplate.IntegrationTests.Persistence;

[Collection(IntegrationConstants.MongoCollection)]
public class UnitOfWorkTests(MongoFixture mongoFixture)
{
    [SkippableFact]
    public async Task RollbackTransactionAsync_Should_LeaveNoDocument()
    {
        Skip.If(mongoFixture.Unavailable is not null, mongoFixture.Unavailable);
        await using var serviceProvider = IntegrationServices.Create(mongoFixture.CreateDatabase(nameof(UnitOfWorkTests)));

        using (var scope = serviceProvider.CreateScope())
        {
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            await unitOfWork.BeginTransactionAsync(nameof(UnitOfWorkTests));
            await scope.ServiceProvider.GetRequiredService<IProductRepository>().CreateAsync(new Product(Guid.CreateVersion7(), "SKU-RB", "Rolled back", 1));
            await unitOfWork.RollbackTransactionAsync();
        }

        Assert.Equal(0, await serviceProvider.CountAsync<Product>("products", product => product.Sku == "SKU-RB"));
    }
}
