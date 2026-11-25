package orders

import (
	domain "delta/internal/domain/orders"

	"github.com/google/uuid"
	"github.com/shopspring/decimal"
)

// OrderDto is how an order is shown, with its lines.
type OrderDto struct {
	ID          uuid.UUID       `json:"Id"`
	OrderNumber string          `json:"OrderNumber"`
	CustomerID  uuid.UUID       `json:"CustomerId"`
	Status      string          `json:"Status"`
	TotalAmount decimal.Decimal `json:"TotalAmount"`
	Lines       []LineDto       `json:"Lines"`
}

// LineDto is how an order line is shown.
type LineDto struct {
	ID                 uuid.UUID       `json:"Id"`
	SKU                string          `json:"Sku"`
	Price              decimal.Decimal `json:"Price"`
	Currency           string          `json:"Currency"`
	Quantity           int             `json:"Quantity"`
	IsCancelled        bool            `json:"IsCancelled"`
	CancellationReason *string         `json:"CancellationReason"`
}

// MapOrder shows an order.
func MapOrder(order *domain.Order) OrderDto {
	lines := make([]LineDto, 0, len(order.Lines))
	for _, line := range order.Lines {
		lines = append(lines, MapLine(line))
	}

	return OrderDto{ID: order.ID, OrderNumber: order.OrderNumber, CustomerID: order.CustomerID, Status: string(order.Status), TotalAmount: order.TotalAmount(), Lines: lines}
}

// MapLine shows an order line.
func MapLine(line *domain.Line) LineDto {
	var reason *string
	if line.CancellationReason != "" {
		reason = &line.CancellationReason
	}

	return LineDto{ID: line.ID, SKU: line.SKU, Price: line.Price.Amount, Currency: line.Price.Currency, Quantity: line.Quantity, IsCancelled: line.IsCancelled, CancellationReason: reason}
}
