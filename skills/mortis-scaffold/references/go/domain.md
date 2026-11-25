# Aggregates: internal/domain

One package per aggregate. An aggregate embeds `shared.AggregateRoot`, changes only through its own methods, returns an error for a broken rule, and raises an event for every change worth reacting to. It knows nothing of the database.

The example has four aggregates. `products` and `orders` are shown in full; `customers` and `categories` follow the same pattern and are shown after them.

## products

Package `products`, imports `"{module}/internal/shared"`, `"github.com/google/uuid"`:

```go
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
```

Package `products`, no imports:

```go
// The rule codes and lengths of a product.
const (
	InsufficientStockCode = "product_insufficient_stock"
	SKUMaxLength          = 64
	NameMaxLength         = 200
)
```

Package `products`, imports `"{module}/internal/shared"`:

```go
// Created is raised when a product is created.
type Created struct {
	shared.EventBase
	ID  string
	SKU string
}

// EventName is the event's wire name.
func (*Created) EventName() string { return "{Name}.Domain.Aggregates.Products.Events:Created" }

// StockIncreased is raised when units are added to the stock.
type StockIncreased struct {
	shared.EventBase
	ID       string
	SKU      string
	Quantity int
	Stock    int
}

// EventName is the event's wire name.
func (*StockIncreased) EventName() string {
	return "{Name}.Domain.Aggregates.Products.Events:StockIncreased"
}

// StockDecreased is raised when units are taken from the stock.
type StockDecreased struct {
	shared.EventBase
	ID       string
	SKU      string
	Quantity int
	Stock    int
}

// EventName is the event's wire name.
func (*StockDecreased) EventName() string {
	return "{Name}.Domain.Aggregates.Products.Events:StockDecreased"
}
```

`repository.go`: a read that locks ends in `ForUpdate`; a command that changes an aggregate reads it that way, so two commands on one row run one after the other and no change is lost.

Package `products`, imports `"context"`, `"github.com/google/uuid"`:

```go
// Repository stores products. The ForUpdate reads lock the row until the transaction ends.
type Repository interface {
	Create(ctx context.Context, product *Product) error
	Update(ctx context.Context, product *Product) error
	FindByID(ctx context.Context, id uuid.UUID) (*Product, error)
	FindBySKUForUpdate(ctx context.Context, sku string) (*Product, error)
}
```

## orders: an aggregate with children and a value object

`Line` lives only inside its order; its unexported `cancel` is reached through `Order.CancelLine`. `Money` checks itself on creation. `Status` is a string type.

Package `orders`, imports `"{module}/internal/shared"`, `"github.com/google/uuid"`, `"github.com/shopspring/decimal"`. In parts, in file order:

```go
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
```

```go
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
```

```go
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
```

Package `orders`, imports `"{module}/internal/shared"`, `"github.com/google/uuid"`:

```go
// Line is one product on an order; it lives only inside its order.
type Line struct {
	ID                 uuid.UUID
	OrderID            uuid.UUID
	SKU                string
	Price              Money
	Quantity           int
	IsCancelled        bool
	CancellationReason string
}

func (l *Line) cancel(reason string) error {
	if l.IsCancelled {
		return shared.BusinessRule(LineAlreadyCancelledCode, "The order line is already cancelled.")
	}

	l.IsCancelled = true
	l.CancellationReason = reason

	return nil
}
```

Package `orders`, imports `"strings"`, `"{module}/internal/shared"`, `"github.com/shopspring/decimal"`:

```go
// Money is an amount in a three-letter currency.
type Money struct {
	Amount   decimal.Decimal
	Currency string
}

// NewMoney checks the amount and upper-cases the currency.
func NewMoney(amount decimal.Decimal, currency string) (Money, error) {
	if amount.IsNegative() {
		return Money{}, shared.InvalidParameter("amount", "it cannot be negative")
	}

	trimmed := strings.TrimSpace(currency)
	if len(trimmed) != CurrencyLength {
		return Money{}, shared.InvalidParameter("currency", "it must be a three-letter code")
	}

	return Money{Amount: amount, Currency: strings.ToUpper(trimmed)}, nil
}
```

Package `orders`, no imports:

```go
// Status is where an order is in its life.
type Status string

// The statuses of an order.
const (
	StatusOpen      Status = "Open"
	StatusCompleted Status = "Completed"
	StatusCancelled Status = "Cancelled"
)
```

Package `orders`, imports `"github.com/shopspring/decimal"`:

```go
// LineModel is the shape in which commands receive an order line.
type LineModel struct {
	SKU      string
	Price    decimal.Decimal
	Currency string
	Quantity int
}
```

Package `orders`, no imports:

```go
// The rule codes and lengths of an order.
const (
	NotOpenCode              = "order_not_open"
	MixedCurrencyCode        = "order_mixed_currency"
	NothingToFulfilCode      = "order_nothing_to_fulfil"
	LineAlreadyCancelledCode = "order_line_already_cancelled"
	OrderNumberMaxLength     = 64
	SKUMaxLength             = 64
	CurrencyLength           = 3
	ReasonMaxLength          = 500
)
```

Package `orders`, imports `"{module}/internal/shared"`. In parts, in file order:

