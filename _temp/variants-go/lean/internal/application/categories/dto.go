package categories

import domain "boilerplate/internal/domain/categories"

// CategoryDto is how a category is shown.
type CategoryDto struct {
	ID   int    `json:"Id"`
	Name string `json:"Name"`
}

// MapCategory shows a category.
func MapCategory(category *domain.Category) CategoryDto {
	return CategoryDto{ID: category.ID, Name: category.Name}
}
