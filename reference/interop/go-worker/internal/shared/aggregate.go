package shared

import "time"

// Audit records who created and last changed an aggregate.
type Audit struct {
	CreatedAt       time.Time
	CreatedUserID   string
	CreatedUserName string
	UpdatedAt       time.Time
	UpdatedUserID   string
	UpdatedUserName string
}

// AggregateRoot is embedded in every aggregate: it collects the events the aggregate raises and its audit.
type AggregateRoot struct {
	Audit
	events []Event
}

// Raise records an event the aggregate raised.
func (a *AggregateRoot) Raise(event Event) {
	a.events = append(a.events, event)
}

// Events returns the events raised and not yet taken.
func (a *AggregateRoot) Events() []Event {
	return a.events
}

// ClearEvents forgets the raised events once they are taken.
func (a *AggregateRoot) ClearEvents() {
	a.events = nil
}

// SetCreatedAudit records who created the aggregate.
func (a *AggregateRoot) SetCreatedAudit(user AuditUser) {
	now := time.Now().UTC()
	a.CreatedAt = now
	a.CreatedUserID = user.ID
	a.CreatedUserName = user.Email
	a.UpdatedAt = now
	a.UpdatedUserID = user.ID
	a.UpdatedUserName = user.Email
}

// SetUpdatedAudit records who changed the aggregate last.
func (a *AggregateRoot) SetUpdatedAudit(user AuditUser) {
	a.UpdatedAt = time.Now().UTC()
	a.UpdatedUserID = user.ID
	a.UpdatedUserName = user.Email
}

// Aggregate is what a repository needs from any aggregate.
type Aggregate interface {
	Events() []Event
	ClearEvents()
}