```go
// Created is raised when an order is opened.
type Created struct {
	shared.EventBase
	ID          string
	OrderNumber string
	CustomerID  string
}

// EventName is the event's wire name.
func (*Created) EventName() string { return "{Name}.Domain.Aggregates.Orders.Events:Created" }

// LineAdded is raised when a product is added to an order.
type LineAdded struct {
	shared.EventBase
	ID       string
	LineID   string
	SKU      string
	Quantity int
}

// EventName is the event's wire name.
func (*LineAdded) EventName() string { return "{Name}.Domain.Aggregates.Orders.Events:LineAdded" }

// LineCancelled is raised when a line of an order is cancelled.
type LineCancelled struct {
	shared.EventBase
	ID       string
	LineID   string
	SKU      string
	Quantity int
	Reason   string
}

// EventName is the event's wire name.
func (*LineCancelled) EventName() string {
	return "{Name}.Domain.Aggregates.Orders.Events:LineCancelled"
}

// Completed is raised when an order is fulfilled.
type Completed struct {
	shared.EventBase
	ID          string
	OrderNumber string
	CustomerID  string
}

// EventName is the event's wire name.
func (*Completed) EventName() string { return "{Name}.Domain.Aggregates.Orders.Events:Completed" }

// Cancelled is raised when an order is cancelled.
type Cancelled struct {
	shared.EventBase
	ID          string
	OrderNumber string
	CustomerID  string
}
```

```go
// EventName is the event's wire name.
func (*Cancelled) EventName() string { return "{Name}.Domain.Aggregates.Orders.Events:Cancelled" }
```

Package `orders`, imports `"context"`, `"github.com/google/uuid"`:

```go
// Repository stores orders with their lines. The ForUpdate read locks the order until the transaction ends.
type Repository interface {
	Create(ctx context.Context, order *Order) error
	Update(ctx context.Context, order *Order) error
	FindByID(ctx context.Context, id uuid.UUID) (*Order, error)
	FindByIDForUpdate(ctx context.Context, id uuid.UUID) (*Order, error)
}
```

## customers

Package `customers`, imports `"strings"`, `"{module}/internal/shared"`, `"github.com/google/uuid"`:

```go
// Customer is the aggregate root of a buyer: a name, a unique email, and the orders it completed.
type Customer struct {
	shared.AggregateRoot
	ID                  uuid.UUID
	Name                string
	Email               string
	IsBlocked           bool
	CompletedOrderCount int
}

// New creates a customer; the email is stored trimmed and lower-cased.
func New(id uuid.UUID, name string, email string, user shared.AuditUser) *Customer {
	customer := &Customer{ID: id, Name: name, Email: strings.ToLower(strings.TrimSpace(email))}
	customer.SetCreatedAudit(user)
	customer.Raise(&Created{EventBase: shared.NewEventBase(), ID: id.String(), Email: customer.Email})

	return customer
}

// Block stops the customer from completing orders.
func (c *Customer) Block() {
	c.IsBlocked = true
	c.Raise(&Blocked{EventBase: shared.NewEventBase(), ID: c.ID.String()})
}

// RegisterCompletedOrder counts one more completed order.
func (c *Customer) RegisterCompletedOrder() error {
	if c.IsBlocked {
		return shared.BusinessRule(BlockedCode, "A blocked customer cannot complete orders.")
	}

	c.CompletedOrderCount++

	return nil
}
```

Package `customers`, no imports:

```go
// The rule codes and lengths of a customer.
const (
	BlockedCode    = "customer_blocked"
	NameMaxLength  = 200
	EmailMaxLength = 320
)
```

Package `customers`, imports `"{module}/internal/shared"`:

```go
// Created is raised when a customer is created.
type Created struct {
	shared.EventBase
	ID    string
	Email string
}

// EventName is the event's wire name.
func (*Created) EventName() string { return "{Name}.Domain.Aggregates.Customers.Events:Created" }

// Blocked is raised when a customer is blocked.
type Blocked struct {
	shared.EventBase
	ID string
}

// EventName is the event's wire name.
func (*Blocked) EventName() string { return "{Name}.Domain.Aggregates.Customers.Events:Blocked" }
```

Package `customers`, imports `"context"`, `"github.com/google/uuid"`:

```go
// Repository stores customers. The ForUpdate read locks the row until the transaction ends.
type Repository interface {
	Create(ctx context.Context, customer *Customer) error
	Update(ctx context.Context, customer *Customer) error
	FindByID(ctx context.Context, id uuid.UUID) (*Customer, error)
	FindByIDForUpdate(ctx context.Context, id uuid.UUID) (*Customer, error)
}
```

## categories: an integer key the database assigns

The repository assigns the id on insert, then calls `MarkCreated`, which raises `Created` now that the id exists.

Package `categories`, imports `"errors"`, `"strconv"`, `"strings"`, `"{module}/internal/shared"`:

```go
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
```

Package `categories`, no imports:

```go
// NameMaxLength is the longest name a category can have.
const NameMaxLength = 100
```

Package `categories`, imports `"{module}/internal/shared"`:

```go
// Created is raised when a category has its id.
type Created struct {
	shared.EventBase
	ID   string
	Name string
}

// EventName is the event's wire name.
func (*Created) EventName() string { return "{Name}.Domain.Aggregates.Categories.Events:Created" }
```

Package `categories`, imports `"context"`:

```go
// Repository stores categories.
type Repository interface {
	Create(ctx context.Context, category *Category) error
	FindByID(ctx context.Context, id int) (*Category, error)
}
```
