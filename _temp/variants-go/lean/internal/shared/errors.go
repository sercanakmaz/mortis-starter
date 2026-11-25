package shared

import (
	"errors"
	"fmt"
	"net/http"
)

// Error is a failure the caller can act on: a stable code, an HTTP status and the values the message names.
type Error struct {
	Code    string
	Status  int
	Message string
	Params  map[string]any
	Fields  map[string][]string
	Cause   error
}

func (e *Error) Error() string {
	return e.Message
}

func (e *Error) Unwrap() error {
	return e.Cause
}

// AsError returns the Error inside err, or nil.
func AsError(err error) *Error {
	var target *Error
	if errors.As(err, &target) {
		return target
	}

	return nil
}

// NotFound is a missing record.
func NotFound(name string) *Error {
	return &Error{Code: "not_found", Status: http.StatusNotFound, Message: fmt.Sprintf("%s was not found.", name), Params: map[string]any{"name": name}}
}

// Duplicate is a record that already exists.
func Duplicate(name string, cause error) *Error {
	return &Error{Code: "duplicate", Status: http.StatusConflict, Message: fmt.Sprintf("%s already exists.", name), Params: map[string]any{"name": name}, Cause: cause}
}

// InvalidParameter is a value the caller sent that cannot be used.
func InvalidParameter(name string, reason string) *Error {
	return &Error{Code: "invalid_parameter", Status: http.StatusBadRequest, Message: fmt.Sprintf("%s is invalid: %s", name, reason), Params: map[string]any{"name": name, "reason": reason}}
}

// BusinessRule is a rule of the domain the request would break.
func BusinessRule(code string, message string) *Error {
	return &Error{Code: code, Status: http.StatusConflict, Message: message}
}

// ValidationFailed lists, per field, why a command is not valid.
func ValidationFailed(fields map[string][]string) *Error {
	return &Error{Code: "validation_failed", Status: http.StatusUnprocessableEntity, Message: "The request is not valid.", Fields: fields}
}

// ReferenceViolation is a reference to a missing record, or a record still referenced.
func ReferenceViolation(name string, cause error) *Error {
	return &Error{Code: "reference_violation", Status: http.StatusBadRequest, Message: fmt.Sprintf("The reference %s points at a missing record, or the record is still referenced.", name), Params: map[string]any{"name": name}, Cause: cause}
}
