package orders

import (
	"context"
	"fmt"

	domain "boilerplate/internal/domain/orders"
	"boilerplate/internal/shared"

	"github.com/google/uuid"
)

// CreateOrderCommand opens an order with its lines.
type CreateOrderCommand struct {
	shared.WithContext
	shared.InTransaction
	ID          uuid.UUID          `json:"Id"`
	OrderNumber string             `json:"OrderNumber"`
	CustomerID  uuid.UUID          `json:"CustomerId"`
	Lines       []domain.LineModel `json:"Lines"`
}

// Validate checks the command's shape.
func (c *CreateOrderCommand) Validate() shared.FieldErrors {
	fieldErrors := shared.FieldErrors{}
	fieldErrors.NotEmpty("OrderNumber", c.OrderNumber)
	fieldErrors.MaxLength("OrderNumber", c.OrderNumber, domain.OrderNumberMaxLength)
	fieldErrors.Check(c.CustomerID != uuid.Nil, "CustomerId", "'CustomerId' must not be empty.")
	fieldErrors.Check(len(c.Lines) > 0, "Lines", "'Lines' must not be empty.")

	for index, line := range c.Lines {
		ValidateLine(fieldErrors, fmt.Sprintf("Lines[%d].", index), line)
	}

	return fieldErrors
}

// CreateOrderHandler runs CreateOrderCommand.
type CreateOrderHandler struct {
	Orders domain.Repository
}

// Handle opens the order and adds its lines.
func (h CreateOrderHandler) Handle(ctx context.Context, command *CreateOrderCommand) (OrderDto, error) {
	if command.ID == uuid.Nil {
		command.ID = shared.NewID()
	}

	order := domain.New(command.ID, command.OrderNumber, command.CustomerID, command.Context.User)

	for _, line := range command.Lines {
		price, err := domain.NewMoney(line.Price, line.Currency)
		if err != nil {
			return OrderDto{}, err
		}

		if _, err := order.AddLine(line.SKU, price, line.Quantity); err != nil {
			return OrderDto{}, err
		}
	}

	if err := h.Orders.Create(ctx, order); err != nil {
		return OrderDto{}, err
	}

	return MapOrder(order), nil
}
