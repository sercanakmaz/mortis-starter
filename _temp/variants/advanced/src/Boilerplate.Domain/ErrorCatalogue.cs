using Boilerplate.Domain.Aggregates.Customers;
using Boilerplate.Domain.Aggregates.Products;
using Boilerplate.Domain.Aggregates.Orders;

namespace Boilerplate.Domain;

public static class ErrorCatalogue
{
    public static readonly IReadOnlyDictionary<string, string> Messages = new Dictionary<string, string>
    {
        ["not_found"] = "The record was not found.",
        ["duplicate"] = "The record already exists.",
        ["forbidden"] = "The action is not allowed.",
        ["invalid_parameter"] = "A value is not valid.",
        ["reference_violation"] = "A reference points at a missing record, or the record is still referenced.",
        ["transaction_required"] = "A write ran outside a transaction.",
        ["tenant_required"] = "The record belongs to a tenant, and the caller has none.",
        ["validation_failed"] = "The request is not valid.",
        [CustomerConstants.BlockedCode] = "A blocked customer cannot complete orders.",
        [ProductConstants.InsufficientStockCode] = "The product does not have enough stock.",
        [OrderConstants.NotOpenCode] = "The order is not open.",
        [OrderConstants.MixedCurrencyCode] = "Every line of an order uses the same currency.",
        [OrderConstants.NothingToFulfilCode] = "An order without an active line cannot be fulfilled.",
        [OrderConstants.LineAlreadyCancelledCode] = "The order line is already cancelled."
    };
}
