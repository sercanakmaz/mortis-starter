package products

import "delta/internal/shared"

// Created is raised when a product is created.
type Created struct {
	shared.EventBase
	ID  string
	SKU string
}

// EventName is the event's wire name.
func (*Created) EventName() string { return "Delta.Domain.Aggregates.Products.Events:Created" }

// StockIncreased is raised when units are added to the stock.
type StockIncreased struct {
	shared.EventBase
	ID       string
	SKU      string
	Quantity int
	Stock    int
}

// EventName is the event's wire name.
func (*StockIncreased) EventName() string {
	return "Delta.Domain.Aggregates.Products.Events:StockIncreased"
}

// StockDecreased is raised when units are taken from the stock.
type StockDecreased struct {
	shared.EventBase
	ID       string
	SKU      string
	Quantity int
	Stock    int
}

// EventName is the event's wire name.
func (*StockDecreased) EventName() string {
	return "Delta.Domain.Aggregates.Products.Events:StockDecreased"
}
