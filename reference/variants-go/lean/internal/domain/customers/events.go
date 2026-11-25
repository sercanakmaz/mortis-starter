package customers

import "boilerplate/internal/shared"

// Created is raised when a customer is created.
type Created struct {
	shared.EventBase
	ID    string
	Email string
}

// EventName is the event's wire name.
func (*Created) EventName() string { return "Boilerplate.Domain.Aggregates.Customers.Events:Created" }

// Blocked is raised when a customer is blocked.
type Blocked struct {
	shared.EventBase
	ID string
}

// EventName is the event's wire name.
func (*Blocked) EventName() string { return "Boilerplate.Domain.Aggregates.Customers.Events:Blocked" }
