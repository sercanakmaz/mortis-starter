package categories

import (
	"context"

	domain "delta/internal/domain/categories"
	"delta/internal/shared"
)

// GetCategoryCommand reads one category.
type GetCategoryCommand struct {
	shared.WithContext
	ID int `json:"Id"`
}

// GetCategoryHandler runs GetCategoryCommand.
type GetCategoryHandler struct {
	Categories domain.Repository
}

// Handle reads the category.
func (h GetCategoryHandler) Handle(ctx context.Context, command *GetCategoryCommand) (CategoryDto, error) {
	category, err := h.Categories.FindByID(ctx, command.ID)
	if err != nil {
		return CategoryDto{}, err
	}

	if category == nil {
		return CategoryDto{}, shared.NotFound("Category")
	}

	return MapCategory(category), nil
}
