package orders

import (
	"context"

	domain "delta/internal/domain/orders"
	"delta/internal/shared"

	"github.com/google/uuid"
)

// CompleteOrderCommand fulfils an open order.
type CompleteOrderCommand struct {
	shared.WithContext
	shared.InTransaction
	ID uuid.UUID `json:"Id"`
}

// CompleteOrderHandler runs CompleteOrderCommand.
type CompleteOrderHandler struct {
	Orders domain.Repository
}

// Handle completes the order.
func (h CompleteOrderHandler) Handle(ctx context.Context, command *CompleteOrderCommand) (OrderDto, error) {
	order, err := LoadForUpdate(ctx, h.Orders, command.ID)
	if err != nil {
		return OrderDto{}, err
	}

	if err := order.Complete(); err != nil {
		return OrderDto{}, err
	}

	return Save(ctx, h.Orders, order, command.Context.User)
}
