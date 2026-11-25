package application_test

import (
	"context"
	"testing"

	"delta/internal/application"
	appcustomers "delta/internal/application/customers"
	apporders "delta/internal/application/orders"
	"delta/internal/domain/customers"
	"delta/internal/domain/orders"
	"delta/internal/domain/products"
	"delta/internal/shared"
	"delta/internal/testsupport"

	"github.com/shopspring/decimal"
)

type harness struct {
	dispatcher *shared.Dispatcher
	unitOfWork *testsupport.UnitOfWork
	products   *testsupport.Products
	customers  *testsupport.Customers
	orders     *testsupport.Orders
}

func newHarness(t *testing.T) harness {
	t.Helper()

	product, _ := products.New(shared.NewID(), "SKU-1", "Bolt", 10, shared.AuditUser{}, nil)
	customer := customers.New(shared.NewID(), "Ada", "ada@example.com", shared.AuditUser{})
	events := shared.NewEventHandlers()
	unitOfWork := &testsupport.UnitOfWork{Events: events}
	h := harness{dispatcher: shared.NewDispatcher(unitOfWork), unitOfWork: unitOfWork, products: testsupport.NewProducts(product), customers: testsupport.NewCustomers(customer), orders: testsupport.NewOrders()}
	application.Register(h.dispatcher, events, application.Repositories{Products: h.products, Orders: h.orders, Customers: h.customers})

	return h
}

func (h harness) userContext() shared.CommandContext {
	return shared.CommandContext{User: shared.AuditUser{ID: "user-1"}}
}

func (h harness) createOrder(t *testing.T) apporders.OrderDto {
	t.Helper()

	var customerID [16]byte
	for id := range h.customers.Items {
		customerID = id
	}

	order, err := shared.Send[apporders.OrderDto](context.Background(), h.dispatcher, &apporders.CreateOrderCommand{
		WithContext: shared.WithContext{Context: h.userContext()},
		OrderNumber: "A-1",
		CustomerID:  customerID,
		Lines:       []orders.LineModel{{SKU: "SKU-1", Price: decimal.RequireFromString("10.50"), Currency: "EUR", Quantity: 2}},
	})
	if err != nil {
		t.Fatal(err)
	}

	return order
}

func TestCreateOrder_PublishesLineAddedForTheConsumersAfterTheCommand(t *testing.T) {
	h := newHarness(t)

	order := h.createOrder(t)

	names := []string{}
	for _, event := range h.unitOfWork.Published {
		names = append(names, event.EventName())
	}

	if order.TotalAmount.String() != "21" || len(names) != 2 || names[1] != (&orders.LineAdded{}).EventName() {
		t.Fatalf("got %s and %v", order.TotalAmount, names)
	}
}

func TestCompleteOrder_CountsTheOrderInTheSameUnitOfWork(t *testing.T) {
	h := newHarness(t)
	order := h.createOrder(t)

	_, err := shared.Send[apporders.OrderDto](context.Background(), h.dispatcher, &apporders.CompleteOrderCommand{WithContext: shared.WithContext{Context: h.userContext()}, ID: order.ID})

	if err != nil || h.customers.Items[order.CustomerID].CompletedOrderCount != 1 {
		t.Fatalf("got %v", err)
	}
}

func TestCompleteOrder_ForABlockedCustomer_FailsAndPublishesNothing(t *testing.T) {
	h := newHarness(t)
	order := h.createOrder(t)
	h.unitOfWork.Published = nil
	_, _ = shared.Send[appcustomers.CustomerDto](context.Background(), h.dispatcher, &appcustomers.BlockCustomerCommand{WithContext: shared.WithContext{Context: h.userContext()}, ID: order.CustomerID})
	h.unitOfWork.Published = nil

	_, err := shared.Send[apporders.OrderDto](context.Background(), h.dispatcher, &apporders.CompleteOrderCommand{WithContext: shared.WithContext{Context: h.userContext()}, ID: order.ID})

	if failure := shared.AsError(err); failure == nil || failure.Code != customers.BlockedCode || len(h.unitOfWork.Published) != 0 {
		t.Fatalf("got %v and %d published", err, len(h.unitOfWork.Published))
	}
}

func TestCancelOrderLine_OfTheLastActiveLine_CancelsTheOrder(t *testing.T) {
	h := newHarness(t)
	order := h.createOrder(t)

	cancelled, err := shared.Send[apporders.OrderDto](context.Background(), h.dispatcher, &apporders.CancelOrderLineCommand{WithContext: shared.WithContext{Context: h.userContext()}, ID: order.ID, LineID: order.Lines[0].ID, Reason: "gone"})

	if err != nil || h.orders.Items[order.ID].Status != orders.StatusCancelled || !cancelled.Lines[0].IsCancelled {
		t.Fatalf("got %v", err)
	}
}
