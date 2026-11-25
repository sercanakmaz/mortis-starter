using Boilerplate.Application.Products;
using Boilerplate.Domain.Aggregates.Products;
using Boilerplate.IntegrationTests.TestSupport;
using Boilerplate.Shared.Exceptions;

namespace Boilerplate.IntegrationTests.Persistence;

[Collection(IntegrationConstants.PostgresCollection)]
public class ProductRepositoryTests(PostgresFixture postgresFixture)
{
    [SkippableFact]
    public async Task Send_WithADuplicateSku_Should_ThrowDuplicateAndKeepOneRow()
    {
        Skip.If(postgresFixture.Unavailable is not null, postgresFixture.Unavailable);
        await using var serviceProvider = IntegrationServices.Create(await postgresFixture.CreateDatabaseAsync(nameof(ProductRepositoryTests)));

        await serviceProvider.Send(new CreateProductCommand { Sku = "SKU-1", Name = "Bolt", Stock = 1 });
        await Assert.ThrowsAsync<DuplicateException>(() => serviceProvider.Send(new CreateProductCommand { Sku = "SKU-1", Name = "Bolt again", Stock = 1 }));

        Assert.Equal(1, await serviceProvider.CountAsync<Product>(product => product.Sku == "SKU-1"));
    }
}
