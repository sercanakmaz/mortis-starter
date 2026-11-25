package categories

import "context"

// Repository stores categories.
type Repository interface {
	Create(ctx context.Context, category *Category) error
	FindByID(ctx context.Context, id int) (*Category, error)
}
