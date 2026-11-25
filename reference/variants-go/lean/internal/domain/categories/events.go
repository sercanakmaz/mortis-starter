package categories

import "boilerplate/internal/shared"

// Created is raised when a category has its id.
type Created struct {
	shared.EventBase
	ID   string
	Name string
}

// EventName is the event's wire name.
func (*Created) EventName() string { return "Boilerplate.Domain.Aggregates.Categories.Events:Created" }
