package testsupport

import (
	"context"

	"boilerplate/internal/domain/customers"
	"boilerplate/internal/domain/orders"
	"boilerplate/internal/domain/products"
	"boilerplate/internal/shared"

	"github.com/google/uuid"
)

func raise(ctx context.Context, aggregate shared.Aggregate) {
	if eventContext := shared.EventContextOf(ctx); eventContext != nil {
		for _, event := range aggregate.Events() {
			eventContext.AddRaised(event)
		}
	}

	aggregate.ClearEvents()
}

// Products keeps products in memory.
type Products struct {
	Items map[uuid.UUID]*products.Product
}

// NewProducts returns an empty store holding the given products.
func NewProducts(items ...*products.Product) *Products {
	store := &Products{Items: map[uuid.UUID]*products.Product{}}
	for _, item := range items {
		item.ClearEvents()
		store.Items[item.ID] = item
	}

	return store
}

// Create stores a product.
func (p *Products) Create(ctx context.Context, product *products.Product) error {
	p.Items[product.ID] = product
	raise(ctx, product)

	return nil
}

// Update stores a product's changes.
func (p *Products) Update(ctx context.Context, product *products.Product) error {
	p.Items[product.ID] = product
	raise(ctx, product)

	return nil
}

// FindByID returns a product, or nil.
func (p *Products) FindByID(_ context.Context, id uuid.UUID) (*products.Product, error) {
	return p.Items[id], nil
}

// FindBySKUForUpdate returns the product with a SKU, or nil.
func (p *Products) FindBySKUForUpdate(_ context.Context, sku string) (*products.Product, error) {
	for _, product := range p.Items {
		if product.SKU == sku {
			return product, nil
		}
	}

	return nil, nil
}

// Customers keeps customers in memory.
type Customers struct {
	Items map[uuid.UUID]*customers.Customer
}

// NewCustomers returns a store holding the given customers.
func NewCustomers(items ...*customers.Customer) *Customers {
	store := &Customers{Items: map[uuid.UUID]*customers.Customer{}}
	for _, item := range items {
		item.ClearEvents()
		store.Items[item.ID] = item
	}

	return store
}

// Create stores a customer.
func (c *Customers) Create(ctx context.Context, customer *customers.Customer) error {
	c.Items[customer.ID] = customer
	raise(ctx, customer)

	return nil
}

// Update stores a customer's changes.
func (c *Customers) Update(ctx context.Context, customer *customers.Customer) error {
	c.Items[customer.ID] = customer
	raise(ctx, customer)

	return nil
}

// FindByID returns a customer, or nil.
func (c *Customers) FindByID(_ context.Context, id uuid.UUID) (*customers.Customer, error) {
	return c.Items[id], nil
}

// FindByIDForUpdate returns a customer, or nil.
func (c *Customers) FindByIDForUpdate(_ context.Context, id uuid.UUID) (*customers.Customer, error) {
	return c.Items[id], nil
}

// Orders keeps orders in memory.
type Orders struct {
	Items map[uuid.UUID]*orders.Order
}

// NewOrders returns a store holding the given orders.
func NewOrders(items ...*orders.Order) *Orders {
	store := &Orders{Items: map[uuid.UUID]*orders.Order{}}
	for _, item := range items {
		item.ClearEvents()
		store.Items[item.ID] = item
	}

	return store
}

// Create stores an order.
func (o *Orders) Create(ctx context.Context, order *orders.Order) error {
	o.Items[order.ID] = order
	raise(ctx, order)

	return nil
}

// Update stores an order's changes.
func (o *Orders) Update(ctx context.Context, order *orders.Order) error {
	o.Items[order.ID] = order
	raise(ctx, order)

	return nil
}

// FindByID returns an order, or nil.
func (o *Orders) FindByID(_ context.Context, id uuid.UUID) (*orders.Order, error) {
	return o.Items[id], nil
}

// FindByIDForUpdate returns an order, or nil.
func (o *Orders) FindByIDForUpdate(_ context.Context, id uuid.UUID) (*orders.Order, error) {
	return o.Items[id], nil
}
