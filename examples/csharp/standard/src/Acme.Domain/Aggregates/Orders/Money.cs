using Acme.Shared.Exceptions;

namespace Acme.Domain.Aggregates.Orders;

public record Money
{
    public decimal Amount { get; }

    public string Currency { get; }

    public Money(decimal amount, string currency)
    {
        if (amount < 0)
        {
            throw new InvalidParameterException(nameof(amount), "it cannot be negative");
        }

        if (string.IsNullOrWhiteSpace(currency) || currency.Trim().Length != OrderConstants.CurrencyLength)
        {
            throw new InvalidParameterException(nameof(currency), "it must be a three-letter code");
        }

        Amount = amount;
        Currency = currency.Trim().ToUpperInvariant();
    }
}
