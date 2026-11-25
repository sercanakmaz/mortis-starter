using Boilerplate.Domain.Aggregates.Customers;
using Boilerplate.Infrastructure.Secrets;
using Boilerplate.Tests.TestSupport.Infrastructure.Persistence;

namespace Boilerplate.Tests.Infrastructure.Persistence.Customers;

public class CustomerConfigurationTests
{
    [Fact]
    public void Model_Should_StoreTheTaxNumberAsAnEnvelopeThroughAConverter()
    {
        var property = AppDbContextBuilder.Create().Build().Model.FindEntityType(typeof(Customer))!.FindProperty(nameof(Customer.TaxNumber))!;

        var converter = property.GetValueConverter();

        Assert.NotNull(converter);
        Assert.StartsWith(SecretsConstants.Version + ".", (string)converter.ConvertToProvider("TR-123"));
        Assert.Equal(SecretsConstants.EnvelopeMaxLength, property.GetMaxLength());
    }
}
