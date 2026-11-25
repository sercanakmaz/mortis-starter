package products

import (
	"delta/internal/shared"

	"github.com/google/uuid"
)

// Product is the aggregate root of the catalogue: a SKU, a name and its stock.
type Product struct {
	shared.AggregateRoot
	ID         uuid.UUID
	SKU        string
	Name       string
	Stock      int
	CategoryID *int
}

// New creates a product with its initial stock.
func New(id uuid.UUID, sku string, name string, stock int, user shared.AuditUser, categoryID *int) (*Product, error) {
	if stock < 0 {
		return nil, shared.InvalidParameter("stock", "it cannot be negative")
	}

	product := &Product{ID: id, SKU: sku, Name: name, Stock: stock, CategoryID: categoryID}
	product.SetCreatedAudit(user)
	product.Raise(&Created{EventBase: shared.NewEventBase(), ID: id.String(), SKU: sku})

	return product, nil
}

// IncreaseStock adds units to the stock.
func (p *Product) IncreaseStock(quantity int) error {
	if quantity <= 0 {
		return shared.InvalidParameter("quantity", "it must be greater than zero")
	}

	p.Stock += quantity
	p.Raise(&StockIncreased{EventBase: shared.NewEventBase(), ID: p.ID.String(), SKU: p.SKU, Quantity: quantity, Stock: p.Stock})

	return nil
}

// DecreaseStock takes units from the stock; it never goes below zero.
func (p *Product) DecreaseStock(quantity int) error {
	if quantity <= 0 {
		return shared.InvalidParameter("quantity", "it must be greater than zero")
	}

	if p.Stock < quantity {
		return shared.BusinessRule(InsufficientStockCode, "The product does not have enough stock.")
	}

	p.Stock -= quantity
	p.Raise(&StockDecreased{EventBase: shared.NewEventBase(), ID: p.ID.String(), SKU: p.SKU, Quantity: quantity, Stock: p.Stock})

	return nil
}
