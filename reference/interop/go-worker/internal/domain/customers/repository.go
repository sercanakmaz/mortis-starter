package customers

import (
	"context"

	"github.com/google/uuid"
)

// Repository stores customers. The ForUpdate read locks the row until the transaction ends.
type Repository interface {
	Create(ctx context.Context, customer *Customer) error
	Update(ctx context.Context, customer *Customer) error
	FindByID(ctx context.Context, id uuid.UUID) (*Customer, error)
	FindByIDForUpdate(ctx context.Context, id uuid.UUID) (*Customer, error)
}
