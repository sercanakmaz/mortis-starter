package customers

import (
	"context"

	domain "delta/internal/domain/customers"
	"delta/internal/shared"

	"github.com/google/uuid"
)

// BlockCustomerCommand stops a customer from completing orders.
type BlockCustomerCommand struct {
	shared.WithContext
	shared.InTransaction
	ID uuid.UUID `json:"Id"`
}

// BlockCustomerHandler runs BlockCustomerCommand.
type BlockCustomerHandler struct {
	Customers domain.Repository
}

// Handle blocks the customer.
func (h BlockCustomerHandler) Handle(ctx context.Context, command *BlockCustomerCommand) (CustomerDto, error) {
	customer, err := h.Customers.FindByIDForUpdate(ctx, command.ID)
	if err != nil {
		return CustomerDto{}, err
	}

	if customer == nil {
		return CustomerDto{}, shared.NotFound("Customer")
	}

	customer.Block()
	customer.SetUpdatedAudit(command.Context.User)

	if err := h.Customers.Update(ctx, customer); err != nil {
		return CustomerDto{}, err
	}

	return MapCustomer(customer), nil
}
