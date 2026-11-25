package api

import (
	"net/http"

	"boilerplate/internal/shared"
)

// ContextOf is the context of a request's command. Without authentication the caller is anonymous.
func ContextOf(_ *http.Request) shared.CommandContext {
	return shared.CommandContext{}
}
