using System.Reflection;
using Boilerplate.Domain;

namespace Boilerplate.Tests.Domain;

public class ErrorCatalogueTests
{
    [Fact]
    public void Codes_Should_DeclareEveryRuleCodeOfTheDomain()
    {
        var codes = typeof(ErrorCatalogue).Assembly.GetTypes()
            .Where(type => type.Name.EndsWith("Constants"))
            .SelectMany(type => type.GetFields(BindingFlags.Public | BindingFlags.Static))
            .Where(field => field.IsLiteral && field.Name.EndsWith("Code"))
            .Select(field => (string)field.GetRawConstantValue())
            .ToList();

        Assert.All(codes, code => Assert.True(ErrorCatalogue.Codes.Contains(code), $"{code} is not catalogued"));
    }

    [Fact]
    public void Codes_Should_DeclareTheCodesOfTheSharedFailures()
    {
        Assert.All(["not_found", "duplicate", "forbidden", "invalid_parameter", "reference_violation", "transaction_required", "tenant_required", "validation_failed"], code => Assert.True(ErrorCatalogue.Codes.Contains(code), code));
    }
}
