# Commands and handlers: internal/application

One package per aggregate, importing its own domain package as `domain`. A command is a struct embedding `shared.WithContext`, and `shared.InTransaction` when it writes; it validates its own shape in `Validate`. Its handler is a struct holding the repositories it needs, with one method `Handle(ctx, *Command) (Result, error)`. A command that reads (`Get...Command`) has no transaction. A missing id in a create command gets `shared.NewID()` in the handler.

## products, in full

Package `products`, imports `domain "{module}/internal/domain/products"`, `"github.com/google/uuid"`:

```go
// ProductDto is how a product is shown.
type ProductDto struct {
	ID         uuid.UUID `json:"Id"`
	SKU        string    `json:"Sku"`
	Name       string    `json:"Name"`
	Stock      int       `json:"Stock"`
	CategoryID *int      `json:"CategoryId"`
}

// MapProduct shows a product.
func MapProduct(product *domain.Product) ProductDto {
	return ProductDto{ID: product.ID, SKU: product.SKU, Name: product.Name, Stock: product.Stock, CategoryID: product.CategoryID}
}
```

Package `products`, imports `"context"`, `domain "{module}/internal/domain/products"`, `"{module}/internal/shared"`, `"github.com/google/uuid"`:

```go
// CreateProductCommand creates a product with its initial stock.
type CreateProductCommand struct {
	shared.WithContext
	shared.InTransaction
	ID         uuid.UUID `json:"Id"`
	SKU        string    `json:"Sku"`
	Name       string    `json:"Name"`
	Stock      int       `json:"Stock"`
	CategoryID *int      `json:"CategoryId"`
}

// Validate checks the command's shape.
func (c *CreateProductCommand) Validate() shared.FieldErrors {
	fieldErrors := shared.FieldErrors{}
	fieldErrors.NotEmpty("Sku", c.SKU)
	fieldErrors.MaxLength("Sku", c.SKU, domain.SKUMaxLength)
	fieldErrors.NotEmpty("Name", c.Name)
	fieldErrors.MaxLength("Name", c.Name, domain.NameMaxLength)
	fieldErrors.Check(c.Stock >= 0, "Stock", "'Stock' must be greater than or equal to '0'.")
	fieldErrors.Check(c.CategoryID == nil || *c.CategoryID > 0, "CategoryId", "'CategoryId' must be greater than '0'.")

	return fieldErrors
}

// CreateProductHandler runs CreateProductCommand.
type CreateProductHandler struct {
	Products domain.Repository
}

// Handle creates the product.
func (h CreateProductHandler) Handle(ctx context.Context, command *CreateProductCommand) (ProductDto, error) {
	if command.ID == uuid.Nil {
		command.ID = shared.NewID()
	}

	product, err := domain.New(command.ID, command.SKU, command.Name, command.Stock, command.Context.User, command.CategoryID)
	if err != nil {
		return ProductDto{}, err
	}

	if err := h.Products.Create(ctx, product); err != nil {
		return ProductDto{}, err
	}

	return MapProduct(product), nil
}
```

Package `products`, imports `"context"`, `domain "{module}/internal/domain/products"`, `"{module}/internal/shared"`, `"github.com/google/uuid"`:

```go
// GetProductCommand reads one product.
type GetProductCommand struct {
	shared.WithContext
	ID uuid.UUID `json:"Id"`
}

// GetProductHandler runs GetProductCommand.
type GetProductHandler struct {
	Products domain.Repository
}

// Handle reads the product.
func (h GetProductHandler) Handle(ctx context.Context, command *GetProductCommand) (ProductDto, error) {
	product, err := h.Products.FindByID(ctx, command.ID)
	if err != nil {
		return ProductDto{}, err
	}

	if product == nil {
		return ProductDto{}, shared.NotFound("Product")
	}

	return MapProduct(product), nil
}
```

Package `products`, imports `"context"`, `domain "{module}/internal/domain/products"`, `"{module}/internal/shared"`:

