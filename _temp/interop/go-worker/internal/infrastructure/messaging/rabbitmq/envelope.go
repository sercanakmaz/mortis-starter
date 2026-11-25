package rabbitmq

import (
	"encoding/json"
	"os"
	"regexp"
	"strings"
	"time"

	"boilerplate/internal/shared"

	"github.com/google/uuid"
)

// Envelope is the MassTransit envelope; json names are camelCase, and reading matches names ignoring case.
type Envelope struct {
	MessageID          *uuid.UUID      `json:"messageId"`
	RequestID          *uuid.UUID      `json:"requestId"`
	CorrelationID      *uuid.UUID      `json:"correlationId"`
	ConversationID     *uuid.UUID      `json:"conversationId"`
	InitiatorID        *uuid.UUID      `json:"initiatorId"`
	SourceAddress      *string         `json:"sourceAddress"`
	DestinationAddress *string         `json:"destinationAddress"`
	ResponseAddress    *string         `json:"responseAddress"`
	FaultAddress       *string         `json:"faultAddress"`
	MessageType        []string        `json:"messageType"`
	Message            json.RawMessage `json:"message"`
	ExpirationTime     *time.Time      `json:"expirationTime"`
	SentTime           *time.Time      `json:"sentTime"`
	Headers            map[string]any  `json:"headers"`
	Host               map[string]any  `json:"host"`
}

var goWord = regexp.MustCompile(`[A-Z]+[a-z0-9]*|[a-z0-9]+`)

// CamelName turns a Go field name into the C# serializer's camelCase name: LineID is lineId, SKU is sku.
func CamelName(goName string) string {
	words := splitWords(goName)
	for i, word := range words {
		if i == 0 {
			words[i] = strings.ToLower(word)
			continue
		}
		words[i] = strings.ToUpper(word[:1]) + strings.ToLower(word[1:])
	}
	return strings.Join(words, "")
}

func splitWords(name string) []string {
	var words []string
	for _, match := range goWord.FindAllString(name, -1) {
		upper := 0
		for upper < len(match) && match[upper] >= 'A' && match[upper] <= 'Z' {
			upper++
		}
		if upper > 1 && upper < len(match) {
			words = append(words, match[:upper-1], match[upper-1:])
			continue
		}
		words = append(words, match)
	}
	return words
}

// MessageBody serializes an event the way the C# side does: camelCase names, a zero causation id as null.
func MessageBody(event shared.Event) (json.RawMessage, error) {
	raw, err := json.Marshal(event)
	if err != nil {
		return nil, err
	}
	var fields map[string]any
	if err := json.Unmarshal(raw, &fields); err != nil {
		return nil, err
	}
	body := make(map[string]any, len(fields))
	for name, value := range fields {
		body[CamelName(name)] = value
	}
	if event.Base().CausationID == uuid.Nil {
		body["causationId"] = nil
	}
	return json.Marshal(body)
}

// NewEnvelope wraps an event for publishing from the source endpoint under its wire name.
func NewEnvelope(event shared.Event, wireName string, source string) (Envelope, error) {
	body, err := MessageBody(event)
	if err != nil {
		return Envelope{}, err
	}
	base := event.Base()
	messageID, correlationID := base.EventID, base.CorrelationID
	sourceAddress, destinationAddress := AddressPrefix+source, AddressPrefix+wireName
	sentTime := time.Now().UTC()
	machine, _ := os.Hostname()
	envelope := Envelope{
		MessageID:          &messageID,
		CorrelationID:      &correlationID,
		ConversationID:     &correlationID,
		SourceAddress:      &sourceAddress,
		DestinationAddress: &destinationAddress,
		MessageType:        []string{URNPrefix + wireName, URNPrefix + EventBaseName, URNPrefix + QueueEventName},
		Message:            body,
		SentTime:           &sentTime,
		Headers:            map[string]any{TenantHeader: nullable(base.TenantID), UserHeader: nullable(base.UserID), UserEmailHeader: nullable(base.UserEmail), LanguageHeader: nullable(base.Language)},
		Host:               map[string]any{"machineName": machine, "processName": os.Args[0], "processId": os.Getpid(), "frameworkVersion": "go"},
	}
	if base.CausationID != uuid.Nil {
		initiator := base.CausationID
		envelope.InitiatorID = &initiator
	}
	return envelope, nil
}

func nullable(value string) any {
	if value == "" {
		return nil
	}
	return value
}

// ReadEvent fills event from the envelope: the body first, then the envelope's correlation id and the Mortis headers for what the body lacks.
func ReadEvent(envelope Envelope, event shared.Event) error {
	if err := json.Unmarshal(envelope.Message, event); err != nil {
		return err
	}
	base := event.Base()
	if base.CorrelationID == uuid.Nil && envelope.CorrelationID != nil {
		base.CorrelationID = *envelope.CorrelationID
	}
	if base.EventID == uuid.Nil && envelope.MessageID != nil {
		base.EventID = *envelope.MessageID
	}
	fill := func(target *string, header string) {
		if *target == "" {
			if value, ok := envelope.Headers[header].(string); ok {
				*target = value
			}
		}
	}
	fill(&base.TenantID, TenantHeader)
	fill(&base.UserID, UserHeader)
	fill(&base.UserEmail, UserEmailHeader)
	fill(&base.Language, LanguageHeader)
	return nil
}

// Accepts reports whether the envelope carries the URN of the wire name.
func Accepts(envelope Envelope, wireName string) bool {
	for _, messageType := range envelope.MessageType {
		if messageType == URNPrefix+wireName {
			return true
		}
	}
	return false
}
