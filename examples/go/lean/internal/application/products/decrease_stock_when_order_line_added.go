package products

import (
	"context"

	"delta/internal/domain/orders"
	"delta/internal/shared"
)

// DecreaseStockWhenOrderLineAddedConsumer takes the ordered units from the stock after the order committed.
type DecreaseStockWhenOrderLineAddedConsumer struct {
	Dispatcher *shared.Dispatcher
}

// Handle sends DecreaseProductStockCommand as the user whose command added the line.
func (c DecreaseStockWhenOrderLineAddedConsumer) Handle(ctx context.Context, event shared.Event) error {
	lineAdded := event.(*orders.LineAdded)

	_, err := shared.Send[ProductDto](ctx, c.Dispatcher, &DecreaseProductStockCommand{WithContext: shared.WithContext{Context: shared.FromEvent(event)}, SKU: lineAdded.SKU, Quantity: lineAdded.Quantity})

	return err
}