```go
// DecreaseProductStockCommand decreases the stock of the product with a SKU.
type DecreaseProductStockCommand struct {
	shared.WithContext
	shared.InTransaction
	SKU      string `json:"Sku"`
	Quantity int    `json:"Quantity"`
}

// Validate checks the command's shape.
func (c *DecreaseProductStockCommand) Validate() shared.FieldErrors {
	fieldErrors := shared.FieldErrors{}
	fieldErrors.NotEmpty("Sku", c.SKU)
	fieldErrors.MaxLength("Sku", c.SKU, domain.SKUMaxLength)
	fieldErrors.Check(c.Quantity > 0, "Quantity", "'Quantity' must be greater than '0'.")

	return fieldErrors
}

// DecreaseProductStockHandler runs DecreaseProductStockCommand.
type DecreaseProductStockHandler struct {
	Products domain.Repository
}

// Handle changes the stock, holding the product's row until the transaction ends.
func (h DecreaseProductStockHandler) Handle(ctx context.Context, command *DecreaseProductStockCommand) (ProductDto, error) {
	product, err := h.Products.FindBySKUForUpdate(ctx, command.SKU)
	if err != nil {
		return ProductDto{}, err
	}

	if product == nil {
		return ProductDto{}, shared.NotFound("Product")
	}

	if err := product.DecreaseStock(command.Quantity); err != nil {
		return ProductDto{}, err
	}

	product.SetUpdatedAudit(command.Context.User)

	if err := h.Products.Update(ctx, product); err != nil {
		return ProductDto{}, err
	}

	return MapProduct(product), nil
}
```

`increase_product_stock.go` is the same with `Increase` for `Decrease`.

A consumer reacts after the order committed, as the user whose command raised the event:

Package `products`, imports `"context"`, `"{module}/internal/domain/orders"`, `"{module}/internal/shared"`:

```go
// DecreaseStockWhenOrderLineAddedConsumer takes the ordered units from the stock after the order committed.
type DecreaseStockWhenOrderLineAddedConsumer struct {
	Dispatcher *shared.Dispatcher
}

// Handle sends DecreaseProductStockCommand as the user whose command added the line.
func (c DecreaseStockWhenOrderLineAddedConsumer) Handle(ctx context.Context, event shared.Event) error {
	lineAdded := event.(*orders.LineAdded)

	_, err := shared.Send[ProductDto](ctx, c.Dispatcher, &DecreaseProductStockCommand{WithContext: shared.WithContext{Context: shared.FromEvent(event)}, SKU: lineAdded.SKU, Quantity: lineAdded.Quantity})

	return err
}
```

`increase_stock_when_order_line_cancelled.go` is the same over `orders.LineCancelled`, sending `IncreaseProductStockCommand`.

## orders

Package `orders`, imports `domain "{module}/internal/domain/orders"`, `"github.com/google/uuid"`, `"github.com/shopspring/decimal"`:

```go
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
```

Package `orders`, imports `domain "{module}/internal/domain/orders"`, `"{module}/internal/shared"`:

```go
// ValidateLine checks the shape of one order line under a field prefix.
func ValidateLine(fieldErrors shared.FieldErrors, prefix string, line domain.LineModel) {
	fieldErrors.NotEmpty(prefix+"Sku", line.SKU)
	fieldErrors.MaxLength(prefix+"Sku", line.SKU, domain.SKUMaxLength)
	fieldErrors.Check(!line.Price.IsNegative(), prefix+"Price", "'Price' must be greater than or equal to '0'.")
	fieldErrors.Check(len(line.Currency) == domain.CurrencyLength, prefix+"Currency", "'Currency' must be 3 characters in length.")
	fieldErrors.Check(line.Quantity > 0, prefix+"Quantity", "'Quantity' must be greater than '0'.")
}
```

Package `orders`, imports `"context"`, `domain "{module}/internal/domain/orders"`, `"{module}/internal/shared"`, `"github.com/google/uuid"`:

