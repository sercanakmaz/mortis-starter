using System.Reflection;
using Boilerplate.Domain;

namespace Boilerplate.Tests.Domain;

public class ErrorCatalogueTests
{
    [Fact]
    public void Messages_Should_DeclareEveryRuleCodeOfTheDomain()
    {
        var codes = typeof(ErrorCatalogue).Assembly.GetTypes()
            .Where(type => type.Name.EndsWith("Constants"))
            .SelectMany(type => type.GetFields(BindingFlags.Public | BindingFlags.Static))
            .Where(field => field.IsLiteral && field.Name.EndsWith("Code"))
            .Select(field => (string)field.GetRawConstantValue())
            .ToList();

        Assert.All(codes, code => Assert.True(ErrorCatalogue.Messages.ContainsKey(code), $"{code} has no message"));
    }

    [Fact]
    public void Messages_Should_DeclareTheCodesOfTheSharedFailures()
    {
        Assert.All(["not_found", "duplicate", "forbidden", "invalid_parameter", "reference_violation", "transaction_required", "validation_failed"], code => Assert.True(ErrorCatalogue.Messages.ContainsKey(code), code));
    }
}
