package customers

import (
	domain "boilerplate/internal/domain/customers"

	"github.com/google/uuid"
)

// CustomerDto is how a customer is shown.
type CustomerDto struct {
	ID                  uuid.UUID `json:"Id"`
	Name                string    `json:"Name"`
	Email               string    `json:"Email"`
	IsBlocked           bool      `json:"IsBlocked"`
	CompletedOrderCount int       `json:"CompletedOrderCount"`
}

// MapCustomer shows a customer.
func MapCustomer(customer *domain.Customer) CustomerDto {
	return CustomerDto{ID: customer.ID, Name: customer.Name, Email: customer.Email, IsBlocked: customer.IsBlocked, CompletedOrderCount: customer.CompletedOrderCount}
}
