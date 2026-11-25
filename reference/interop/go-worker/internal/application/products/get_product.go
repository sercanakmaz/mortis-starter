package products

import (
	"context"

	domain "boilerplate/internal/domain/products"
	"boilerplate/internal/shared"

	"github.com/google/uuid"
)

// GetProductCommand reads one product.
type GetProductCommand struct {
	shared.WithContext
	ID uuid.UUID `json:"Id"`
}

// GetProductHandler runs GetProductCommand.
type GetProductHandler struct {
	Products domain.Repository
}

// Handle reads the product.
func (h GetProductHandler) Handle(ctx context.Context, command *GetProductCommand) (ProductDto, error) {
	product, err := h.Products.FindByID(ctx, command.ID)
	if err != nil {
		return ProductDto{}, err
	}

	if product == nil {
		return ProductDto{}, shared.NotFound("Product")
	}

	return MapProduct(product), nil
}
