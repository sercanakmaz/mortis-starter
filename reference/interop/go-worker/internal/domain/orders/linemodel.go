package orders

import "github.com/shopspring/decimal"

// LineModel is the shape in which commands receive an order line.
type LineModel struct {
	SKU      string
	Price    decimal.Decimal
	Currency string
	Quantity int
}
