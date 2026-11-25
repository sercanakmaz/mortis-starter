package rabbitmq

import (
	"context"
	"encoding/json"

	"boilerplate/internal/shared"

	amqp "github.com/rabbitmq/amqp091-go"
)

// Publisher publishes events to their message exchange, as the C# RabbitMqPublisher does.
type Publisher struct {
	Connection *amqp.Connection
	Source     string
	WireNames  map[string]string
}

// WireName is the exchange an event is published to: its EventName, unless WireNames maps it to the name the other service uses (the C# MessageNameAttribute, or a Contracts project).
func (p *Publisher) WireName(event shared.Event) string {
	if name, ok := p.WireNames[event.EventName()]; ok {
		return name
	}
	return event.EventName()
}

// Publish declares the event's exchange (durable fanout) and publishes the envelope with persistent delivery and publisher confirms.
func (p *Publisher) Publish(ctx context.Context, event shared.Event) error {
	channel, err := p.Connection.Channel()
	if err != nil {
		return err
	}
	defer channel.Close()

	if err := channel.Confirm(false); err != nil {
		return err
	}
	wireName := p.WireName(event)
	if err := channel.ExchangeDeclare(wireName, amqp.ExchangeFanout, true, false, false, false, nil); err != nil {
		return err
	}
	envelope, err := NewEnvelope(event, wireName, p.Source)
	if err != nil {
		return err
	}
	body, err := json.Marshal(envelope)
	if err != nil {
		return err
	}
	confirmation, err := channel.PublishWithDeferredConfirmWithContext(ctx, wireName, "", false, false, amqp.Publishing{
		ContentType:   ContentType,
		DeliveryMode:  amqp.Persistent,
		MessageId:     envelope.MessageID.String(),
		CorrelationId: envelope.CorrelationID.String(),
		Body:          body,
	})
	if err != nil {
		return err
	}
	if _, err := confirmation.WaitContext(ctx); err != nil {
		return err
	}
	return nil
}
