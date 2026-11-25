package customers

import (
	"context"

	domain "delta/internal/domain/customers"
	"delta/internal/shared"

	"github.com/google/uuid"
)

// RegisterCompletedOrderCommand counts an order the customer completed.
type RegisterCompletedOrderCommand struct {
	shared.WithContext
	shared.InTransaction
	ID uuid.UUID `json:"Id"`
}

// RegisterCompletedOrderHandler runs RegisterCompletedOrderCommand.
type RegisterCompletedOrderHandler struct {
	Customers domain.Repository
}

// Handle counts the order; a blocked customer refuses it.
func (h RegisterCompletedOrderHandler) Handle(ctx context.Context, command *RegisterCompletedOrderCommand) (CustomerDto, error) {
	customer, err := h.Customers.FindByIDForUpdate(ctx, command.ID)
	if err != nil {
		return CustomerDto{}, err
	}

	if customer == nil {
		return CustomerDto{}, shared.NotFound("Customer")
	}

	if err := customer.RegisterCompletedOrder(); err != nil {
		return CustomerDto{}, err
	}

	customer.SetUpdatedAudit(command.Context.User)

	if err := h.Customers.Update(ctx, customer); err != nil {
		return CustomerDto{}, err
	}

	return MapCustomer(customer), nil
}
