package orders

import (
	"context"

	"github.com/google/uuid"
)

// Repository stores orders with their lines. The ForUpdate read locks the order until the transaction ends.
type Repository interface {
	Create(ctx context.Context, order *Order) error
	Update(ctx context.Context, order *Order) error
	FindByID(ctx context.Context, id uuid.UUID) (*Order, error)
	FindByIDForUpdate(ctx context.Context, id uuid.UUID) (*Order, error)
}
