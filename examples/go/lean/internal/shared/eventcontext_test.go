package shared_test

import (
	"testing"

	"delta/internal/shared"
)

type probeEvent struct {
	shared.EventBase
}

func (*probeEvent) EventName() string { return "Probe" }

func TestAddRaised_InsideACommand_StampsTheEventWithItsContext(t *testing.T) {
	eventContext := &shared.EventContext{}
	commandContext := shared.CommandContext{User: shared.AuditUser{ID: "user-1", Email: "user-1@localhost"}, TenantID: "t-1"}.Complete()
	eventContext.EnterCommand(commandContext)
	event := &probeEvent{EventBase: shared.NewEventBase()}

	eventContext.AddRaised(event)

	base := eventContext.TakeRaised().Base()
	if base.CorrelationID != commandContext.CorrelationID || base.UserID != "user-1" || base.TenantID != "t-1" {
		t.Fatalf("got %+v", base)
	}
}

func TestFromEvent_IsCausedByTheEventInTheSameRequest(t *testing.T) {
	event := &probeEvent{EventBase: shared.NewEventBase()}
	event.CorrelationID = shared.NewID()
	event.UserID = "user-1"

	commandContext := shared.FromEvent(event)

	if commandContext.CausationID != event.EventID || commandContext.CorrelationID != event.CorrelationID || commandContext.User.ID != "user-1" {
		t.Fatalf("got %+v", commandContext)
	}
}
