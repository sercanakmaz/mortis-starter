package rabbitmq_test

import (
	"encoding/json"
	"testing"

	"boilerplate/internal/domain/orders"
	"boilerplate/internal/infrastructure/messaging/rabbitmq"
	"boilerplate/internal/shared"

	"github.com/google/uuid"
)

func TestCamelName_GoInitialisms_MatchTheCSharpSerializer(t *testing.T) {
	for goName, want := range map[string]string{"ID": "id", "LineID": "lineId", "SKU": "sku", "CorrelationID": "correlationId", "OccurredAt": "occurredAt", "UserEmail": "userEmail", "HTTPServer": "httpServer"} {
		if got := rabbitmq.CamelName(goName); got != want {
			t.Errorf("CamelName(%q) = %q, want %q", goName, got, want)
		}
	}
}

func TestNewEnvelope_LineAdded_CarriesTheContextInTheBodyAndTheHeaders(t *testing.T) {
	event := &orders.LineAdded{EventBase: shared.EventBase{EventID: uuid.New(), CorrelationID: uuid.New(), TenantID: "acme", UserID: "go-user", UserEmail: "go@example.com"}, ID: "order-1", LineID: "line-1", SKU: "SKU-1", Quantity: 3}

	envelope, err := rabbitmq.NewEnvelope(event, event.EventName(), "go-test")
	if err != nil {
		t.Fatal(err)
	}
	var body map[string]any
	_ = json.Unmarshal(envelope.Message, &body)

	if *envelope.MessageID != event.EventID || *envelope.CorrelationID != event.CorrelationID || envelope.InitiatorID != nil {
		t.Errorf("ids: %v %v %v", envelope.MessageID, envelope.CorrelationID, envelope.InitiatorID)
	}
	if envelope.MessageType[0] != "urn:message:Boilerplate.Domain.Aggregates.Orders.Events:LineAdded" {
		t.Errorf("messageType: %v", envelope.MessageType)
	}
	if body["sku"] != "SKU-1" || body["lineId"] != "line-1" || body["tenantId"] != "acme" || body["userId"] != "go-user" || body["causationId"] != nil {
		t.Errorf("body: %v", body)
	}
	if envelope.Headers[rabbitmq.TenantHeader] != "acme" || envelope.Headers[rabbitmq.UserEmailHeader] != "go@example.com" || envelope.Headers[rabbitmq.LanguageHeader] != nil {
		t.Errorf("headers: %v", envelope.Headers)
	}
}

func TestReadEvent_ACSharpBodyWithoutContext_TakesItFromTheHeaders(t *testing.T) {
	correlation := uuid.New()
	envelope := rabbitmq.Envelope{
		CorrelationID: &correlation,
		MessageType:   []string{"urn:message:Boilerplate.Domain.Aggregates.Orders.Events:LineAdded"},
		Message:       json.RawMessage(`{"Id":"order-1","LineId":"line-1","Sku":"SKU-1","Quantity":3,"EventId":"0199f1c2-0000-7000-8000-000000000001","CausationId":null,"TenantId":null}`),
		Headers:       map[string]any{rabbitmq.TenantHeader: "acme", rabbitmq.UserHeader: "cs-user"},
	}
	event := &orders.LineAdded{}

	if err := rabbitmq.ReadEvent(envelope, event); err != nil {
		t.Fatal(err)
	}

	if event.SKU != "SKU-1" || event.LineID != "line-1" || event.Quantity != 3 || event.CorrelationID != correlation || event.TenantID != "acme" || event.UserID != "cs-user" {
		t.Errorf("event: %+v", event)
	}
	if !rabbitmq.Accepts(envelope, event.EventName()) || rabbitmq.Accepts(envelope, "Other:Event") {
		t.Error("Accepts")
	}
}
