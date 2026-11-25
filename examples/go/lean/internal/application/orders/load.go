package orders

import (
	"context"

	domain "delta/internal/domain/orders"
	"delta/internal/shared"

	"github.com/google/uuid"
)

// LoadForUpdate reads an order to change it, or fails with not_found.
func LoadForUpdate(ctx context.Context, orders domain.Repository, id uuid.UUID) (*domain.Order, error) {
	order, err := orders.FindByIDForUpdate(ctx, id)
	if err != nil {
		return nil, err
	}

	if order == nil {
		return nil, shared.NotFound("Order")
	}

	return order, nil
}

// Save records who changed the order and stores it.
func Save(ctx context.Context, orders domain.Repository, order *domain.Order, user shared.AuditUser) (OrderDto, error) {
	order.SetUpdatedAudit(user)

	if err := orders.Update(ctx, order); err != nil {
		return OrderDto{}, err
	}

	return MapOrder(order), nil
}
