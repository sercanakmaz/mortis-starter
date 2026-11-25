using System.Globalization;
using Acme.Domain.Aggregates.Orders;
using Acme.Shared.Exceptions;

namespace Acme.Tests.Domain.Orders;

public class MoneyTests
{
    [Fact]
    public void Constructor_Should_TrimAndUpperCaseTheCurrency()
    {
        var money = new Money(12.5m, " eur ");

        Assert.Equal(12.5m, money.Amount);
        Assert.Equal("EUR", money.Currency);
    }

    [Fact]
    public void Constructor_UnderTurkishCulture_Should_UpperCaseTheCurrencyInvariantly()
    {
        var previousCulture = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = new CultureInfo("tr-TR");

        var money = new Money(1m, "inr");

        CultureInfo.CurrentCulture = previousCulture;
        Assert.Equal("INR", money.Currency);
    }

    [Fact]
    public void Constructor_WithNegativeAmount_Should_ThrowInvalidParameterForAmount()
    {
        var exception = Assert.Throws<InvalidParameterException>(() => new Money(-0.01m, "EUR"));

        Assert.StartsWith("amount is invalid", exception.Message);
    }

    [Fact]
    public void Constructor_WithTwoLetterCurrency_Should_ThrowInvalidParameterForCurrency()
    {
        var exception = Assert.Throws<InvalidParameterException>(() => new Money(1m, "EU"));

        Assert.StartsWith("currency is invalid", exception.Message);
    }

    [Fact]
    public void Equals_WithSameAmountAndCurrency_Should_BeEqual()
    {
        Assert.Equal(new Money(5m, "eur"), new Money(5m, "EUR"));
        Assert.NotEqual(new Money(5m, "EUR"), new Money(5m, "USD"));
    }
}
