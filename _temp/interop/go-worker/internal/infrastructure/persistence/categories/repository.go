package categories

import (
	"context"
	"errors"

	domain "boilerplate/internal/domain/categories"
	"boilerplate/internal/infrastructure/persistence"

	"github.com/jackc/pgx/v5"
)

// Repository stores categories in the categories table; the database assigns the id.
type Repository struct {
	Database *persistence.Database
}

// Create inserts a category, takes the id the database assigned, then marks it created.
func (r Repository) Create(ctx context.Context, category *domain.Category) error {
	err := r.Database.On(ctx).QueryRow(ctx, "insert into categories (name, created_at, created_user_id, created_user_name, updated_at, updated_user_id, updated_user_name) values ($1, $2, $3, $4, $5, $6, $7) returning id",
		category.Name, category.CreatedAt, category.CreatedUserID, category.CreatedUserName, category.UpdatedAt, category.UpdatedUserID, category.UpdatedUserName).Scan(&category.ID)
	if err != nil {
		return err
	}

	if err := category.MarkCreated(); err != nil {
		return err
	}

	persistence.RaiseEvents(ctx, category)

	return nil
}

// FindByID reads a category, or nil.
func (r Repository) FindByID(ctx context.Context, id int) (*domain.Category, error) {
	category := &domain.Category{}

	err := r.Database.On(ctx).QueryRow(ctx, "select id, name, created_at, created_user_id, created_user_name, updated_at, updated_user_id, updated_user_name from categories where id = $1", id).
		Scan(&category.ID, &category.Name, &category.CreatedAt, &category.CreatedUserID, &category.CreatedUserName, &category.UpdatedAt, &category.UpdatedUserID, &category.UpdatedUserName)
	if errors.Is(err, pgx.ErrNoRows) {
		return nil, nil
	}

	if err != nil {
		return nil, err
	}

	return category, nil
}
