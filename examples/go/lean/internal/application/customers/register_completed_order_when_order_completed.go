package customers

import (
	"context"

	"delta/internal/domain/orders"
	"delta/internal/shared"

	"github.com/google/uuid"
)

// RegisterCompletedOrderWhenOrderCompletedHandler counts the order in the transaction that completed it.
type RegisterCompletedOrderWhenOrderCompletedHandler struct {
	Dispatcher *shared.Dispatcher
}

// Handle sends RegisterCompletedOrderCommand; its failure rolls the completion back.
func (h RegisterCompletedOrderWhenOrderCompletedHandler) Handle(ctx context.Context, event shared.Event) error {
	completed := event.(*orders.Completed)

	customerID, err := uuid.Parse(completed.CustomerID)
	if err != nil {
		return err
	}

	_, err = shared.Send[CustomerDto](ctx, h.Dispatcher, &RegisterCompletedOrderCommand{WithContext: shared.WithContext{Context: shared.FromEvent(event)}, ID: customerID})

	return err
}
