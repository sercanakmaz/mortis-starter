using Acme.Domain.Aggregates.Products;
using Acme.IntegrationTests.TestSupport;
using Acme.Shared.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace Acme.IntegrationTests.Persistence;

[Collection(IntegrationConstants.PostgresCollection)]
public class UnitOfWorkTests(PostgresFixture postgresFixture)
{
    [SkippableFact]
    public async Task RollbackTransactionAsync_Should_LeaveNoRow()
    {
        Skip.If(postgresFixture.Unavailable is not null, postgresFixture.Unavailable);
        await using var serviceProvider = IntegrationServices.Create(await postgresFixture.CreateDatabaseAsync(nameof(UnitOfWorkTests)));

        using (var scope = serviceProvider.CreateScope())
        {
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            await unitOfWork.BeginTransactionAsync(nameof(UnitOfWorkTests));
            await scope.ServiceProvider.GetRequiredService<IProductRepository>().CreateAsync(new Product(Guid.CreateVersion7(), "SKU-RB", "Rolled back", 1));
            await unitOfWork.RollbackTransactionAsync();
        }

        Assert.Equal(0, await serviceProvider.CountAsync<Product>(product => product.Sku == "SKU-RB"));
    }
}
