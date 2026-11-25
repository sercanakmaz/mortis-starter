package persistence

import (
	"context"
	"errors"
	"log/slog"
	"math"
	"time"

	"boilerplate/internal/shared"

	"github.com/google/uuid"
	"github.com/jackc/pgx/v5"
)

// OutboxWorker runs, with Go consumers, the rows another service wrote to the shared outbox table (events.transport = database).
type OutboxWorker struct {
	UnitOfWork *UnitOfWork
	Consumers  map[string]shared.Consumer
	Events     map[string]func() shared.Event
	Logger     *slog.Logger
}

type outboxRow struct {
	ID       uuid.UUID
	Consumer string
	Type     string
	Payload  string
	Attempts int
}

// Run polls the table until ctx ends.
func (w *OutboxWorker) Run(ctx context.Context, interval time.Duration) {
	for ctx.Err() == nil {
		worked, err := w.RunOnce(ctx)
		if err != nil {
			w.Logger.ErrorContext(ctx, "The outbox worker failed", "Exception", err.Error())
		}

		if !worked {
			select {
			case <-ctx.Done():
			case <-time.After(interval):
			}
		}
	}
}

// RunOnce claims one due row of a consumer this worker knows, runs it in the claim's transaction and deletes it, or records the failure.
func (w *OutboxWorker) RunOnce(ctx context.Context) (bool, error) {
	transaction, err := w.UnitOfWork.Database.Pool.Begin(ctx)
	if err != nil {
		return false, err
	}
	defer func() { _ = transaction.Rollback(ctx) }()

	consumers := make([]string, 0, len(w.Consumers))
	for name := range w.Consumers {
		consumers = append(consumers, name)
	}

	var row outboxRow
	err = transaction.QueryRow(ctx, "select id, consumer, type, payload::text, attempts from outbox_messages where attempts < max_attempts and next_attempt_at <= now() and consumer = any($1) order by occurred_at, id limit 1 for update skip locked", consumers).
		Scan(&row.ID, &row.Consumer, &row.Type, &row.Payload, &row.Attempts)
	if errors.Is(err, pgx.ErrNoRows) {
		return false, nil
	}
	if err != nil {
		return false, err
	}

	eventContext := shared.WithEventContext(ctx)
	transactionContext := withTransaction(eventContext, transaction)

	if err := w.handle(transactionContext, row); err != nil {
		_ = transaction.Rollback(ctx)
		shared.EventContextOf(eventContext).Clear()
		w.Logger.ErrorContext(ctx, "Event consumer "+row.Consumer+" failed for "+row.Type, "Exception", err.Error())

		_, recordErr := w.UnitOfWork.Database.Pool.Exec(ctx, "update outbox_messages set attempts = attempts + 1, last_error = $2, next_attempt_at = $3 where id = $1",
			row.ID, err.Error(), time.Now().UTC().Add(time.Duration(math.Pow(2, float64(row.Attempts+1)))*time.Second))

		return true, recordErr
	}

	if _, err := transaction.Exec(transactionContext, "delete from outbox_messages where id = $1", row.ID); err != nil {
		return true, err
	}

	if err := w.UnitOfWork.Events.ExecuteRaised(transactionContext); err != nil {
		return true, err
	}

	if err := transaction.Commit(ctx); err != nil {
		return true, err
	}

	w.UnitOfWork.Publisher.Publish(eventContext, shared.EventContextOf(eventContext).TakeDispatched())
	w.Logger.InfoContext(ctx, "Handled "+row.Type+" by "+row.Consumer)

	return true, nil
}

func (w *OutboxWorker) handle(ctx context.Context, row outboxRow) error {
	newEvent, ok := w.Events[row.Type]
	if !ok {
		return errors.New("no Go type for the event " + row.Type)
	}

	event := newEvent()
	if err := decodePayload(row.Payload, event); err != nil {
		return err
	}

	return w.Consumers[row.Consumer].Handle(ctx, event)
}
