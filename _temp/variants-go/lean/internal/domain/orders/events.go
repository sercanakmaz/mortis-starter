package orders

import "boilerplate/internal/shared"

// Created is raised when an order is opened.
type Created struct {
	shared.EventBase
	ID          string
	OrderNumber string
	CustomerID  string
}

// EventName is the event's wire name.
func (*Created) EventName() string { return "Boilerplate.Domain.Aggregates.Orders.Events:Created" }

// LineAdded is raised when a product is added to an order.
type LineAdded struct {
	shared.EventBase
	ID       string
	LineID   string
	SKU      string
	Quantity int
}

// EventName is the event's wire name.
func (*LineAdded) EventName() string { return "Boilerplate.Domain.Aggregates.Orders.Events:LineAdded" }

// LineCancelled is raised when a line of an order is cancelled.
type LineCancelled struct {
	shared.EventBase
	ID       string
	LineID   string
	SKU      string
	Quantity int
	Reason   string
}

// EventName is the event's wire name.
func (*LineCancelled) EventName() string {
	return "Boilerplate.Domain.Aggregates.Orders.Events:LineCancelled"
}

// Completed is raised when an order is fulfilled.
type Completed struct {
	shared.EventBase
	ID          string
	OrderNumber string
	CustomerID  string
}

// EventName is the event's wire name.
func (*Completed) EventName() string { return "Boilerplate.Domain.Aggregates.Orders.Events:Completed" }

// Cancelled is raised when an order is cancelled.
type Cancelled struct {
	shared.EventBase
	ID          string
	OrderNumber string
	CustomerID  string
}

// EventName is the event's wire name.
func (*Cancelled) EventName() string { return "Boilerplate.Domain.Aggregates.Orders.Events:Cancelled" }
