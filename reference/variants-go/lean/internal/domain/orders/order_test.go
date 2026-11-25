package orders_test

import (
	"testing"

	"boilerplate/internal/domain/orders"
	"boilerplate/internal/shared"

	"github.com/shopspring/decimal"
)

func newOrder(t *testing.T) *orders.Order {
	t.Helper()

	order := orders.New(shared.NewID(), "A-1", shared.NewID(), shared.AuditUser{})
	price, _ := orders.NewMoney(decimal.RequireFromString("10.50"), "eur")

	if _, err := order.AddLine("SKU-1", price, 2); err != nil {
		t.Fatal(err)
	}

	order.ClearEvents()

	return order
}

func codeOf(err error) string {
	if failure := shared.AsError(err); failure != nil {
		return failure.Code
	}

	return ""
}

func TestAddLine_WithTheSameActiveSku_FailsDuplicate(t *testing.T) {
	order := newOrder(t)
	price, _ := orders.NewMoney(decimal.NewFromInt(1), "EUR")

	_, err := order.AddLine("SKU-1", price, 1)

	if codeOf(err) != "duplicate" {
		t.Fatalf("got %v", err)
	}
}

func TestAddLine_WithAnotherCurrency_FailsMixedCurrency(t *testing.T) {
	order := newOrder(t)
	price, _ := orders.NewMoney(decimal.NewFromInt(1), "USD")

	_, err := order.AddLine("SKU-2", price, 1)

	if codeOf(err) != orders.MixedCurrencyCode {
		t.Fatalf("got %v", err)
	}
}

func TestTotalAmount_LeavesOutCancelledLines(t *testing.T) {
	order := newOrder(t)
	price, _ := orders.NewMoney(decimal.NewFromInt(4), "EUR")
	line, _ := order.AddLine("SKU-2", price, 1)

	_ = order.CancelLine(line.ID, "not needed")

	if !order.TotalAmount().Equal(decimal.NewFromInt(21)) {
		t.Fatalf("got %s", order.TotalAmount())
	}
}

func TestCancelWhenAllLinesCancelled_CancelsOnlyWhenNoLineIsActive(t *testing.T) {
	order := newOrder(t)

	if order.CancelWhenAllLinesCancelled() {
		t.Fatal("an order with an active line was cancelled")
	}

	_ = order.CancelLine(order.Lines[0].ID, "gone")

	if !order.CancelWhenAllLinesCancelled() || order.Status != orders.StatusCancelled {
		t.Fatalf("got %s", order.Status)
	}
}

func TestComplete_WithoutAnActiveLine_FailsNothingToFulfil(t *testing.T) {
	order := newOrder(t)
	_ = order.CancelLine(order.Lines[0].ID, "gone")

	if err := order.Complete(); codeOf(err) != orders.NothingToFulfilCode {
		t.Fatalf("got %v", err)
	}
}

func TestComplete_WhenNotOpen_FailsNotOpen(t *testing.T) {
	order := newOrder(t)
	_ = order.Complete()

	if err := order.Complete(); codeOf(err) != orders.NotOpenCode {
		t.Fatalf("got %v", err)
	}
}
