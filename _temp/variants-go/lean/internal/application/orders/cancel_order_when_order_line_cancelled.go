package orders

import (
	"context"

	domain "boilerplate/internal/domain/orders"
	"boilerplate/internal/shared"

	"github.com/google/uuid"
)

// CancelOrderWhenOrderLineCancelledHandler cancels the order in the transaction that cancelled its last line.
type CancelOrderWhenOrderLineCancelledHandler struct {
	Dispatcher *shared.Dispatcher
}

// Handle sends CancelOrderWhenAllLinesCancelledCommand.
func (h CancelOrderWhenOrderLineCancelledHandler) Handle(ctx context.Context, event shared.Event) error {
	lineCancelled := event.(*domain.LineCancelled)

	orderID, err := uuid.Parse(lineCancelled.ID)
	if err != nil {
		return err
	}

	_, err = shared.Send[OrderDto](ctx, h.Dispatcher, &CancelOrderWhenAllLinesCancelledCommand{WithContext: shared.WithContext{Context: shared.FromEvent(event)}, ID: orderID})

	return err
}
