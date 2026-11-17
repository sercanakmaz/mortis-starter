namespace Acme.Domain.Aggregates.Orders;

public static class OrderConstants
{
    public const string NotOpenCode = "order_not_open";

    public const string MixedCurrencyCode = "order_mixed_currency";

    public const string NothingToFulfilCode = "order_nothing_to_fulfil";

    public const string LineAlreadyCancelledCode = "order_line_already_cancelled";

    public const int OrderNumberMaxLength = 64;

    public const int SkuMaxLength = 64;

    public const int CurrencyLength = 3;

    public const int ReasonMaxLength = 500;
}
