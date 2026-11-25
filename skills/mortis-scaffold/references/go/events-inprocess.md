# Consumers in the same process: internal/infrastructure/messaging

With `events.outbox` = `none`, the unit of work hands the committed events to `EventPublisher`, which runs their consumers at once, in the process that ran the command. A consumer's failure is logged with `Exception`, never returned: the command has committed. A crash between the commit and the consumer loses the reaction; that is the price of this option (`docs/` of the architecture skill).

Package `messaging`, imports `"context"`, `"log/slog"`, `"{module}/internal/infrastructure/logging"`, `"{module}/internal/shared"`:

```go
// EventPublisher runs, in this process, the consumers of events whose command committed; a consumer's failure is logged, never returned.
type EventPublisher struct {
	Events *shared.EventHandlers
	Logger *slog.Logger
}

// Publish runs every consumer of every event, in order.
func (p *EventPublisher) Publish(ctx context.Context, events []shared.Event) {
	logger := p.Logger.With("Category", "{Name}.EventConsumers")

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
```
