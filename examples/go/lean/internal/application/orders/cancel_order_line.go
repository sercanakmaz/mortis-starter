package orders

import (
	"context"

	domain "delta/internal/domain/orders"
	"delta/internal/shared"

	"github.com/google/uuid"
)

// CancelOrderLineCommand cancels one line of an open order.
type CancelOrderLineCommand struct {
	shared.WithContext
	shared.InTransaction
	ID     uuid.UUID `json:"Id"`
	LineID uuid.UUID `json:"LineId"`
	Reason string    `json:"Reason"`
}

// Validate checks the command's shape.
func (c *CancelOrderLineCommand) Validate() shared.FieldErrors {
	fieldErrors := shared.FieldErrors{}
	fieldErrors.Check(c.ID != uuid.Nil, "Id", "'Id' must not be empty.")
	fieldErrors.Check(c.LineID != uuid.Nil, "LineId", "'LineId' must not be empty.")
	fieldErrors.NotEmpty("Reason", c.Reason)
	fieldErrors.MaxLength("Reason", c.Reason, domain.ReasonMaxLength)

	return fieldErrors
}

// CancelOrderLineHandler runs CancelOrderLineCommand.
type CancelOrderLineHandler struct {
	Orders domain.Repository
}

// Handle cancels the line.
func (h CancelOrderLineHandler) Handle(ctx context.Context, command *CancelOrderLineCommand) (OrderDto, error) {
	order, err := LoadForUpdate(ctx, h.Orders, command.ID)
	if err != nil {
		return OrderDto{}, err
	}

	if err := order.CancelLine(command.LineID, command.Reason); err != nil {
		return OrderDto{}, err
	}

	return Save(ctx, h.Orders, order, command.Context.User)
}
