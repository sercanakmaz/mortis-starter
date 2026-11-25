package products

import (
	"context"

	"github.com/google/uuid"
)

// Repository stores products. The ForUpdate reads lock the row until the transaction ends.
type Repository interface {
	Create(ctx context.Context, product *Product) error
	Update(ctx context.Context, product *Product) error
	FindByID(ctx context.Context, id uuid.UUID) (*Product, error)
	FindBySKUForUpdate(ctx context.Context, sku string) (*Product, error)
}
