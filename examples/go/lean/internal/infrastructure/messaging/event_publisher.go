package messaging

import (
	"context"
	"log/slog"

	"delta/internal/infrastructure/logging"
	"delta/internal/shared"
)

// EventPublisher runs, in this process, the consumers of events whose command committed; a consumer's failure is logged, never returned.
type EventPublisher struct {
	Events *shared.EventHandlers
	Logger *slog.Logger
}

// Publish runs every consumer of every event, in order.
func (p *EventPublisher) Publish(ctx context.Context, events []shared.Event) {
	logger := p.Logger.With("Category", "Delta.EventConsumers")

	for _, event := range events {
		eventContext := logging.WithFields(ctx, logging.OfEvent(event)...)
		name := logging.TypeName(event)

		for _, consumer := range p.Events.ConsumersOf(event.EventName()) {
			consumerName := logging.TypeName(consumer)

			if err := consumer.Handle(eventContext, event); err != nil {
				logger.ErrorContext(eventContext, "Event consumer "+consumerName+" failed for "+name+" "+event.Base().EventID.String(), logging.Exception, err.Error())

				continue
			}

			logger.InfoContext(eventContext, "Handled "+name+" "+event.Base().EventID.String()+" by "+consumerName)
		}
	}
}
