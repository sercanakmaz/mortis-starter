package shared

import "github.com/google/uuid"

// CommandContext is what a command carries besides its own data: who asked, and which request it belongs to.
type CommandContext struct {
	CorrelationID uuid.UUID
	CausationID   uuid.UUID
	User          AuditUser
	TenantID      string
	Language      string
}

// Complete gives a context without a correlation id a new one.
func (c CommandContext) Complete() CommandContext {
	if c.CorrelationID == uuid.Nil {
		c.CorrelationID = NewID()
	}

	return c
}

// FromEvent is the context of a command sent in reaction to an event: the same request and user, caused by the event.
func FromEvent(event Event) CommandContext {
	base := event.Base()

	return CommandContext{
		CorrelationID: base.CorrelationID,
		CausationID:   base.EventID,
		User:          AuditUser{ID: base.UserID, Email: base.UserEmail},
		TenantID:      base.TenantID,
		Language:      base.Language,
	}
}

// Contextual is a command that carries a CommandContext.
type Contextual interface {
	CommandContext() *CommandContext
}

// WithContext is embedded in every command to carry its context.
type WithContext struct {
	Context CommandContext `json:"-"`
}

// CommandContext returns the embedded context so the pipeline can complete it.
func (w *WithContext) CommandContext() *CommandContext {
	return &w.Context
}
