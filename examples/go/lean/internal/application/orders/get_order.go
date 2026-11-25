package orders

import (
	"context"

	domain "delta/internal/domain/orders"
	"delta/internal/shared"

	"github.com/google/uuid"
)

// GetOrderCommand reads one order with its lines.
type GetOrderCommand struct {
	shared.WithContext
	ID uuid.UUID `json:"Id"`
}

// GetOrderHandler runs GetOrderCommand.
type GetOrderHandler struct {
	Orders domain.Repository
}

// Handle reads the order.
func (h GetOrderHandler) Handle(ctx context.Context, command *GetOrderCommand) (OrderDto, error) {
	order, err := h.Orders.FindByID(ctx, command.ID)
	if err != nil {
		return OrderDto{}, err
	}

	if order == nil {
		return OrderDto{}, shared.NotFound("Order")
	}

	return MapOrder(order), nil
}
