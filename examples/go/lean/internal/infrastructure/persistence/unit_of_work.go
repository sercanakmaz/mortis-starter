package persistence

import (
	"context"

	"delta/internal/shared"
)

// Publisher runs the consumers of committed events.
type Publisher interface {
	Publish(ctx context.Context, events []shared.Event)
}

// UnitOfWork runs a command in one transaction: the sync handlers before the commit, the consumers after it.
type UnitOfWork struct {
	Database  *Database
	Events    *shared.EventHandlers
	Publisher Publisher
}

// InTransaction runs work in the context's transaction, or in a new one it commits when work succeeds.
func (u *UnitOfWork) InTransaction(ctx context.Context, name string, work func(ctx context.Context) error) error {
	if _, ok := transactionOf(ctx); ok {
		return work(ctx)
	}

	transaction, err := u.Database.Pool.Begin(ctx)
	if err != nil {
		return err
	}

	transactionContext := withTransaction(ctx, transaction)
	eventContext := shared.EventContextOf(ctx)

	err = work(transactionContext)
	if err == nil {
		err = u.Events.ExecuteRaised(transactionContext)
	}

	if err != nil {
		_ = transaction.Rollback(ctx)
		eventContext.Clear()

		return err
	}

	if err := transaction.Commit(ctx); err != nil {
		eventContext.Clear()

		return err
	}

	u.Publisher.Publish(ctx, eventContext.TakeDispatched())

	return nil
}
