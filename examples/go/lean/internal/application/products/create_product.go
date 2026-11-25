package products

import (
	"context"

	domain "delta/internal/domain/products"
	"delta/internal/shared"

	"github.com/google/uuid"
)

// CreateProductCommand creates a product with its initial stock.
type CreateProductCommand struct {
	shared.WithContext
	shared.InTransaction
	ID         uuid.UUID `json:"Id"`
	SKU        string    `json:"Sku"`
	Name       string    `json:"Name"`
	Stock      int       `json:"Stock"`
	CategoryID *int      `json:"CategoryId"`
}

// Validate checks the command's shape.
func (c *CreateProductCommand) Validate() shared.FieldErrors {
	fieldErrors := shared.FieldErrors{}
	fieldErrors.NotEmpty("Sku", c.SKU)
	fieldErrors.MaxLength("Sku", c.SKU, domain.SKUMaxLength)
	fieldErrors.NotEmpty("Name", c.Name)
	fieldErrors.MaxLength("Name", c.Name, domain.NameMaxLength)
	fieldErrors.Check(c.Stock >= 0, "Stock", "'Stock' must be greater than or equal to '0'.")
	fieldErrors.Check(c.CategoryID == nil || *c.CategoryID > 0, "CategoryId", "'CategoryId' must be greater than '0'.")

	return fieldErrors
}

// CreateProductHandler runs CreateProductCommand.
type CreateProductHandler struct {
	Products domain.Repository
}

// Handle creates the product.
func (h CreateProductHandler) Handle(ctx context.Context, command *CreateProductCommand) (ProductDto, error) {
	if command.ID == uuid.Nil {
		command.ID = shared.NewID()
	}

	product, err := domain.New(command.ID, command.SKU, command.Name, command.Stock, command.Context.User, command.CategoryID)
	if err != nil {
		return ProductDto{}, err
	}

	if err := h.Products.Create(ctx, product); err != nil {
		return ProductDto{}, err
	}

	return MapProduct(product), nil
}
