package orders

import (
	"context"

	domain "boilerplate/internal/domain/orders"
	"boilerplate/internal/shared"

	"github.com/google/uuid"
)

// CancelOrderWhenAllLinesCancelledCommand cancels an order whose last active line was cancelled.
type CancelOrderWhenAllLinesCancelledCommand struct {
	shared.WithContext
	shared.InTransaction
	ID uuid.UUID `json:"Id"`
}

// CancelOrderWhenAllLinesCancelledHandler runs CancelOrderWhenAllLinesCancelledCommand.
type CancelOrderWhenAllLinesCancelledHandler struct {
	Orders domain.Repository
}

// Handle cancels the order when every line is cancelled, and writes nothing otherwise.
func (h CancelOrderWhenAllLinesCancelledHandler) Handle(ctx context.Context, command *CancelOrderWhenAllLinesCancelledCommand) (OrderDto, error) {
	order, err := LoadForUpdate(ctx, h.Orders, command.ID)
	if err != nil {
		return OrderDto{}, err
	}

	if !order.CancelWhenAllLinesCancelled() {
		return MapOrder(order), nil
	}

	return Save(ctx, h.Orders, order, command.Context.User)
}
