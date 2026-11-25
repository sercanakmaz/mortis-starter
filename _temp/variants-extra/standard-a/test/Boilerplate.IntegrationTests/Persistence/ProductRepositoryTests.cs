using Boilerplate.Application.Products;
using Boilerplate.Domain.Aggregates.Products;
using Boilerplate.IntegrationTests.TestSupport;
using Boilerplate.Infrastructure.Persistence.Mongo;
using Boilerplate.Shared.Exceptions;
using Microsoft.Extensions.DependencyInjection;

namespace Boilerplate.IntegrationTests.Persistence;

[Collection(IntegrationConstants.MongoCollection)]
public class ProductRepositoryTests(MongoFixture mongoFixture)
{
    [SkippableFact]
    public async Task Send_WithADuplicateSku_Should_ThrowDuplicateAndKeepOneDocument()
    {
        Skip.If(mongoFixture.Unavailable is not null, mongoFixture.Unavailable);
        await using var serviceProvider = IntegrationServices.Create(mongoFixture.CreateDatabase(nameof(ProductRepositoryTests)));

        await serviceProvider.GetRequiredService<MongoIndexes>().EnsureAsync();
        await serviceProvider.Send(new CreateProductCommand { Sku = "SKU-1", Name = "Bolt", Stock = 1 });
        await Assert.ThrowsAsync<DuplicateException>(() => serviceProvider.Send(new CreateProductCommand { Sku = "SKU-1", Name = "Bolt again", Stock = 1 }));

        Assert.Equal(1, await serviceProvider.CountAsync<Product>("products", product => product.Sku == "SKU-1"));
    }
}
