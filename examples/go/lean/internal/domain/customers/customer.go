package customers

import (
	"strings"

	"delta/internal/shared"

	"github.com/google/uuid"
)

// Customer is the aggregate root of a buyer: a name, a unique email, and the orders it completed.
type Customer struct {
	shared.AggregateRoot
	ID                  uuid.UUID
	Name                string
	Email               string
	IsBlocked           bool
	CompletedOrderCount int
}

// New creates a customer; the email is stored trimmed and lower-cased.
func New(id uuid.UUID, name string, email string, user shared.AuditUser) *Customer {
	customer := &Customer{ID: id, Name: name, Email: strings.ToLower(strings.TrimSpace(email))}
	customer.SetCreatedAudit(user)
	customer.Raise(&Created{EventBase: shared.NewEventBase(), ID: id.String(), Email: customer.Email})

	return customer
}

// Block stops the customer from completing orders.
func (c *Customer) Block() {
	c.IsBlocked = true
	c.Raise(&Blocked{EventBase: shared.NewEventBase(), ID: c.ID.String()})
}

// RegisterCompletedOrder counts one more completed order.
func (c *Customer) RegisterCompletedOrder() error {
	if c.IsBlocked {
		return shared.BusinessRule(BlockedCode, "A blocked customer cannot complete orders.")
	}

	c.CompletedOrderCount++

	return nil
}
