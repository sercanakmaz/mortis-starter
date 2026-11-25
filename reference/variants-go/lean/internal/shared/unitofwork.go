package shared

import "context"

// UnitOfWork runs work in one transaction; a call inside another joins it.
type UnitOfWork interface {
	InTransaction(ctx context.Context, name string, work func(ctx context.Context) error) error
}

// Transactional marks a command that runs in a transaction.
type Transactional interface {
	Transactional()
}

// InTransaction is embedded in a command that runs in a transaction.
type InTransaction struct{}

// Transactional marks the command.
func (InTransaction) Transactional() {}