```go
// LoadForUpdate reads an order to change it, or fails with not_found.
func LoadForUpdate(ctx context.Context, orders domain.Repository, id uuid.UUID) (*domain.Order, error) {
	order, err := orders.FindByIDForUpdate(ctx, id)
	if err != nil {
		return nil, err
	}

	if order == nil {
		return nil, shared.NotFound("Order")
	}

	return order, nil
}

// Save records who changed the order and stores it.
func Save(ctx context.Context, orders domain.Repository, order *domain.Order, user shared.AuditUser) (OrderDto, error) {
	order.SetUpdatedAudit(user)

	if err := orders.Update(ctx, order); err != nil {
		return OrderDto{}, err
	}

	return MapOrder(order), nil
}
```

Package `orders`, imports `"context"`, `"fmt"`, `domain "{module}/internal/domain/orders"`, `"{module}/internal/shared"`, `"github.com/google/uuid"`:

```go
// CreateOrderCommand opens an order with its lines.
type CreateOrderCommand struct {
	shared.WithContext
	shared.InTransaction
	ID          uuid.UUID          `json:"Id"`
	OrderNumber string             `json:"OrderNumber"`
	CustomerID  uuid.UUID          `json:"CustomerId"`
	Lines       []domain.LineModel `json:"Lines"`
}

// Validate checks the command's shape.
func (c *CreateOrderCommand) Validate() shared.FieldErrors {
	fieldErrors := shared.FieldErrors{}
	fieldErrors.NotEmpty("OrderNumber", c.OrderNumber)
	fieldErrors.MaxLength("OrderNumber", c.OrderNumber, domain.OrderNumberMaxLength)
	fieldErrors.Check(c.CustomerID != uuid.Nil, "CustomerId", "'CustomerId' must not be empty.")
	fieldErrors.Check(len(c.Lines) > 0, "Lines", "'Lines' must not be empty.")

	for index, line := range c.Lines {
		ValidateLine(fieldErrors, fmt.Sprintf("Lines[%d].", index), line)
	}

	return fieldErrors
}

// CreateOrderHandler runs CreateOrderCommand.
type CreateOrderHandler struct {
	Orders domain.Repository
}

// Handle opens the order and adds its lines.
func (h CreateOrderHandler) Handle(ctx context.Context, command *CreateOrderCommand) (OrderDto, error) {
	if command.ID == uuid.Nil {
		command.ID = shared.NewID()
	}

	order := domain.New(command.ID, command.OrderNumber, command.CustomerID, command.Context.User)

	for _, line := range command.Lines {
		price, err := domain.NewMoney(line.Price, line.Currency)
		if err != nil {
			return OrderDto{}, err
		}

		if _, err := order.AddLine(line.SKU, price, line.Quantity); err != nil {
			return OrderDto{}, err
		}
	}

	if err := h.Orders.Create(ctx, order); err != nil {
		return OrderDto{}, err
	}

	return MapOrder(order), nil
}
```

Package `orders`, imports `"context"`, `domain "{module}/internal/domain/orders"`, `"{module}/internal/shared"`, `"github.com/google/uuid"`:

```go
// GetOrderCommand reads one order with its lines.
type GetOrderCommand struct {
	shared.WithContext
	ID uuid.UUID `json:"Id"`
}

// GetOrderHandler runs GetOrderCommand.
type GetOrderHandler struct {
	Orders domain.Repository
}

// Handle reads the order.
func (h GetOrderHandler) Handle(ctx context.Context, command *GetOrderCommand) (OrderDto, error) {
	order, err := h.Orders.FindByID(ctx, command.ID)
	if err != nil {
		return OrderDto{}, err
	}

	if order == nil {
		return OrderDto{}, shared.NotFound("Order")
	}

	return MapOrder(order), nil
}
```

Package `orders`, imports `"context"`, `domain "{module}/internal/domain/orders"`, `"{module}/internal/shared"`, `"github.com/google/uuid"`:

