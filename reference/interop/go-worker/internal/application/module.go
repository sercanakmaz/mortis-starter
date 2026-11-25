package application

import (
	"boilerplate/internal/application/categories"
	"boilerplate/internal/application/customers"
	"boilerplate/internal/application/orders"
	"boilerplate/internal/application/products"
	domaincategories "boilerplate/internal/domain/categories"
	domaincustomers "boilerplate/internal/domain/customers"
	domainorders "boilerplate/internal/domain/orders"
	domainproducts "boilerplate/internal/domain/products"
	"boilerplate/internal/shared"
)

// Repositories are the stores the application's handlers need.
type Repositories struct {
	Products   domainproducts.Repository
	Orders     domainorders.Repository
	Customers  domaincustomers.Repository
	Categories domaincategories.Repository
}

// Register registers every command handler, sync handler and consumer of the application.
func Register(dispatcher *shared.Dispatcher, events *shared.EventHandlers, repositories Repositories) {
	shared.Register(dispatcher, products.CreateProductHandler{Products: repositories.Products})
	shared.Register(dispatcher, products.GetProductHandler{Products: repositories.Products})
	shared.Register(dispatcher, products.DecreaseProductStockHandler{Products: repositories.Products})
	shared.Register(dispatcher, products.IncreaseProductStockHandler{Products: repositories.Products})
	shared.Register(dispatcher, customers.CreateCustomerHandler{Customers: repositories.Customers})
	shared.Register(dispatcher, customers.GetCustomerHandler{Customers: repositories.Customers})
	shared.Register(dispatcher, customers.BlockCustomerHandler{Customers: repositories.Customers})
	shared.Register(dispatcher, customers.RegisterCompletedOrderHandler{Customers: repositories.Customers})
	shared.Register(dispatcher, orders.CreateOrderHandler{Orders: repositories.Orders})
	shared.Register(dispatcher, orders.GetOrderHandler{Orders: repositories.Orders})
	shared.Register(dispatcher, orders.AddOrderLineHandler{Orders: repositories.Orders})
	shared.Register(dispatcher, orders.CancelOrderLineHandler{Orders: repositories.Orders})
	shared.Register(dispatcher, orders.CompleteOrderHandler{Orders: repositories.Orders})
	shared.Register(dispatcher, orders.CancelOrderWhenAllLinesCancelledHandler{Orders: repositories.Orders})
	shared.Register(dispatcher, categories.CreateCategoryHandler{Categories: repositories.Categories})
	shared.Register(dispatcher, categories.GetCategoryHandler{Categories: repositories.Categories})

	events.AddSync((&domainorders.Completed{}).EventName(), 0, customers.RegisterCompletedOrderWhenOrderCompletedHandler{Dispatcher: dispatcher})
	events.AddSync((&domainorders.LineCancelled{}).EventName(), 0, orders.CancelOrderWhenOrderLineCancelledHandler{Dispatcher: dispatcher})
	events.AddConsumer((&domainorders.LineAdded{}).EventName(), products.DecreaseStockWhenOrderLineAddedConsumer{Dispatcher: dispatcher})
	events.AddConsumer((&domainorders.LineCancelled{}).EventName(), products.IncreaseStockWhenOrderLineCancelledConsumer{Dispatcher: dispatcher})
}
