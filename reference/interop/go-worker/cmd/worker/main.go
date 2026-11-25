package main

import (
	"context"
	"fmt"
	"os"
	"os/signal"
	"time"

	"boilerplate/internal/application/products"
	"boilerplate/internal/bootstrap"
	"boilerplate/internal/domain/orders"
	"boilerplate/internal/infrastructure/persistence"
	"boilerplate/internal/shared"
)

// The rows' names are the C# service's: its consumer classes and its event types.
const (
	DecreaseStockConsumer = "Boilerplate.Application.Products.DecreaseStockWhenOrderLineAddedEventConsumer"
	IncreaseStockConsumer = "Boilerplate.Application.Products.IncreaseStockWhenOrderLineCancelledEventConsumer"
	LineAddedType         = "Boilerplate.Domain.Aggregates.Orders.Events.LineAdded"
	LineCancelledType     = "Boilerplate.Domain.Aggregates.Orders.Events.LineCancelled"
	PollingInterval       = time.Second
)

func main() {
	ctx, stop := signal.NotifyContext(context.Background(), os.Interrupt)
	defer stop()

	app, err := bootstrap.New(ctx)
	if err != nil {
		fmt.Fprintln(os.Stderr, err)
		os.Exit(1)
	}
	defer app.Database.Pool.Close()

	worker := &persistence.OutboxWorker{
		UnitOfWork: app.UnitOfWork,
		Logger:     app.Logger,
		Consumers: map[string]shared.Consumer{
			DecreaseStockConsumer: products.DecreaseStockWhenOrderLineAddedConsumer{Dispatcher: app.Dispatcher},
			IncreaseStockConsumer: products.IncreaseStockWhenOrderLineCancelledConsumer{Dispatcher: app.Dispatcher},
		},
		Events: map[string]func() shared.Event{
			LineAddedType:     func() shared.Event { return &orders.LineAdded{} },
			LineCancelledType: func() shared.Event { return &orders.LineCancelled{} },
		},
	}

	app.Logger.Info("The Go outbox worker is running")
	worker.Run(ctx, PollingInterval)
}
