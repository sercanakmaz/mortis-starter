package shared

import "github.com/google/uuid"

// NewID returns a new time-ordered identifier. It is the only way the project creates ids.
func NewID() uuid.UUID {
	return uuid.Must(uuid.NewV7())
}
