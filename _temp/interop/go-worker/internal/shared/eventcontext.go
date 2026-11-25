package shared

import "context"

type eventContextKey struct{}

// EventContext holds, for one top-level command and everything it causes, the commands running and the events raised.
type EventContext struct {
	commands   []CommandContext
	raised     []Event
	dispatched []Event
}

// WithEventContext returns ctx with a new EventContext, unless it has one.
func WithEventContext(ctx context.Context) context.Context {
	if EventContextOf(ctx) != nil {
		return ctx
	}

	return context.WithValue(ctx, eventContextKey{}, &EventContext{})
}

// EventContextOf returns the EventContext of ctx, or nil.
func EventContextOf(ctx context.Context) *EventContext {
	eventContext, _ := ctx.Value(eventContextKey{}).(*EventContext)

	return eventContext
}

// EnterCommand makes a command's context the one events are stamped with.
func (e *EventContext) EnterCommand(commandContext CommandContext) {
	e.commands = append(e.commands, commandContext)
}

// LeaveCommand restores the context of the outer command.
func (e *EventContext) LeaveCommand() {
	e.commands = e.commands[:len(e.commands)-1]
}

// AddRaised queues an event, stamped with the context of the command that raised it.
func (e *EventContext) AddRaised(event Event) {
	if len(e.commands) > 0 {
		current := e.commands[len(e.commands)-1]
		base := event.Base()
		base.CorrelationID = current.CorrelationID
		base.CausationID = current.CausationID
		base.TenantID = current.TenantID
		base.Language = current.Language
		base.UserID = current.User.ID
		base.UserEmail = current.User.Email
	}

	e.raised = append(e.raised, event)
}

// TakeRaised removes and returns the oldest raised event, or nil.
func (e *EventContext) TakeRaised() Event {
	if len(e.raised) == 0 {
		return nil
	}

	event := e.raised[0]
	e.raised = e.raised[1:]

	return event
}

// AddDispatched records an event whose sync handlers have run.
func (e *EventContext) AddDispatched(event Event) {
	e.dispatched = append(e.dispatched, event)
}

// TakeDispatched removes and returns every event whose sync handlers have run.
func (e *EventContext) TakeDispatched() []Event {
	dispatched := e.dispatched
	e.dispatched = nil

	return dispatched
}

// Clear forgets every raised and dispatched event, after a rollback.
func (e *EventContext) Clear() {
	e.raised = nil
	e.dispatched = nil
}
