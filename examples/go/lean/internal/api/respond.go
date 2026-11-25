package api

import (
	"crypto/sha256"
	"encoding/hex"
	"encoding/json"
	"log/slog"
	"net/http"
	"unicode"
	"unicode/utf8"

	"delta/internal/shared"
)

// The codes and texts of a failure the API did not expect.
const (
	UnexpectedErrorCode    = "unexpected_error"
	UnexpectedErrorMessage = "An unexpected error occurred."
)

// ErrorResponse is the one body of every failure.
type ErrorResponse struct {
	Code    string              `json:"code"`
	Message string              `json:"message"`
	Errors  map[string][]string `json:"errors"`
}

// WriteJSON writes a result with camelCase member names.
func WriteJSON(writer http.ResponseWriter, status int, value any) {
	encoded, err := json.Marshal(value)
	if err != nil {
		WriteError(writer, nil, err)

		return
	}

	var decoded any
	_ = json.Unmarshal(encoded, &decoded)

	writer.Header().Set("Content-Type", "application/json")
	writer.WriteHeader(status)
	_ = json.NewEncoder(writer).Encode(camelCase(decoded))
}

// WriteError writes a failure: its own status and code, or 500 with a reference for an unexpected one.
func WriteError(writer http.ResponseWriter, logger *slog.Logger, err error) {
	response := ErrorResponse{}
	status := http.StatusInternalServerError

	if failure := shared.AsError(err); failure != nil {
		status = failure.Status
		response = ErrorResponse{Code: failure.Code, Message: failure.Message, Errors: failure.Fields}
	} else {
		reference := Reference(err)
		response = ErrorResponse{Code: UnexpectedErrorCode, Message: UnexpectedErrorMessage + " Reference " + reference + "."}

		if logger != nil {
			logger.Error("Request failed with 500 "+UnexpectedErrorCode+" reference "+reference, "Exception", err.Error())
		}
	}

	writer.Header().Set("Content-Type", "application/json")
	writer.WriteHeader(status)
	_ = json.NewEncoder(writer).Encode(response)
}

// Reference is a short, stable name of a failure's kind, to find its log line.
func Reference(err error) string {
	hash := sha256.Sum256([]byte(err.Error()))

	return hex.EncodeToString(hash[:])[:8]
}

func camelCase(value any) any {
	switch typed := value.(type) {
	case map[string]any:
		converted := make(map[string]any, len(typed))
		for key, member := range typed {
			converted[lowerFirst(key)] = camelCase(member)
		}

		return converted
	case []any:
		for index, member := range typed {
			typed[index] = camelCase(member)
		}

		return typed
	default:
		return value
	}
}

func lowerFirst(key string) string {
	first, size := utf8.DecodeRuneInString(key)

	return string(unicode.ToLower(first)) + key[size:]
}
