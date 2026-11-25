package orders

import (
	"boilerplate/internal/shared"

	"github.com/google/uuid"
)

// Line is one product on an order; it lives only inside its order.
type Line struct {
	ID                 uuid.UUID
	OrderID            uuid.UUID
	SKU                string
	Price              Money
	Quantity           int
	IsCancelled        bool
	CancellationReason string
}

func (l *Line) cancel(reason string) error {
	if l.IsCancelled {
		return shared.BusinessRule(LineAlreadyCancelledCode, "The order line is already cancelled.")
	}

	l.IsCancelled = true
	l.CancellationReason = reason

	return nil
}
