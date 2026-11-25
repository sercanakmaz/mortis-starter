package orders

import (
	"context"

	domain "delta/internal/domain/orders"
	"delta/internal/shared"

	"github.com/google/uuid"
)

// AddOrderLineCommand adds a product to an open order.
type AddOrderLineCommand struct {
	shared.WithContext
	shared.InTransaction
	ID   uuid.UUID         `json:"Id"`
	Line *domain.LineModel `json:"Line"`
}

// Validate checks the command's shape.
func (c *AddOrderLineCommand) Validate() shared.FieldErrors {
	fieldErrors := shared.FieldErrors{}
	fieldErrors.Check(c.ID != uuid.Nil, "Id", "'Id' must not be empty.")
	fieldErrors.Check(c.Line != nil, "Line", "'Line' must not be empty.")

	if c.Line != nil {
		ValidateLine(fieldErrors, "Line.", *c.Line)
	}

	return fieldErrors
}

// AddOrderLineHandler runs AddOrderLineCommand.
type AddOrderLineHandler struct {
	Orders domain.Repository
}

// Handle adds the line.
func (h AddOrderLineHandler) Handle(ctx context.Context, command *AddOrderLineCommand) (OrderDto, error) {
	order, err := LoadForUpdate(ctx, h.Orders, command.ID)
	if err != nil {
		return OrderDto{}, err
	}

	price, err := domain.NewMoney(command.Line.Price, command.Line.Currency)
	if err != nil {
		return OrderDto{}, err
	}

	if _, err := order.AddLine(command.Line.SKU, price, command.Line.Quantity); err != nil {
		return OrderDto{}, err
	}

	return Save(ctx, h.Orders, order, command.Context.User)
}
