package categories

import (
	"errors"
	"strconv"
	"strings"

	"delta/internal/shared"
)

// Category groups products; its integer id is assigned by the database.
type Category struct {
	shared.AggregateRoot
	ID   int
	Name string
}

// New creates a category without an id; the repository assigns it.
func New(name string, user shared.AuditUser) *Category {
	category := &Category{Name: strings.TrimSpace(name)}
	category.SetCreatedAudit(user)

	return category
}

// MarkCreated raises Created once the database has assigned the id.
func (c *Category) MarkCreated() error {
	if c.ID == 0 {
		return errors.New("a category is marked created only after the database has assigned its id")
	}

	c.Raise(&Created{EventBase: shared.NewEventBase(), ID: strconv.Itoa(c.ID), Name: c.Name})

	return nil
}
