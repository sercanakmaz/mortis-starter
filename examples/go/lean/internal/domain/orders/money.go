package orders

import (
	"strings"

	"delta/internal/shared"

	"github.com/shopspring/decimal"
)

// Money is an amount in a three-letter currency.
type Money struct {
	Amount   decimal.Decimal
	Currency string
}

// NewMoney checks the amount and upper-cases the currency.
func NewMoney(amount decimal.Decimal, currency string) (Money, error) {
	if amount.IsNegative() {
		return Money{}, shared.InvalidParameter("amount", "it cannot be negative")
	}

	trimmed := strings.TrimSpace(currency)
	if len(trimmed) != CurrencyLength {
		return Money{}, shared.InvalidParameter("currency", "it must be a three-letter code")
	}

	return Money{Amount: amount, Currency: strings.ToUpper(trimmed)}, nil
}
