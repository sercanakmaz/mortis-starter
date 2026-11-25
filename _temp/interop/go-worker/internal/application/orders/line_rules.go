package orders

import (
	domain "boilerplate/internal/domain/orders"
	"boilerplate/internal/shared"
)

// ValidateLine checks the shape of one order line under a field prefix.
func ValidateLine(fieldErrors shared.FieldErrors, prefix string, line domain.LineModel) {
	fieldErrors.NotEmpty(prefix+"Sku", line.SKU)
	fieldErrors.MaxLength(prefix+"Sku", line.SKU, domain.SKUMaxLength)
	fieldErrors.Check(!line.Price.IsNegative(), prefix+"Price", "'Price' must be greater than or equal to '0'.")
	fieldErrors.Check(len(line.Currency) == domain.CurrencyLength, prefix+"Currency", "'Currency' must be 3 characters in length.")
	fieldErrors.Check(line.Quantity > 0, prefix+"Quantity", "'Quantity' must be greater than '0'.")
}
