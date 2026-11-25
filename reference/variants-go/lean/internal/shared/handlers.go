package shared

import (
	"context"
	"sort"
)

// SyncHandler reacts to an event inside the transaction of the command that raised it; its failure rolls the command back.
type SyncHandler interface {
	Handle(ctx context.Context, event Event) error
}

// Consumer reacts to an event after the command that raised it committed; its failure is logged and never undoes the command.
type Consumer interface {
	Handle(ctx context.Context, event Event) error
}

type syncRegistration struct {
	sequence int
	handler  SyncHandler
}

// EventHandlers knows, per event name, the sync handlers and the consumers.
type EventHandlers struct {
	sync      map[string][]syncRegistration
	consumers map[string][]Consumer
}

// NewEventHandlers returns an empty set of handlers.
func NewEventHandlers() *EventHandlers {
	return &EventHandlers{sync: map[string][]syncRegistration{}, consumers: map[string][]Consumer{}}
}

// AddSync registers a sync handler of an event; handlers of one event run in ascending sequence.
func (h *EventHandlers) AddSync(eventName string, sequence int, handler SyncHandler) {
	h.sync[eventName] = append(h.sync[eventName], syncRegistration{sequence: sequence, handler: handler})
	sort.SliceStable(h.sync[eventName], func(i, j int) bool { return h.sync[eventName][i].sequence < h.sync[eventName][j].sequence })
}

// AddConsumer registers a consumer of an event.
func (h *EventHandlers) AddConsumer(eventName string, consumer Consumer) {
	h.consumers[eventName] = append(h.consumers[eventName], consumer)
}

// ConsumersOf returns the consumers of an event.
func (h *EventHandlers) ConsumersOf(eventName string) []Consumer {
	return h.consumers[eventName]
}

// ExecuteRaised runs the sync handlers of every raised event, including events their commands raise, in raised order.
func (h *EventHandlers) ExecuteRaised(ctx context.Context) error {
	eventContext := EventContextOf(ctx)

	for event := eventContext.TakeRaised(); event != nil; event = eventContext.TakeRaised() {
		for _, registration := range h.sync[event.EventName()] {
			if err := registration.handler.Handle(ctx, event); err != nil {
				return err
			}
		}

		eventContext.AddDispatched(event)
	}

	return nil
}
