using Boilerplate.Application.Products;

namespace Boilerplate.Tests.Application.Products;

public class CreateProductCommandValidatorTests
{
    [Theory]
    [InlineData("", "Bolt", 1, "Sku")]
    [InlineData("SKU-1", "", 1, "Name")]
    [InlineData("SKU-1", "Bolt", -1, "Stock")]
    public void Validate_WithAnInvalidMember_Should_FailOnThatMember(string sku, string name, int stock, string member)
    {
        var validationResult = new CreateProductCommandValidator().Validate(new CreateProductCommand { Sku = sku, Name = name, Stock = stock });

        Assert.Contains(validationResult.Errors, validationFailure => validationFailure.PropertyName == member);
    }

    [Fact]
    public void Validate_WithAValidCommand_Should_Pass()
    {
        Assert.True(new CreateProductCommandValidator().Validate(new CreateProductCommand { Sku = "SKU-1", Name = "Bolt", Stock = 1 }).IsValid);
    }
}
