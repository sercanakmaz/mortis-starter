package logging

import (
	"context"
	"reflect"

	"delta/internal/shared"

	"github.com/google/uuid"
)

// Field is one named value every log line of a scope carries.
type Field struct {
	Key   string
	Value any
}

type fieldsKey struct{}

// WithFields returns ctx whose log lines carry fields, replacing fields of the same key.
func WithFields(ctx context.Context, fields ...Field) context.Context {
	existing := FieldsOf(ctx)
	merged := make([]Field, 0, len(existing)+len(fields))

	for _, field := range existing {
		replaced := false
		for _, added := range fields {
			replaced = replaced || added.Key == field.Key
		}

		if !replaced {
			merged = append(merged, field)
		}
	}

	return context.WithValue(ctx, fieldsKey{}, append(merged, fields...))
}

// FieldsOf returns the fields of ctx.
func FieldsOf(ctx context.Context) []Field {
	fields, _ := ctx.Value(fieldsKey{}).([]Field)

	return fields
}

// OfCommand is what a command's log lines carry.
func OfCommand(command any) []Field {
	var commandContext shared.CommandContext
	if contextual, ok := command.(shared.Contextual); ok {
		commandContext = *contextual.CommandContext()
	}

	return []Field{
		{Key: CorrelationID, Value: text(commandContext.CorrelationID)},
		{Key: "CausationId", Value: text(commandContext.CausationID)},
		{Key: "TenantId", Value: commandContext.TenantID},
		{Key: "UserId", Value: commandContext.User.ID},
		{Key: "Language", Value: commandContext.Language},
		{Key: "Command", Value: TypeName(command)},
	}
}

// OfEvent is what the log lines about an event carry.
func OfEvent(event shared.Event) []Field {
	base := event.Base()

	return []Field{
		{Key: CorrelationID, Value: text(base.CorrelationID)},
		{Key: "CausationId", Value: text(base.CausationID)},
		{Key: "TenantId", Value: base.TenantID},
		{Key: "UserId", Value: base.UserID},
		{Key: "Language", Value: base.Language},
		{Key: "Event", Value: TypeName(event)},
		{Key: "EventId", Value: base.EventID.String()},
	}
}

// TypeName is the name of a value's type, without the pointer.
func TypeName(value any) string {
	valueType := reflect.TypeOf(value)
	for valueType.Kind() == reflect.Pointer {
		valueType = valueType.Elem()
	}

	return valueType.Name()
}

func text(id uuid.UUID) any {
	if id == uuid.Nil {
		return nil
	}

	return id.String()
}
