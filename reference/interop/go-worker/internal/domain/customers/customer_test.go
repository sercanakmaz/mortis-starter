package customers_test

import (
	"testing"

	"boilerplate/internal/domain/customers"
	"boilerplate/internal/shared"
)

func TestNew_StoresTheEmailTrimmedAndLowerCased(t *testing.T) {
	customer := customers.New(shared.NewID(), "Ada", "  Ada@Example.COM ", shared.AuditUser{})

	if customer.Email != "ada@example.com" {
		t.Fatalf("got %q", customer.Email)
	}
}

func TestRegisterCompletedOrder_WhenBlocked_FailsCustomerBlocked(t *testing.T) {
	customer := customers.New(shared.NewID(), "Ada", "ada@example.com", shared.AuditUser{})
	customer.Block()

	err := customer.RegisterCompletedOrder()

	if failure := shared.AsError(err); failure == nil || failure.Code != customers.BlockedCode || customer.CompletedOrderCount != 0 {
		t.Fatalf("got %v", err)
	}
}
