package persistence

import (
	"context"

	"delta/internal/shared"
)

// RaiseEvents hands the aggregate's events to the command's EventContext once the aggregate is written.
func RaiseEvents(ctx context.Context, aggregate shared.Aggregate) {
	if eventContext := shared.EventContextOf(ctx); eventContext != nil {
		for _, event := range aggregate.Events() {
			eventContext.AddRaised(event)
		}
	}

	aggregate.ClearEvents()
}
