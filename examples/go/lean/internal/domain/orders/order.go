package orders

import (
	"delta/internal/shared"

	"github.com/google/uuid"
	"github.com/shopspring/decimal"
)

// Order is the aggregate root of a purchase: its lines, its customer and its status.
type Order struct {
	shared.AggregateRoot
	ID          uuid.UUID
	OrderNumber string
	CustomerID  uuid.UUID
	Status      Status
	Lines       []*Line
}

// New opens an order for a customer.
func New(id uuid.UUID, orderNumber string, customerID uuid.UUID, user shared.AuditUser) *Order {
	order := &Order{ID: id, OrderNumber: orderNumber, CustomerID: customerID, Status: StatusOpen}
	order.SetCreatedAudit(user)
	order.Raise(&Created{EventBase: shared.NewEventBase(), ID: id.String(), OrderNumber: orderNumber, CustomerID: customerID.String()})

	return order
}

// TotalAmount is the sum of the active lines.
func (o *Order) TotalAmount() decimal.Decimal {
	total := decimal.Zero
	for _, line := range o.Lines {
		if !line.IsCancelled {
			total = total.Add(line.Price.Amount.Mul(decimal.NewFromInt(int64(line.Quantity))))
		}
	}

	return total
}

// AddLine adds a product to an open order; one active line per SKU, one currency per order.
func (o *Order) AddLine(sku string, price Money, quantity int) (*Line, error) {
	if err := o.ensureOpen(); err != nil {
		return nil, err
	}

	if quantity <= 0 {
		return nil, shared.InvalidParameter("quantity", "it must be greater than zero")
	}

	for _, line := range o.Lines {
		if !line.IsCancelled && line.SKU == sku {
			return nil, shared.Duplicate("OrderLine", nil)
		}
	}

	if len(o.Lines) > 0 && o.Lines[0].Price.Currency != price.Currency {
		return nil, shared.BusinessRule(MixedCurrencyCode, "Every line of an order uses the same currency.")
	}

	line := &Line{ID: shared.NewID(), OrderID: o.ID, SKU: sku, Price: price, Quantity: quantity}
	o.Lines = append(o.Lines, line)
	o.Raise(&LineAdded{EventBase: shared.NewEventBase(), ID: o.ID.String(), LineID: line.ID.String(), SKU: sku, Quantity: quantity})

	return line, nil
}

// CancelLine cancels one line of an open order.
func (o *Order) CancelLine(lineID uuid.UUID, reason string) error {
	if err := o.ensureOpen(); err != nil {
		return err
	}

	for _, line := range o.Lines {
		if line.ID == lineID {
			if err := line.cancel(reason); err != nil {
				return err
			}

			o.Raise(&LineCancelled{EventBase: shared.NewEventBase(), ID: o.ID.String(), LineID: lineID.String(), SKU: line.SKU, Quantity: line.Quantity, Reason: reason})

			return nil
		}
	}

	return shared.NotFound("OrderLine")
}

// CancelWhenAllLinesCancelled cancels an open order whose lines are all cancelled, and says whether it did.
func (o *Order) CancelWhenAllLinesCancelled() bool {
	if o.Status != StatusOpen {
		return false
	}

	for _, line := range o.Lines {
		if !line.IsCancelled {
			return false
		}
	}

	o.Status = StatusCancelled
	o.Raise(&Cancelled{EventBase: shared.NewEventBase(), ID: o.ID.String(), OrderNumber: o.OrderNumber, CustomerID: o.CustomerID.String()})

	return true
}

// Complete fulfils an open order that has an active line.
func (o *Order) Complete() error {
	if err := o.ensureOpen(); err != nil {
		return err
	}

	active := false
	for _, line := range o.Lines {
		active = active || !line.IsCancelled
	}

	if !active {
		return shared.BusinessRule(NothingToFulfilCode, "An order without an active line cannot be fulfilled.")
	}

	o.Status = StatusCompleted
	o.Raise(&Completed{EventBase: shared.NewEventBase(), ID: o.ID.String(), OrderNumber: o.OrderNumber, CustomerID: o.CustomerID.String()})

	return nil
}

func (o *Order) ensureOpen() error {
	if o.Status != StatusOpen {
		return shared.BusinessRule(NotOpenCode, "The order is not open.")
	}

	return nil
}
