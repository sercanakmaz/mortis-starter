package products

import (
	domain "boilerplate/internal/domain/products"

	"github.com/google/uuid"
)

// ProductDto is how a product is shown.
type ProductDto struct {
	ID         uuid.UUID `json:"Id"`
	SKU        string    `json:"Sku"`
	Name       string    `json:"Name"`
	Stock      int       `json:"Stock"`
	CategoryID *int      `json:"CategoryId"`
}

// MapProduct shows a product.
func MapProduct(product *domain.Product) ProductDto {
	return ProductDto{ID: product.ID, SKU: product.SKU, Name: product.Name, Stock: product.Stock, CategoryID: product.CategoryID}
}
