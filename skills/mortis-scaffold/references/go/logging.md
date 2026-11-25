# Logging: internal/infrastructure/logging

`log/slog` with one handler that writes every record as one flat JSON line on stderr, `CorrelationId` first. The fields of a scope (a command, an event) travel in the `context.Context`; every log call passes `ctx`.

Package `logging`, no imports:

```go
// The names of the log fields every line has.
const (
	CorrelationID = "CorrelationId"
	Exception     = "Exception"
	Service       = "{Name}"
)
```

Package `logging`, imports `"context"`, `"reflect"`, `"{module}/internal/shared"`, `"github.com/google/uuid"`. In parts, in file order:

```go
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
```

```go
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
```

Package `logging`, imports `"bytes"`, `"context"`, `"encoding/json"`, `"io"`, `"log/slog"`, `"sync"`, `"time"`. In parts, in file order:

```go
// JSONLineHandler writes each log record as one flat JSON object, CorrelationId first.
type JSONLineHandler struct {
	writer   io.Writer
	category string
	mutex    *sync.Mutex
}

// NewJSONLineHandler writes to writer.
func NewJSONLineHandler(writer io.Writer) *JSONLineHandler {
	return &JSONLineHandler{writer: writer, mutex: &sync.Mutex{}}
}

// Enabled writes Information and above.
func (h *JSONLineHandler) Enabled(_ context.Context, level slog.Level) bool {
	return level >= slog.LevelInfo
}

// WithAttrs keeps the handler; a Category attribute names the logger.
func (h *JSONLineHandler) WithAttrs(attrs []slog.Attr) slog.Handler {
	copied := *h
	for _, attr := range attrs {
		if attr.Key == "Category" {
			copied.category = attr.Value.String()
		}
	}

	return &copied
}

// WithGroup is not used: every line is flat.
func (h *JSONLineHandler) WithGroup(_ string) slog.Handler {
	return h
}
```

```go
// Handle writes one line.
func (h *JSONLineHandler) Handle(ctx context.Context, record slog.Record) error {
	fields := FieldsOf(ctx)
	correlationID := any(nil)
	rest := []Field{}

	for _, field := range fields {
		if field.Key == CorrelationID {
			correlationID = field.Value
		} else {
			rest = append(rest, field)
		}
	}

	record.Attrs(func(attr slog.Attr) bool {
		rest = append(rest, Field{Key: attr.Key, Value: attr.Value.Any()})

		return true
	})

	line := []Field{
		{Key: CorrelationID, Value: correlationID},
		{Key: "Timestamp", Value: record.Time.UTC().Format(time.RFC3339Nano)},
		{Key: "Level", Value: levelName(record.Level)},
		{Key: "Service", Value: Service},
		{Key: "Category", Value: h.category},
		{Key: "Message", Value: record.Message},
	}

	var buffer bytes.Buffer
	buffer.WriteByte('{')

	for index, field := range append(line, rest...) {
		if index > 0 {
			buffer.WriteByte(',')
		}

		key, _ := json.Marshal(field.Key)
		value, err := json.Marshal(field.Value)
		if err != nil {
			value, _ = json.Marshal(err.Error())
		}

		buffer.Write(key)
		buffer.WriteByte(':')
		buffer.Write(value)
	}

	buffer.WriteString("}\n")

	h.mutex.Lock()
	defer h.mutex.Unlock()

	_, err := h.writer.Write(buffer.Bytes())

	return err
}
```

```go
func levelName(level slog.Level) string {
	switch {
	case level >= slog.LevelError:
		return "Error"
	case level >= slog.LevelWarn:
		return "Warning"
	default:
		return "Information"
	}
}
```

`command_middleware.go`: the dispatcher's one middleware. `"<Command> started"` is the line the conformance checks look for.

Package `logging`, imports `"context"`, `"log/slog"`, `"{module}/internal/shared"`:

```go
// CommandMiddleware gives every log line of a command its context fields and logs the command's start and failure.
func CommandMiddleware(logger *slog.Logger) shared.Middleware {
	logger = logger.With("Category", "{Name}.Commands")

	return func(ctx context.Context, command any, next func(ctx context.Context) (any, error)) (any, error) {
		ctx = WithFields(ctx, OfCommand(command)...)
		name := TypeName(command)

		logger.InfoContext(ctx, name+" started")

		result, err := next(ctx)
		if err != nil {
			logger.WarnContext(ctx, name+" failed: "+err.Error())

			return nil, err
		}

		logger.InfoContext(ctx, name+" completed")

		return result, nil
	}
}
```
