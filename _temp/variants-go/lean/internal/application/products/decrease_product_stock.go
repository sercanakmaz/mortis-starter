package products

import (
	"context"

	domain "boilerplate/internal/domain/products"
	"boilerplate/internal/shared"
)

// DecreaseProductStockCommand decreases the stock of the product with a SKU.
type DecreaseProductStockCommand struct {
	shared.WithContext
	shared.InTransaction
	SKU      string `json:"Sku"`
	Quantity int    `json:"Quantity"`
}

// Validate checks the command's shape.
func (c *DecreaseProductStockCommand) Validate() shared.FieldErrors {
	fieldErrors := shared.FieldErrors{}
	fieldErrors.NotEmpty("Sku", c.SKU)
	fieldErrors.MaxLength("Sku", c.SKU, domain.SKUMaxLength)
	fieldErrors.Check(c.Quantity > 0, "Quantity", "'Quantity' must be greater than '0'.")

	return fieldErrors
}

// DecreaseProductStockHandler runs DecreaseProductStockCommand.
type DecreaseProductStockHandler struct {
	Products domain.Repository
}

// Handle changes the stock, holding the product's row until the transaction ends.
func (h DecreaseProductStockHandler) Handle(ctx context.Context, command *DecreaseProductStockCommand) (ProductDto, error) {
	product, err := h.Products.FindBySKUForUpdate(ctx, command.SKU)
	if err != nil {
		return ProductDto{}, err
	}

	if product == nil {
		return ProductDto{}, shared.NotFound("Product")
	}

	if err := product.DecreaseStock(command.Quantity); err != nil {
		return ProductDto{}, err
	}

	product.SetUpdatedAudit(command.Context.User)

	if err := h.Products.Update(ctx, product); err != nil {
		return ProductDto{}, err
	}

	return MapProduct(product), nil
}
