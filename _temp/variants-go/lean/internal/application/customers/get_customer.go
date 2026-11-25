package customers

import (
	"context"

	domain "boilerplate/internal/domain/customers"
	"boilerplate/internal/shared"

	"github.com/google/uuid"
)

// GetCustomerCommand reads one customer.
type GetCustomerCommand struct {
	shared.WithContext
	ID uuid.UUID `json:"Id"`
}

// GetCustomerHandler runs GetCustomerCommand.
type GetCustomerHandler struct {
	Customers domain.Repository
}

// Handle reads the customer.
func (h GetCustomerHandler) Handle(ctx context.Context, command *GetCustomerCommand) (CustomerDto, error) {
	customer, err := h.Customers.FindByID(ctx, command.ID)
	if err != nil {
		return CustomerDto{}, err
	}

	if customer == nil {
		return CustomerDto{}, shared.NotFound("Customer")
	}

	return MapCustomer(customer), nil
}
