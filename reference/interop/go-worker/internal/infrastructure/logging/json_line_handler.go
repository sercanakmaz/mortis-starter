package logging

import (
	"bytes"
	"context"
	"encoding/json"
	"io"
	"log/slog"
	"sync"
	"time"
)

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
