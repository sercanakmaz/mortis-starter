package persistence

import (
	"context"

	"github.com/jackc/pgx/v5"
	"github.com/jackc/pgx/v5/pgconn"
	"github.com/jackc/pgx/v5/pgxpool"
)

// Querier is what repositories run SQL on: the transaction of the context, or the pool.
type Querier interface {
	Exec(ctx context.Context, sql string, arguments ...any) (pgconn.CommandTag, error)
	Query(ctx context.Context, sql string, arguments ...any) (pgx.Rows, error)
	QueryRow(ctx context.Context, sql string, arguments ...any) pgx.Row
}

type transactionKey struct{}

// Database is the connection pool and the way to the context's transaction.
type Database struct {
	Pool *pgxpool.Pool
}

// Open connects to PostgreSQL.
func Open(ctx context.Context, connectionString string) (*Database, error) {
	pool, err := pgxpool.New(ctx, ConnectionString(connectionString))
	if err != nil {
		return nil, err
	}

	return &Database{Pool: pool}, nil
}

// On returns the context's transaction, or the pool when there is none.
func (d *Database) On(ctx context.Context) Querier {
	if transaction, ok := ctx.Value(transactionKey{}).(pgx.Tx); ok {
		return transaction
	}

	return d.Pool
}

func withTransaction(ctx context.Context, transaction pgx.Tx) context.Context {
	return context.WithValue(ctx, transactionKey{}, transaction)
}

func transactionOf(ctx context.Context) (pgx.Tx, bool) {
	transaction, ok := ctx.Value(transactionKey{}).(pgx.Tx)

	return transaction, ok
}
