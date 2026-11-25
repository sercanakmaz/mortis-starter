package products

import (
	"context"
	"errors"

	domain "delta/internal/domain/products"
	"delta/internal/infrastructure/persistence"

	"github.com/google/uuid"
	"github.com/jackc/pgx/v5"
)

const columns = "id, sku, name, stock, category_id, created_at, created_user_id, created_user_name, updated_at, updated_user_id, updated_user_name"

// Repository stores products in the products table.
type Repository struct {
	Database *persistence.Database
}

// Create inserts a product.
func (r Repository) Create(ctx context.Context, product *domain.Product) error {
	_, err := r.Database.On(ctx).Exec(ctx, "insert into products ("+columns+") values ($1, $2, $3, $4, $5, $6, $7, $8, $9, $10, $11)",
		product.ID, product.SKU, product.Name, product.Stock, product.CategoryID, product.CreatedAt, product.CreatedUserID, product.CreatedUserName, product.UpdatedAt, product.UpdatedUserID, product.UpdatedUserName)
	if err != nil {
		return err
	}

	persistence.RaiseEvents(ctx, product)

	return nil
}

// Update writes a product's changes.
func (r Repository) Update(ctx context.Context, product *domain.Product) error {
	_, err := r.Database.On(ctx).Exec(ctx, "update products set name = $2, stock = $3, category_id = $4, updated_at = $5, updated_user_id = $6, updated_user_name = $7 where id = $1",
		product.ID, product.Name, product.Stock, product.CategoryID, product.UpdatedAt, product.UpdatedUserID, product.UpdatedUserName)
	if err != nil {
		return err
	}

	persistence.RaiseEvents(ctx, product)

	return nil
}

// FindByID reads a product, or nil.
func (r Repository) FindByID(ctx context.Context, id uuid.UUID) (*domain.Product, error) {
	return r.one(ctx, "select "+columns+" from products where id = $1", id)
}

// FindBySKUForUpdate reads a product by SKU and locks its row, or nil.
func (r Repository) FindBySKUForUpdate(ctx context.Context, sku string) (*domain.Product, error) {
	return r.one(ctx, "select "+columns+" from products where sku = $1 for update", sku)
}

func (r Repository) one(ctx context.Context, sql string, argument any) (*domain.Product, error) {
	product := &domain.Product{}

	err := r.Database.On(ctx).QueryRow(ctx, sql, argument).Scan(&product.ID, &product.SKU, &product.Name, &product.Stock, &product.CategoryID,
		&product.CreatedAt, &product.CreatedUserID, &product.CreatedUserName, &product.UpdatedAt, &product.UpdatedUserID, &product.UpdatedUserName)
	if errors.Is(err, pgx.ErrNoRows) {
		return nil, nil
	}

	if err != nil {
		return nil, err
	}

	return product, nil
}
