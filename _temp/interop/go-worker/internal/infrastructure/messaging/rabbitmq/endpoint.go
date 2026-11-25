package rabbitmq

import (
	"context"
	"encoding/json"
	"fmt"
	"log/slog"
	"os"
	"runtime/debug"
	"time"

	"boilerplate/internal/shared"

	"github.com/google/uuid"

	amqp "github.com/rabbitmq/amqp091-go"
)

// Endpoint is one receive endpoint: queue Queue consumes one event type, by its wire name EventName, with one consumer, as the C# ReceiveEndpointService.
type Endpoint struct {
	Connection   *amqp.Connection
	Queue        string
	EventName    string
	NewEvent     func() shared.Event
	Consumer     shared.Consumer
	ConsumerName string
	RetryCount   int
	Logger       *slog.Logger
}

// Declare builds the endpoint's topology exactly as the C# side does: queues Q and Q_error (durable, classic), an exchange of the same name bound to each, and the event's exchange bound to exchange Q.
func (e *Endpoint) Declare(channel *amqp.Channel) error {
	arguments := amqp.Table{QueueTypeArgument: ClassicQueueType}
	for _, queue := range []string{e.Queue, e.Queue + ErrorSuffix} {
		if _, err := channel.QueueDeclare(queue, true, false, false, false, arguments); err != nil {
			return err
		}
		if err := channel.ExchangeDeclare(queue, amqp.ExchangeFanout, true, false, false, false, nil); err != nil {
			return err
		}
		if err := channel.QueueBind(queue, "", queue, false, nil); err != nil {
			return err
		}
	}
	if err := channel.ExchangeDeclare(e.EventName, amqp.ExchangeFanout, true, false, false, false, nil); err != nil {
		return err
	}
	return channel.ExchangeBind(e.Queue, "", e.EventName, false, nil)
}

// Run declares the topology and consumes until ctx ends.
func (e *Endpoint) Run(ctx context.Context) error {
	channel, err := e.Connection.Channel()
	if err != nil {
		return err
	}
	defer channel.Close()

	if err := e.Declare(channel); err != nil {
		return err
	}
	if err := channel.Qos(PrefetchCount, 0, false); err != nil {
		return err
	}
	consumerTag := e.Queue + "-" + uuid.NewString()
	deliveries, err := channel.Consume(e.Queue, consumerTag, false, false, false, false, nil)
	if err != nil {
		return err
	}
	for {
		select {
		case <-ctx.Done():
			return channel.Cancel(consumerTag, false)
		case delivery, ok := <-deliveries:
			if !ok {
				return fmt.Errorf("the channel of %s closed", e.Queue)
			}
			if err := e.handle(ctx, channel, delivery); err != nil {
				return err
			}
		}
	}
}

func (e *Endpoint) handle(ctx context.Context, channel *amqp.Channel, delivery amqp.Delivery) error {
	var envelope Envelope
	if err := json.Unmarshal(delivery.Body, &envelope); err != nil || !Accepts(envelope, e.EventName) {
		e.Logger.WarnContext(ctx, "No consumer on "+e.Queue+" for the message; it is acknowledged", "MessageType", envelope.MessageType)
		return delivery.Ack(false)
	}
	event := e.NewEvent()
	if err := ReadEvent(envelope, event); err != nil {
		return e.fault(ctx, channel, delivery, err)
	}
	for attempt := 1; ; attempt++ {
		err := e.Consumer.Handle(ctx, event)
		if err == nil {
			e.Logger.InfoContext(ctx, "Handled "+e.EventName+" "+event.Base().EventID.String()+" by "+e.ConsumerName)
			return delivery.Ack(false)
		}
		if attempt > e.RetryCount {
			return e.fault(ctx, channel, delivery, err)
		}
		e.Logger.WarnContext(ctx, fmt.Sprintf("Event consumer %s failed for %s, attempt %d", e.ConsumerName, e.EventName, attempt), "Exception", err.Error())
	}
}

func (e *Endpoint) fault(ctx context.Context, channel *amqp.Channel, delivery amqp.Delivery, cause error) error {
	e.Logger.ErrorContext(ctx, "Event consumer "+e.ConsumerName+" failed for "+e.EventName+"; moved to "+e.Queue+ErrorSuffix, "Exception", cause.Error())
	headers := amqp.Table{}
	for name, value := range delivery.Headers {
		headers[name] = value
	}
	machine, _ := os.Hostname()
	headers[ReasonHeader] = FaultReason
	headers[FaultExceptionType] = GoErrorExceptionType
	headers[FaultMessage] = cause.Error()
	headers[FaultStackTrace] = string(debug.Stack())
	headers[FaultTimestamp] = time.Now().UTC().Format(time.RFC3339Nano)
	headers[FaultConsumerType] = e.ConsumerName
	headers[FaultInputAddress] = AddressPrefix + e.Queue
	headers[FaultMessageType] = e.EventName
	headers[HostMachineName] = machine
	if err := channel.PublishWithContext(ctx, e.Queue+ErrorSuffix, "", false, false, amqp.Publishing{
		Headers:       headers,
		ContentType:   delivery.ContentType,
		DeliveryMode:  amqp.Persistent,
		MessageId:     delivery.MessageId,
		CorrelationId: delivery.CorrelationId,
		Body:          delivery.Body,
	}); err != nil {
		return err
	}
	return delivery.Ack(false)
}
