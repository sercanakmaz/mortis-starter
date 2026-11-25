package products

import (
	"context"

	"delta/internal/domain/orders"
	"delta/internal/shared"
)

// IncreaseStockWhenOrderLineCancelledConsumer gives the cancelled units back to the stock after the order committed.
type IncreaseStockWhenOrderLineCancelledConsumer struct {
	Dispatcher *shared.Dispatcher
}

// Handle sends IncreaseProductStockCommand as the user whose command cancelled the line.
func (c IncreaseStockWhenOrderLineCancelledConsumer) Handle(ctx context.Context, event shared.Event) error {
	lineCancelled := event.(*orders.LineCancelled)

	_, err := shared.Send[ProductDto](ctx, c.Dispatcher, &IncreaseProductStockCommand{WithContext: shared.WithContext{Context: shared.FromEvent(event)}, SKU: lineCancelled.SKU, Quantity: lineCancelled.Quantity})

	return err
}