```go
// AddOrderLineCommand adds a product to an open order.
type AddOrderLineCommand struct {
	shared.WithContext
	shared.InTransaction
	ID   uuid.UUID         `json:"Id"`
	Line *domain.LineModel `json:"Line"`
}

// Validate checks the command's shape.
func (c *AddOrderLineCommand) Validate() shared.FieldErrors {
	fieldErrors := shared.FieldErrors{}
	fieldErrors.Check(c.ID != uuid.Nil, "Id", "'Id' must not be empty.")
	fieldErrors.Check(c.Line != nil, "Line", "'Line' must not be empty.")

	if c.Line != nil {
		ValidateLine(fieldErrors, "Line.", *c.Line)
	}

	return fieldErrors
}

// AddOrderLineHandler runs AddOrderLineCommand.
type AddOrderLineHandler struct {
	Orders domain.Repository
}

// Handle adds the line.
func (h AddOrderLineHandler) Handle(ctx context.Context, command *AddOrderLineCommand) (OrderDto, error) {
	order, err := LoadForUpdate(ctx, h.Orders, command.ID)
	if err != nil {
		return OrderDto{}, err
	}

	price, err := domain.NewMoney(command.Line.Price, command.Line.Currency)
	if err != nil {
		return OrderDto{}, err
	}

	if _, err := order.AddLine(command.Line.SKU, price, command.Line.Quantity); err != nil {
		return OrderDto{}, err
	}

	return Save(ctx, h.Orders, order, command.Context.User)
}
```

Package `orders`, imports `"context"`, `domain "{module}/internal/domain/orders"`, `"{module}/internal/shared"`, `"github.com/google/uuid"`:

```go
// CancelOrderLineCommand cancels one line of an open order.
type CancelOrderLineCommand struct {
	shared.WithContext
	shared.InTransaction
	ID     uuid.UUID `json:"Id"`
	LineID uuid.UUID `json:"LineId"`
	Reason string    `json:"Reason"`
}

// Validate checks the command's shape.
func (c *CancelOrderLineCommand) Validate() shared.FieldErrors {
	fieldErrors := shared.FieldErrors{}
	fieldErrors.Check(c.ID != uuid.Nil, "Id", "'Id' must not be empty.")
	fieldErrors.Check(c.LineID != uuid.Nil, "LineId", "'LineId' must not be empty.")
	fieldErrors.NotEmpty("Reason", c.Reason)
	fieldErrors.MaxLength("Reason", c.Reason, domain.ReasonMaxLength)

	return fieldErrors
}

// CancelOrderLineHandler runs CancelOrderLineCommand.
type CancelOrderLineHandler struct {
	Orders domain.Repository
}

// Handle cancels the line.
func (h CancelOrderLineHandler) Handle(ctx context.Context, command *CancelOrderLineCommand) (OrderDto, error) {
	order, err := LoadForUpdate(ctx, h.Orders, command.ID)
	if err != nil {
		return OrderDto{}, err
	}

	if err := order.CancelLine(command.LineID, command.Reason); err != nil {
		return OrderDto{}, err
	}

	return Save(ctx, h.Orders, order, command.Context.User)
}
```

Package `orders`, imports `"context"`, `domain "{module}/internal/domain/orders"`, `"{module}/internal/shared"`, `"github.com/google/uuid"`:

```go
// CompleteOrderCommand fulfils an open order.
type CompleteOrderCommand struct {
	shared.WithContext
	shared.InTransaction
	ID uuid.UUID `json:"Id"`
}

// CompleteOrderHandler runs CompleteOrderCommand.
type CompleteOrderHandler struct {
	Orders domain.Repository
}

// Handle completes the order.
func (h CompleteOrderHandler) Handle(ctx context.Context, command *CompleteOrderCommand) (OrderDto, error) {
	order, err := LoadForUpdate(ctx, h.Orders, command.ID)
	if err != nil {
		return OrderDto{}, err
	}

	if err := order.Complete(); err != nil {
		return OrderDto{}, err
	}

	return Save(ctx, h.Orders, order, command.Context.User)
}
```

Package `orders`, imports `"context"`, `domain "{module}/internal/domain/orders"`, `"{module}/internal/shared"`, `"github.com/google/uuid"`:

