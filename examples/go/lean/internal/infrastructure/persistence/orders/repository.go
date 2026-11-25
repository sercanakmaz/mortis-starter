package orders

import (
	"context"
	"errors"

	domain "delta/internal/domain/orders"
	"delta/internal/infrastructure/persistence"

	"github.com/google/uuid"
	"github.com/jackc/pgx/v5"
	"github.com/shopspring/decimal"
)

const columns = "id, order_number, customer_id, status, created_at, created_user_id, created_user_name, updated_at, updated_user_id, updated_user_name"

// Repository stores orders in the orders table and their lines in order_lines.
type Repository struct {
	Database *persistence.Database
}

// Create inserts an order with its lines.
func (r Repository) Create(ctx context.Context, order *domain.Order) error {
	_, err := r.Database.On(ctx).Exec(ctx, "insert into orders ("+columns+") values ($1, $2, $3, $4, $5, $6, $7, $8, $9, $10)",
		order.ID, order.OrderNumber, order.CustomerID, string(order.Status), order.CreatedAt, order.CreatedUserID, order.CreatedUserName, order.UpdatedAt, order.UpdatedUserID, order.UpdatedUserName)
	if err != nil {
		return err
	}

	if err := r.writeLines(ctx, order); err != nil {
		return err
	}

	persistence.RaiseEvents(ctx, order)

	return nil
}

// Update writes an order's changes; a new line is inserted and a changed one updated in place.
func (r Repository) Update(ctx context.Context, order *domain.Order) error {
	_, err := r.Database.On(ctx).Exec(ctx, "update orders set status = $2, updated_at = $3, updated_user_id = $4, updated_user_name = $5 where id = $1",
		order.ID, string(order.Status), order.UpdatedAt, order.UpdatedUserID, order.UpdatedUserName)
	if err != nil {
		return err
	}

	if err := r.writeLines(ctx, order); err != nil {
		return err
	}

	persistence.RaiseEvents(ctx, order)

	return nil
}

// FindByID reads an order with its lines, or nil.
func (r Repository) FindByID(ctx context.Context, id uuid.UUID) (*domain.Order, error) {
	return r.one(ctx, "select "+columns+" from orders where id = $1", id)
}

// FindByIDForUpdate reads an order with its lines and locks the order's row, or nil.
func (r Repository) FindByIDForUpdate(ctx context.Context, id uuid.UUID) (*domain.Order, error) {
	return r.one(ctx, "select "+columns+" from orders where id = $1 for update", id)
}

func (r Repository) writeLines(ctx context.Context, order *domain.Order) error {
	for _, line := range order.Lines {
		var reason *string
		if line.CancellationReason != "" {
			reason = &line.CancellationReason
		}

		_, err := r.Database.On(ctx).Exec(ctx, `insert into order_lines (id, order_id, sku, quantity, is_cancelled, cancellation_reason, price_amount, price_currency)
values ($1, $2, $3, $4, $5, $6, $7::numeric, $8)
on conflict (id) do update set quantity = excluded.quantity, is_cancelled = excluded.is_cancelled, cancellation_reason = excluded.cancellation_reason`,
			line.ID, line.OrderID, line.SKU, line.Quantity, line.IsCancelled, reason, line.Price.Amount.String(), line.Price.Currency)
		if err != nil {
			return err
		}
	}

	return nil
}

func (r Repository) one(ctx context.Context, sql string, id uuid.UUID) (*domain.Order, error) {
	order := &domain.Order{}

	var status string

	err := r.Database.On(ctx).QueryRow(ctx, sql, id).Scan(&order.ID, &order.OrderNumber, &order.CustomerID, &status,
		&order.CreatedAt, &order.CreatedUserID, &order.CreatedUserName, &order.UpdatedAt, &order.UpdatedUserID, &order.UpdatedUserName)
	if errors.Is(err, pgx.ErrNoRows) {
		return nil, nil
	}

	if err != nil {
		return nil, err
	}

	order.Status = domain.Status(status)

	rows, err := r.Database.On(ctx).Query(ctx, "select id, order_id, sku, quantity, is_cancelled, coalesce(cancellation_reason, ''), price_amount::text, price_currency from order_lines where order_id = $1 order by id", id)
	if err != nil {
		return nil, err
	}
	defer rows.Close()

	for rows.Next() {
		line := &domain.Line{}

		var amount string

		if err := rows.Scan(&line.ID, &line.OrderID, &line.SKU, &line.Quantity, &line.IsCancelled, &line.CancellationReason, &amount, &line.Price.Currency); err != nil {
			return nil, err
		}

		line.Price.Amount, err = decimal.NewFromString(amount)
		if err != nil {
			return nil, err
		}

		order.Lines = append(order.Lines, line)
	}

	return order, rows.Err()
}
