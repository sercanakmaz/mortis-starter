package customers

import (
	"context"

	domain "boilerplate/internal/domain/customers"
	"boilerplate/internal/shared"

	"github.com/google/uuid"
)

// CreateCustomerCommand creates a customer.
type CreateCustomerCommand struct {
	shared.WithContext
	shared.InTransaction
	ID    uuid.UUID `json:"Id"`
	Name  string    `json:"Name"`
	Email string    `json:"Email"`
}

// Validate checks the command's shape.
func (c *CreateCustomerCommand) Validate() shared.FieldErrors {
	fieldErrors := shared.FieldErrors{}
	fieldErrors.NotEmpty("Name", c.Name)
	fieldErrors.MaxLength("Name", c.Name, domain.NameMaxLength)
	fieldErrors.NotEmpty("Email", c.Email)
	fieldErrors.MaxLength("Email", c.Email, domain.EmailMaxLength)

	return fieldErrors
}

// CreateCustomerHandler runs CreateCustomerCommand.
type CreateCustomerHandler struct {
	Customers domain.Repository
}

// Handle creates the customer.
func (h CreateCustomerHandler) Handle(ctx context.Context, command *CreateCustomerCommand) (CustomerDto, error) {
	if command.ID == uuid.Nil {
		command.ID = shared.NewID()
	}

	customer := domain.New(command.ID, command.Name, command.Email, command.Context.User)

	if err := h.Customers.Create(ctx, customer); err != nil {
		return CustomerDto{}, err
	}

	return MapCustomer(customer), nil
}