```go
// CancelOrderWhenAllLinesCancelledCommand cancels an order whose last active line was cancelled.
type CancelOrderWhenAllLinesCancelledCommand struct {
	shared.WithContext
	shared.InTransaction
	ID uuid.UUID `json:"Id"`
}

// CancelOrderWhenAllLinesCancelledHandler runs CancelOrderWhenAllLinesCancelledCommand.
type CancelOrderWhenAllLinesCancelledHandler struct {
	Orders domain.Repository
}

// Handle cancels the order when every line is cancelled, and writes nothing otherwise.
func (h CancelOrderWhenAllLinesCancelledHandler) Handle(ctx context.Context, command *CancelOrderWhenAllLinesCancelledCommand) (OrderDto, error) {
	order, err := LoadForUpdate(ctx, h.Orders, command.ID)
	if err != nil {
		return OrderDto{}, err
	}

	if !order.CancelWhenAllLinesCancelled() {
		return MapOrder(order), nil
	}

	return Save(ctx, h.Orders, order, command.Context.User)
}
```

A sync handler runs in the transaction of the command that raised the event; its failure rolls that command back:

Package `orders`, imports `"context"`, `domain "{module}/internal/domain/orders"`, `"{module}/internal/shared"`, `"github.com/google/uuid"`:

```go
// CancelOrderWhenOrderLineCancelledHandler cancels the order in the transaction that cancelled its last line.
type CancelOrderWhenOrderLineCancelledHandler struct {
	Dispatcher *shared.Dispatcher
}

// Handle sends CancelOrderWhenAllLinesCancelledCommand.
func (h CancelOrderWhenOrderLineCancelledHandler) Handle(ctx context.Context, event shared.Event) error {
	lineCancelled := event.(*domain.LineCancelled)

	orderID, err := uuid.Parse(lineCancelled.ID)
	if err != nil {
		return err
	}

	_, err = shared.Send[OrderDto](ctx, h.Dispatcher, &CancelOrderWhenAllLinesCancelledCommand{WithContext: shared.WithContext{Context: shared.FromEvent(event)}, ID: orderID})

	return err
}
```

## customers

Package `customers`, imports `domain "{module}/internal/domain/customers"`, `"github.com/google/uuid"`:

```go
// CustomerDto is how a customer is shown.
type CustomerDto struct {
	ID                  uuid.UUID `json:"Id"`
	Name                string    `json:"Name"`
	Email               string    `json:"Email"`
	IsBlocked           bool      `json:"IsBlocked"`
	CompletedOrderCount int       `json:"CompletedOrderCount"`
}

// MapCustomer shows a customer.
func MapCustomer(customer *domain.Customer) CustomerDto {
	return CustomerDto{ID: customer.ID, Name: customer.Name, Email: customer.Email, IsBlocked: customer.IsBlocked, CompletedOrderCount: customer.CompletedOrderCount}
}
```

Package `customers`, imports `"context"`, `domain "{module}/internal/domain/customers"`, `"{module}/internal/shared"`, `"github.com/google/uuid"`:

```go
// CreateCustomerCommand creates a customer.
type CreateCustomerCommand struct {
	shared.WithContext
	shared.InTransaction
	ID    uuid.UUID `json:"Id"`
	Name  string    `json:"Name"`
	Email string    `json:"Email"`
}

// Validate checks the command's shape.
func (c *CreateCustomerCommand) Validate() shared.FieldErrors {
	fieldErrors := shared.FieldErrors{}
	fieldErrors.NotEmpty("Name", c.Name)
	fieldErrors.MaxLength("Name", c.Name, domain.NameMaxLength)
	fieldErrors.NotEmpty("Email", c.Email)
	fieldErrors.MaxLength("Email", c.Email, domain.EmailMaxLength)

	return fieldErrors
}

// CreateCustomerHandler runs CreateCustomerCommand.
type CreateCustomerHandler struct {
	Customers domain.Repository
}

// Handle creates the customer.
func (h CreateCustomerHandler) Handle(ctx context.Context, command *CreateCustomerCommand) (CustomerDto, error) {
	if command.ID == uuid.Nil {
		command.ID = shared.NewID()
	}

	customer := domain.New(command.ID, command.Name, command.Email, command.Context.User)

	if err := h.Customers.Create(ctx, customer); err != nil {
		return CustomerDto{}, err
	}

	return MapCustomer(customer), nil
}
```

