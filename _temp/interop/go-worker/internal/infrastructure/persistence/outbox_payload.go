package persistence

import (
	"encoding/json"

	"boilerplate/internal/shared"
)

// decodePayload reads the C# System.Text.Json payload: its member names match the Go fields ignoring case.
func decodePayload(payload string, event shared.Event) error {
	return json.Unmarshal([]byte(payload), event)
}
