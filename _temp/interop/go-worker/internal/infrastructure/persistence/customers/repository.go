package customers

import (
	"context"
	"errors"

	domain "boilerplate/internal/domain/customers"
	"boilerplate/internal/infrastructure/persistence"

	"github.com/google/uuid"
	"github.com/jackc/pgx/v5"
)

const columns = "id, name, email, is_blocked, completed_order_count, created_at, created_user_id, created_user_name, updated_at, updated_user_id, updated_user_name"

// Repository stores customers in the customers table.
type Repository struct {
	Database *persistence.Database
}

// Create inserts a customer.
func (r Repository) Create(ctx context.Context, customer *domain.Customer) error {
	_, err := r.Database.On(ctx).Exec(ctx, "insert into customers ("+columns+") values ($1, $2, $3, $4, $5, $6, $7, $8, $9, $10, $11)",
		customer.ID, customer.Name, customer.Email, customer.IsBlocked, customer.CompletedOrderCount, customer.CreatedAt, customer.CreatedUserID, customer.CreatedUserName, customer.UpdatedAt, customer.UpdatedUserID, customer.UpdatedUserName)
	if err != nil {
		return err
	}

	persistence.RaiseEvents(ctx, customer)

	return nil
}

// Update writes a customer's changes.
func (r Repository) Update(ctx context.Context, customer *domain.Customer) error {
	_, err := r.Database.On(ctx).Exec(ctx, "update customers set name = $2, email = $3, is_blocked = $4, completed_order_count = $5, updated_at = $6, updated_user_id = $7, updated_user_name = $8 where id = $1",
		customer.ID, customer.Name, customer.Email, customer.IsBlocked, customer.CompletedOrderCount, customer.UpdatedAt, customer.UpdatedUserID, customer.UpdatedUserName)
	if err != nil {
		return err
	}

	persistence.RaiseEvents(ctx, customer)

	return nil
}

// FindByID reads a customer, or nil.
func (r Repository) FindByID(ctx context.Context, id uuid.UUID) (*domain.Customer, error) {
	return r.one(ctx, "select "+columns+" from customers where id = $1", id)
}

// FindByIDForUpdate reads a customer and locks its row, or nil.
func (r Repository) FindByIDForUpdate(ctx context.Context, id uuid.UUID) (*domain.Customer, error) {
	return r.one(ctx, "select "+columns+" from customers where id = $1 for update", id)
}

func (r Repository) one(ctx context.Context, sql string, id uuid.UUID) (*domain.Customer, error) {
	customer := &domain.Customer{}

	err := r.Database.On(ctx).QueryRow(ctx, sql, id).Scan(&customer.ID, &customer.Name, &customer.Email, &customer.IsBlocked, &customer.CompletedOrderCount,
		&customer.CreatedAt, &customer.CreatedUserID, &customer.CreatedUserName, &customer.UpdatedAt, &customer.UpdatedUserID, &customer.UpdatedUserName)
	if errors.Is(err, pgx.ErrNoRows) {
		return nil, nil
	}

	if err != nil {
		return nil, err
	}

	return customer, nil
}
