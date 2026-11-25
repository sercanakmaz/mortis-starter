package categories

import "delta/internal/shared"

// Created is raised when a category has its id.
type Created struct {
	shared.EventBase
	ID   string
	Name string
}

// EventName is the event's wire name.
func (*Created) EventName() string { return "Delta.Domain.Aggregates.Categories.Events:Created" }
