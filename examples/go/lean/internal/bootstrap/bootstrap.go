package bootstrap

import (
	"context"
	"log/slog"
	"os"

	"delta/internal/application"
	"delta/internal/infrastructure/logging"
	"delta/internal/infrastructure/messaging"
	"delta/internal/infrastructure/persistence"
	"delta/internal/infrastructure/persistence/categories"
	"delta/internal/infrastructure/persistence/customers"
	"delta/internal/infrastructure/persistence/orders"
	"delta/internal/infrastructure/persistence/products"
	"delta/internal/shared"

	"github.com/shopspring/decimal"
)

// ConnectionStringVariable names the environment variable that holds the PostgreSQL connection string.
const ConnectionStringVariable = "ConnectionStrings__Default"

// DefaultConnectionString is used when the variable is not set: the local development database.
const DefaultConnectionString = "postgres://postgres:postgres@localhost:5432/delta"

// App is everything a host needs: the dispatcher, the database and the logger.
type App struct {
	Dispatcher *shared.Dispatcher
	Database   *persistence.Database
	Logger     *slog.Logger
}

// New connects to the database and wires every layer.
func New(ctx context.Context) (*App, error) {
	decimal.MarshalJSONWithoutQuotes = true

	connectionString := os.Getenv(ConnectionStringVariable)
	if connectionString == "" {
		connectionString = DefaultConnectionString
	}

	database, err := persistence.Open(ctx, connectionString)
	if err != nil {
		return nil, err
	}

	logger := slog.New(logging.NewJSONLineHandler(os.Stderr))
	events := shared.NewEventHandlers()
	unitOfWork := &persistence.UnitOfWork{Database: database, Events: events, Publisher: &messaging.EventPublisher{Events: events, Logger: logger}}
	dispatcher := shared.NewDispatcher(unitOfWork, logging.CommandMiddleware(logger))

	application.Register(dispatcher, events, application.Repositories{
		Products:   products.Repository{Database: database},
		Orders:     orders.Repository{Database: database},
		Customers:  customers.Repository{Database: database},
		Categories: categories.Repository{Database: database},
	})

	return &App{Dispatcher: dispatcher, Database: database, Logger: logger}, nil
}