Package `customers`, imports `"context"`, `domain "{module}/internal/domain/customers"`, `"{module}/internal/shared"`, `"github.com/google/uuid"`:

```go
// GetCustomerCommand reads one customer.
type GetCustomerCommand struct {
	shared.WithContext
	ID uuid.UUID `json:"Id"`
}

// GetCustomerHandler runs GetCustomerCommand.
type GetCustomerHandler struct {
	Customers domain.Repository
}

// Handle reads the customer.
func (h GetCustomerHandler) Handle(ctx context.Context, command *GetCustomerCommand) (CustomerDto, error) {
	customer, err := h.Customers.FindByID(ctx, command.ID)
	if err != nil {
		return CustomerDto{}, err
	}

	if customer == nil {
		return CustomerDto{}, shared.NotFound("Customer")
	}

	return MapCustomer(customer), nil
}
```

Package `customers`, imports `"context"`, `domain "{module}/internal/domain/customers"`, `"{module}/internal/shared"`, `"github.com/google/uuid"`:

```go
// BlockCustomerCommand stops a customer from completing orders.
type BlockCustomerCommand struct {
	shared.WithContext
	shared.InTransaction
	ID uuid.UUID `json:"Id"`
}

// BlockCustomerHandler runs BlockCustomerCommand.
type BlockCustomerHandler struct {
	Customers domain.Repository
}

// Handle blocks the customer.
func (h BlockCustomerHandler) Handle(ctx context.Context, command *BlockCustomerCommand) (CustomerDto, error) {
	customer, err := h.Customers.FindByIDForUpdate(ctx, command.ID)
	if err != nil {
		return CustomerDto{}, err
	}

	if customer == nil {
		return CustomerDto{}, shared.NotFound("Customer")
	}

	customer.Block()
	customer.SetUpdatedAudit(command.Context.User)

	if err := h.Customers.Update(ctx, customer); err != nil {
		return CustomerDto{}, err
	}

	return MapCustomer(customer), nil
}
```

Package `customers`, imports `"context"`, `domain "{module}/internal/domain/customers"`, `"{module}/internal/shared"`, `"github.com/google/uuid"`:

```go
// RegisterCompletedOrderCommand counts an order the customer completed.
type RegisterCompletedOrderCommand struct {
	shared.WithContext
	shared.InTransaction
	ID uuid.UUID `json:"Id"`
}

// RegisterCompletedOrderHandler runs RegisterCompletedOrderCommand.
type RegisterCompletedOrderHandler struct {
	Customers domain.Repository
}

// Handle counts the order; a blocked customer refuses it.
func (h RegisterCompletedOrderHandler) Handle(ctx context.Context, command *RegisterCompletedOrderCommand) (CustomerDto, error) {
	customer, err := h.Customers.FindByIDForUpdate(ctx, command.ID)
	if err != nil {
		return CustomerDto{}, err
	}

	if customer == nil {
		return CustomerDto{}, shared.NotFound("Customer")
	}

	if err := customer.RegisterCompletedOrder(); err != nil {
		return CustomerDto{}, err
	}

	customer.SetUpdatedAudit(command.Context.User)

	if err := h.Customers.Update(ctx, customer); err != nil {
		return CustomerDto{}, err
	}

	return MapCustomer(customer), nil
}
```

Package `customers`, imports `"context"`, `"{module}/internal/domain/orders"`, `"{module}/internal/shared"`, `"github.com/google/uuid"`:

```go
// RegisterCompletedOrderWhenOrderCompletedHandler counts the order in the transaction that completed it.
type RegisterCompletedOrderWhenOrderCompletedHandler struct {
	Dispatcher *shared.Dispatcher
}

// Handle sends RegisterCompletedOrderCommand; its failure rolls the completion back.
func (h RegisterCompletedOrderWhenOrderCompletedHandler) Handle(ctx context.Context, event shared.Event) error {
	completed := event.(*orders.Completed)

	customerID, err := uuid.Parse(completed.CustomerID)
	if err != nil {
		return err
	}

	_, err = shared.Send[CustomerDto](ctx, h.Dispatcher, &RegisterCompletedOrderCommand{WithContext: shared.WithContext{Context: shared.FromEvent(event)}, ID: customerID})

	return err
}
```

