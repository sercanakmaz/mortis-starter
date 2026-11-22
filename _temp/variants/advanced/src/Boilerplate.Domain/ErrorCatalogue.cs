using Boilerplate.Domain.Aggregates.Customers;
using Boilerplate.Domain.Aggregates.Products;
using Boilerplate.Domain.Aggregates.Orders;

namespace Boilerplate.Domain;

public static class ErrorCatalogue
{
    public static readonly IReadOnlySet<string> Codes = new HashSet<string>
    {
        "not_found",
        "duplicate",
        "forbidden",
        "invalid_parameter",
        "reference_violation",
        "transaction_required",
        "tenant_required",
        "validation_failed",
        CustomerConstants.BlockedCode,
        ProductConstants.InsufficientStockCode,
        OrderConstants.NotOpenCode,
        OrderConstants.MixedCurrencyCode,
        OrderConstants.NothingToFulfilCode,
        OrderConstants.LineAlreadyCancelledCode
    };
}
