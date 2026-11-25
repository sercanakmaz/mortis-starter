package testsupport

import (
	"context"

	"delta/internal/shared"
)

// UnitOfWork runs work without a database: sync handlers before the end, consumers after, like the real one.
type UnitOfWork struct {
	Events    *shared.EventHandlers
	Published []shared.Event
	depth     int
}

// InTransaction runs work; the outermost call runs the sync handlers and records the events consumers would get.
func (u *UnitOfWork) InTransaction(ctx context.Context, _ string, work func(ctx context.Context) error) error {
	u.depth++
	defer func() { u.depth-- }()

	if err := work(ctx); err != nil {
		shared.EventContextOf(ctx).Clear()

		return err
	}

	if u.depth > 1 {
		return nil
	}

	if err := u.Events.ExecuteRaised(ctx); err != nil {
		shared.EventContextOf(ctx).Clear()

		return err
	}

	u.Published = append(u.Published, shared.EventContextOf(ctx).TakeDispatched()...)

	return nil
}
