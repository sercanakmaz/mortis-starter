package categories

import (
	"context"

	domain "boilerplate/internal/domain/categories"
	"boilerplate/internal/shared"
)

// CreateCategoryCommand creates a category; the database assigns its id.
type CreateCategoryCommand struct {
	shared.WithContext
	shared.InTransaction
	Name string `json:"Name"`
}

// Validate checks the command's shape.
func (c *CreateCategoryCommand) Validate() shared.FieldErrors {
	fieldErrors := shared.FieldErrors{}
	fieldErrors.NotEmpty("Name", c.Name)
	fieldErrors.MaxLength("Name", c.Name, domain.NameMaxLength)

	return fieldErrors
}

// CreateCategoryHandler runs CreateCategoryCommand.
type CreateCategoryHandler struct {
	Categories domain.Repository
}

// Handle creates the category.
func (h CreateCategoryHandler) Handle(ctx context.Context, command *CreateCategoryCommand) (CategoryDto, error) {
	category := domain.New(command.Name, command.Context.User)

	if err := h.Categories.Create(ctx, category); err != nil {
		return CategoryDto{}, err
	}

	return MapCategory(category), nil
}
