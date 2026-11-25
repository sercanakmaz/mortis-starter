package products

import (
	"context"

	"boilerplate/internal/domain/orders"
	"boilerplate/internal/shared"
)

// IncreaseStockWhenOrderLineCancelledConsumer gives the units of a cancelled line back to the stock.
type IncreaseStockWhenOrderLineCancelledConsumer struct {
	Dispatcher *shared.Dispatcher
}

// Handle sends IncreaseProductStockCommand as the user whose command cancelled the line.
func (c IncreaseStockWhenOrderLineCancelledConsumer) Handle(ctx context.Context, event shared.Event) error {
	lineCancelled := event.(*orders.LineCancelled)

	_, err := shared.Send[ProductDto](ctx, c.Dispatcher, &IncreaseProductStockCommand{WithContext: shared.WithContext{Context: shared.FromEvent(event)}, SKU: lineCancelled.SKU, Quantity: lineCancelled.Quantity})

	return err
}
