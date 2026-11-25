package shared

import (
	"time"

	"github.com/google/uuid"
)

// EventBase is what every event carries besides its own data.
type EventBase struct {
	EventID       uuid.UUID
	OccurredAt    time.Time
	CorrelationID uuid.UUID
	CausationID   uuid.UUID
	TenantID      string
	Language      string
	UserID        string
	UserEmail     string
}

// Base returns the event's base so the kernel can stamp it.
func (b *EventBase) Base() *EventBase {
	return b
}

// NewEventBase starts the base of a new event.
func NewEventBase() EventBase {
	return EventBase{EventID: NewID(), OccurredAt: time.Now().UTC()}
}

// Event is a fact an aggregate raised.
type Event interface {
	Base() *EventBase
	EventName() string
}
