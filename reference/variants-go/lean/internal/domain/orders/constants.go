package orders

// The rule codes and lengths of an order.
const (
	NotOpenCode              = "order_not_open"
	MixedCurrencyCode        = "order_mixed_currency"
	NothingToFulfilCode      = "order_nothing_to_fulfil"
	LineAlreadyCancelledCode = "order_line_already_cancelled"
	OrderNumberMaxLength     = 64
	SKUMaxLength             = 64
	CurrencyLength           = 3
	ReasonMaxLength          = 500
)