## categories

Package `categories`, imports `domain "{module}/internal/domain/categories"`:

```go
// CategoryDto is how a category is shown.
type CategoryDto struct {
	ID   int    `json:"Id"`
	Name string `json:"Name"`
}

// MapCategory shows a category.
func MapCategory(category *domain.Category) CategoryDto {
	return CategoryDto{ID: category.ID, Name: category.Name}
}
```

Package `categories`, imports `"context"`, `domain "{module}/internal/domain/categories"`, `"{module}/internal/shared"`:

```go
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
```

Package `categories`, imports `"context"`, `domain "{module}/internal/domain/categories"`, `"{module}/internal/shared"`:

```go
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
```

## Registration

`internal/application/module.go` registers every handler, sync handler and consumer. Without the example, `Repositories` is an empty struct and `Register` registers nothing yet.

Package `application`, imports `"{module}/internal/application/categories"`, `"{module}/internal/application/customers"`, `"{module}/internal/application/orders"`, `"{module}/internal/application/products"`, `domaincategories "{module}/internal/domain/categories"`, `domaincustomers "{module}/internal/domain/customers"`, `domainorders "{module}/internal/domain/orders"`, `domainproducts "{module}/internal/domain/products"`, `"{module}/internal/shared"`:

```go
// Repositories are the stores the application's handlers need.
type Repositories struct {
	Products   domainproducts.Repository
	Orders     domainorders.Repository
	Customers  domaincustomers.Repository
	Categories domaincategories.Repository
}

// Register registers every command handler, sync handler and consumer of the application.
func Register(dispatcher *shared.Dispatcher, events *shared.EventHandlers, repositories Repositories) {
	shared.Register(dispatcher, products.CreateProductHandler{Products: repositories.Products})
	shared.Register(dispatcher, products.GetProductHandler{Products: repositories.Products})
	shared.Register(dispatcher, products.DecreaseProductStockHandler{Products: repositories.Products})
	shared.Register(dispatcher, products.IncreaseProductStockHandler{Products: repositories.Products})
	shared.Register(dispatcher, customers.CreateCustomerHandler{Customers: repositories.Customers})
	shared.Register(dispatcher, customers.GetCustomerHandler{Customers: repositories.Customers})
	shared.Register(dispatcher, customers.BlockCustomerHandler{Customers: repositories.Customers})
	shared.Register(dispatcher, customers.RegisterCompletedOrderHandler{Customers: repositories.Customers})
	shared.Register(dispatcher, orders.CreateOrderHandler{Orders: repositories.Orders})
	shared.Register(dispatcher, orders.GetOrderHandler{Orders: repositories.Orders})
	shared.Register(dispatcher, orders.AddOrderLineHandler{Orders: repositories.Orders})
	shared.Register(dispatcher, orders.CancelOrderLineHandler{Orders: repositories.Orders})
	shared.Register(dispatcher, orders.CompleteOrderHandler{Orders: repositories.Orders})
	shared.Register(dispatcher, orders.CancelOrderWhenAllLinesCancelledHandler{Orders: repositories.Orders})
	shared.Register(dispatcher, categories.CreateCategoryHandler{Categories: repositories.Categories})
	shared.Register(dispatcher, categories.GetCategoryHandler{Categories: repositories.Categories})

	events.AddSync((&domainorders.Completed{}).EventName(), 0, customers.RegisterCompletedOrderWhenOrderCompletedHandler{Dispatcher: dispatcher})
	events.AddSync((&domainorders.LineCancelled{}).EventName(), 0, orders.CancelOrderWhenOrderLineCancelledHandler{Dispatcher: dispatcher})
	events.AddConsumer((&domainorders.LineAdded{}).EventName(), products.DecreaseStockWhenOrderLineAddedConsumer{Dispatcher: dispatcher})
	events.AddConsumer((&domainorders.LineCancelled{}).EventName(), products.IncreaseStockWhenOrderLineCancelledConsumer{Dispatcher: dispatcher})
}
```
