package main

import (
	"context"
	"encoding/json"
	"errors"
	"flag"
	"fmt"
	"log/slog"
	"os"
	"sync/atomic"
	"time"

	"boilerplate/internal/domain/orders"
	"boilerplate/internal/infrastructure/logging"
	"boilerplate/internal/infrastructure/messaging/rabbitmq"
	"boilerplate/internal/shared"

	"github.com/google/uuid"
	amqp "github.com/rabbitmq/amqp091-go"
)

// BrokerVariable names the environment variable that holds the RabbitMQ address.
const BrokerVariable = "MORTIS_RABBITMQ"

// RecorderName is the consumer type the Go endpoint reports in its fault headers.
const RecorderName = "Go.LineAddedRecorder"

// WireVariable names the environment variable that holds LineAdded's wire name on the other side; the C# advanced build keeps queue events in a Contracts project.
const WireVariable = "MORTIS_LINE_ADDED_WIRE_NAME"

func lineAddedWireName() string {
	if name := os.Getenv(WireVariable); name != "" {
		return name
	}
	return (&orders.LineAdded{}).EventName()
}

// gobus publish | consume | declare: the Go side of the bus interop checks (tests/check_interop_go.sh).
func main() {
	if len(os.Args) < 2 {
		fmt.Fprintln(os.Stderr, "usage: gobus publish|consume|declare [flags]")
		os.Exit(2)
	}
	broker := os.Getenv(BrokerVariable)
	if broker == "" {
		broker = "amqp://guest:guest@localhost:5672/"
	}
	connection, err := amqp.Dial(broker)
	if err != nil {
		fmt.Fprintln(os.Stderr, err)
		os.Exit(1)
	}
	defer connection.Close()
	logger := slog.New(logging.NewJSONLineHandler(os.Stderr))

	switch os.Args[1] {
	case "publish":
		err = publish(connection, os.Args[2:])
	case "consume":
		err = consume(connection, logger, os.Args[2:])
	case "declare":
		err = declare(connection, logger, os.Args[2:])
	default:
		err = errors.New("unknown action " + os.Args[1])
	}
	if err != nil {
		fmt.Fprintln(os.Stderr, err)
		os.Exit(1)
	}
}

func publish(connection *amqp.Connection, arguments []string) error {
	flags := flag.NewFlagSet("publish", flag.ExitOnError)
	sku := flags.String("sku", "", "")
	quantity := flags.Int("quantity", 1, "")
	order := flags.String("order", "order-go", "")
	correlation := flags.String("correlation", "", "")
	tenant := flags.String("tenant", "", "")
	user := flags.String("user", "", "")
	email := flags.String("email", "", "")
	_ = flags.Parse(arguments)

	event := &orders.LineAdded{EventBase: shared.EventBase{EventID: uuid.Must(uuid.NewV7()), OccurredAt: time.Now().UTC(), CorrelationID: uuid.Must(uuid.NewV7()), TenantID: *tenant, UserID: *user, UserEmail: *email}, ID: *order, LineID: uuid.NewString(), SKU: *sku, Quantity: *quantity}
	if *correlation != "" {
		event.CorrelationID = uuid.MustParse(*correlation)
	}
	publisher := &rabbitmq.Publisher{Connection: connection, Source: "gobus", WireNames: map[string]string{event.EventName(): lineAddedWireName()}}
	if err := publisher.Publish(context.Background(), event); err != nil {
		return err
	}
	fmt.Println(event.EventID.String())
	return nil
}

type recorder struct {
	sku      string
	out      string
	fail     bool
	attempts atomic.Int32
	done     context.CancelFunc
	limit    int32
}

func (r *recorder) Handle(_ context.Context, event shared.Event) error {
	if r.sku != "" && event.(*orders.LineAdded).SKU != r.sku {
		return nil
	}
	attempt := r.attempts.Add(1)
	if r.fail {
		if attempt >= r.limit {
			go func() { time.Sleep(time.Second); r.done() }()
		}
		return errors.New("the recorder was told to fail")
	}
	body, _ := json.Marshal(event)
	if err := os.WriteFile(r.out, body, 0o644); err != nil {
		return err
	}
	go func() { time.Sleep(300 * time.Millisecond); r.done() }()
	return nil
}

func consume(connection *amqp.Connection, logger *slog.Logger, arguments []string) error {
	flags := flag.NewFlagSet("consume", flag.ExitOnError)
	queue := flags.String("queue", "", "")
	out := flags.String("out", "", "")
	fail := flags.Bool("fail", false, "")
	retry := flags.Int("retry", 1, "")
	timeout := flags.Duration("timeout", 30*time.Second, "")
	ready := flags.String("ready", "", "a file written once the endpoint consumes")
	sku := flags.String("sku", "", "record only the line of this sku")
	_ = flags.Parse(arguments)

	ctx, cancel := context.WithTimeout(context.Background(), *timeout)
	defer cancel()
	consumer := &recorder{sku: *sku, out: *out, fail: *fail, done: cancel, limit: int32(*retry + 1)}
	endpoint := endpointFor(connection, logger, *queue, consumer, *retry)
	if *ready != "" {
		go func() { time.Sleep(time.Second); _ = os.WriteFile(*ready, []byte("ready"), 0o644) }()
	}
	return endpoint.Run(ctx)
}

func declare(connection *amqp.Connection, logger *slog.Logger, arguments []string) error {
	flags := flag.NewFlagSet("declare", flag.ExitOnError)
	queue := flags.String("queue", "", "")
	purge := flags.Bool("purge", false, "drop the messages earlier runs left in the queue")
	_ = flags.Parse(arguments)

	channel, err := connection.Channel()
	if err != nil {
		return err
	}
	defer channel.Close()
	if err := endpointFor(connection, logger, *queue, nil, 0).Declare(channel); err != nil {
		return err
	}
	if *purge {
		_, err = channel.QueuePurge(*queue, false)
	}
	return err
}

func endpointFor(connection *amqp.Connection, logger *slog.Logger, queue string, consumer shared.Consumer, retry int) *rabbitmq.Endpoint {
	return &rabbitmq.Endpoint{
		Connection:   connection,
		Queue:        queue,
		EventName:    lineAddedWireName(),
		NewEvent:     func() shared.Event { return &orders.LineAdded{} },
		Consumer:     consumer,
		ConsumerName: RecorderName,
		RetryCount:   retry,
		Logger:       logger,
	}
}
