# Tests

The `testing` package, table tests where a unit has several cases, and no mocking library: `internal/testsupport` has an in-memory unit of work and repositories that behave like the real ones (events raised on write, sync handlers before the end, consumers' events recorded). Unit tests need no database. Test files sit beside the code, in the external test package (`products_test`) unless they test unexported code.

Package `testsupport`, imports `"context"`, `"{module}/internal/shared"`:

```go
// UnitOfWork runs work without a database: sync handlers before the end, consumers after, like the real one.
type UnitOfWork struct {
	Events    *shared.EventHandlers
	Published []shared.Event
	depth     int
}

// InTransaction runs work; the outermost call runs the sync handlers and records the events consumers would get.
func (u *UnitOfWork) InTransaction(ctx context.Context, _ string, work func(ctx context.Context) error) error {
	u.depth++
	defer func() { u.depth-- }()

	if err := work(ctx); err != nil {
		shared.EventContextOf(ctx).Clear()

		return err
	}

	if u.depth > 1 {
		return nil
	}

	if err := u.Events.ExecuteRaised(ctx); err != nil {
		shared.EventContextOf(ctx).Clear()

		return err
	}

	u.Published = append(u.Published, shared.EventContextOf(ctx).TakeDispatched()...)

	return nil
}
```

With the example:

Package `testsupport`, imports `"context"`, `"{module}/internal/domain/customers"`, `"{module}/internal/domain/orders"`, `"{module}/internal/domain/products"`, `"{module}/internal/shared"`, `"github.com/google/uuid"`. In parts, in file order:

```go
func raise(ctx context.Context, aggregate shared.Aggregate) {
	if eventContext := shared.EventContextOf(ctx); eventContext != nil {
		for _, event := range aggregate.Events() {
			eventContext.AddRaised(event)
		}
	}

	aggregate.ClearEvents()
}

// Products keeps products in memory.
type Products struct {
	Items map[uuid.UUID]*products.Product
}

// NewProducts returns an empty store holding the given products.
func NewProducts(items ...*products.Product) *Products {
	store := &Products{Items: map[uuid.UUID]*products.Product{}}
	for _, item := range items {
		item.ClearEvents()
		store.Items[item.ID] = item
	}

	return store
}

// Create stores a product.
func (p *Products) Create(ctx context.Context, product *products.Product) error {
	p.Items[product.ID] = product
	raise(ctx, product)

	return nil
}

// Update stores a product's changes.
func (p *Products) Update(ctx context.Context, product *products.Product) error {
	p.Items[product.ID] = product
	raise(ctx, product)

	return nil
}

// FindByID returns a product, or nil.
func (p *Products) FindByID(_ context.Context, id uuid.UUID) (*products.Product, error) {
	return p.Items[id], nil
}

// FindBySKUForUpdate returns the product with a SKU, or nil.
func (p *Products) FindBySKUForUpdate(_ context.Context, sku string) (*products.Product, error) {
	for _, product := range p.Items {
		if product.SKU == sku {
			return product, nil
		}
	}

	return nil, nil
}
```

```go
// Customers keeps customers in memory.
type Customers struct {
	Items map[uuid.UUID]*customers.Customer
}

// NewCustomers returns a store holding the given customers.
func NewCustomers(items ...*customers.Customer) *Customers {
	store := &Customers{Items: map[uuid.UUID]*customers.Customer{}}
	for _, item := range items {
		item.ClearEvents()
		store.Items[item.ID] = item
	}

	return store
}

// Create stores a customer.
func (c *Customers) Create(ctx context.Context, customer *customers.Customer) error {
	c.Items[customer.ID] = customer
	raise(ctx, customer)

	return nil
}

// Update stores a customer's changes.
func (c *Customers) Update(ctx context.Context, customer *customers.Customer) error {
	c.Items[customer.ID] = customer
	raise(ctx, customer)

	return nil
}

// FindByID returns a customer, or nil.
func (c *Customers) FindByID(_ context.Context, id uuid.UUID) (*customers.Customer, error) {
	return c.Items[id], nil
}

// FindByIDForUpdate returns a customer, or nil.
func (c *Customers) FindByIDForUpdate(_ context.Context, id uuid.UUID) (*customers.Customer, error) {
	return c.Items[id], nil
}

// Orders keeps orders in memory.
type Orders struct {
	Items map[uuid.UUID]*orders.Order
}

// NewOrders returns a store holding the given orders.
func NewOrders(items ...*orders.Order) *Orders {
	store := &Orders{Items: map[uuid.UUID]*orders.Order{}}
	for _, item := range items {
		item.ClearEvents()
		store.Items[item.ID] = item
	}

	return store
}
```

```go
// Create stores an order.
func (o *Orders) Create(ctx context.Context, order *orders.Order) error {
	o.Items[order.ID] = order
	raise(ctx, order)

	return nil
}

// Update stores an order's changes.
func (o *Orders) Update(ctx context.Context, order *orders.Order) error {
	o.Items[order.ID] = order
	raise(ctx, order)

	return nil
}

// FindByID returns an order, or nil.
func (o *Orders) FindByID(_ context.Context, id uuid.UUID) (*orders.Order, error) {
	return o.Items[id], nil
}

// FindByIDForUpdate returns an order, or nil.
func (o *Orders) FindByIDForUpdate(_ context.Context, id uuid.UUID) (*orders.Order, error) {
	return o.Items[id], nil
}
```

## The kernel's tests

Package `shared_test`, imports `"context"`, `"errors"`, `"testing"`, `"{module}/internal/shared"`, `"{module}/internal/testsupport"`. In parts, in file order:

```go
type probeCommand struct {
	shared.WithContext
	shared.InTransaction
	Name string
}

func (c *probeCommand) Validate() shared.FieldErrors {
	fieldErrors := shared.FieldErrors{}
	fieldErrors.NotEmpty("Name", c.Name)

	return fieldErrors
}

type probeHandler struct {
	err  error
	seen *shared.CommandContext
}

func (h *probeHandler) Handle(_ context.Context, command *probeCommand) (string, error) {
	h.seen = &command.Context

	return command.Name, h.err
}

func TestDispatch_WithAValidCommand_CompletesTheContextAndReturnsTheResult(t *testing.T) {
	handler := &probeHandler{}
	dispatcher := shared.NewDispatcher(&testsupport.UnitOfWork{Events: shared.NewEventHandlers()})
	shared.Register(dispatcher, handler)

	result, err := shared.Send[string](context.Background(), dispatcher, &probeCommand{Name: "first"})

	if err != nil || result != "first" {
		t.Fatalf("got %q, %v", result, err)
	}

	if handler.seen.CorrelationID.String() == "00000000-0000-0000-0000-000000000000" {
		t.Fatal("the context has no correlation id")
	}
}

func TestDispatch_WithAnInvalidCommand_FailsValidationWithoutRunningTheHandler(t *testing.T) {
	handler := &probeHandler{}
	dispatcher := shared.NewDispatcher(&testsupport.UnitOfWork{Events: shared.NewEventHandlers()})
	shared.Register(dispatcher, handler)

	_, err := shared.Send[string](context.Background(), dispatcher, &probeCommand{})

	failure := shared.AsError(err)
	if failure == nil || failure.Code != "validation_failed" || len(failure.Fields["Name"]) != 1 || handler.seen != nil {
		t.Fatalf("got %v", err)
	}
}
```

```go
func TestDispatch_RunsTheMiddlewaresInOrderAroundTheHandler(t *testing.T) {
	order := []string{}
	middleware := func(name string) shared.Middleware {
		return func(ctx context.Context, _ any, next func(ctx context.Context) (any, error)) (any, error) {
			order = append(order, name)

			return next(ctx)
		}
	}
	dispatcher := shared.NewDispatcher(&testsupport.UnitOfWork{Events: shared.NewEventHandlers()}, middleware("outer"), middleware("inner"))
	shared.Register(dispatcher, &probeHandler{})

	_, _ = shared.Send[string](context.Background(), dispatcher, &probeCommand{Name: "first"})

	if len(order) != 2 || order[0] != "outer" || order[1] != "inner" {
		t.Fatalf("got %v", order)
	}
}

func TestDispatch_WithAFailingHandler_ReturnsItsError(t *testing.T) {
	dispatcher := shared.NewDispatcher(&testsupport.UnitOfWork{Events: shared.NewEventHandlers()})
	shared.Register(dispatcher, &probeHandler{err: errors.New("broken")})

	_, err := shared.Send[string](context.Background(), dispatcher, &probeCommand{Name: "first"})

	if err == nil || err.Error() != "broken" {
		t.Fatalf("got %v", err)
	}
}

func TestNew_FindsACommandIgnoringCaseAndTheSuffix(t *testing.T) {
	dispatcher := shared.NewDispatcher(&testsupport.UnitOfWork{Events: shared.NewEventHandlers()})
	shared.Register(dispatcher, &probeHandler{})

	for _, name := range []string{"probeCommand", "PROBE", "probecommand"} {
		if _, ok := dispatcher.New(name); !ok {
			t.Fatalf("%s was not found", name)
		}
	}
}
```

Package `shared_test`, imports `"testing"`, `"{module}/internal/shared"`:

```go
type probeEvent struct {
	shared.EventBase
}

func (*probeEvent) EventName() string { return "Probe" }

func TestAddRaised_InsideACommand_StampsTheEventWithItsContext(t *testing.T) {
	eventContext := &shared.EventContext{}
	commandContext := shared.CommandContext{User: shared.AuditUser{ID: "user-1", Email: "user-1@localhost"}, TenantID: "t-1"}.Complete()
	eventContext.EnterCommand(commandContext)
	event := &probeEvent{EventBase: shared.NewEventBase()}

	eventContext.AddRaised(event)

	base := eventContext.TakeRaised().Base()
	if base.CorrelationID != commandContext.CorrelationID || base.UserID != "user-1" || base.TenantID != "t-1" {
		t.Fatalf("got %+v", base)
	}
}

func TestFromEvent_IsCausedByTheEventInTheSameRequest(t *testing.T) {
	event := &probeEvent{EventBase: shared.NewEventBase()}
	event.CorrelationID = shared.NewID()
	event.UserID = "user-1"

	commandContext := shared.FromEvent(event)

	if commandContext.CausationID != event.EventID || commandContext.CorrelationID != event.CorrelationID || commandContext.User.ID != "user-1" {
		t.Fatalf("got %+v", commandContext)
	}
}
```

## The example's tests

Package `products_test`, imports `"testing"`, `"{module}/internal/domain/products"`, `"{module}/internal/shared"`:

```go
func TestNew_WithNegativeStock_FailsInvalidParameter(t *testing.T) {
	_, err := products.New(shared.NewID(), "SKU-1", "Bolt", -1, shared.AuditUser{}, nil)

	if failure := shared.AsError(err); failure == nil || failure.Code != "invalid_parameter" {
		t.Fatalf("got %v", err)
	}
}

func TestNew_RaisesCreated(t *testing.T) {
	product, _ := products.New(shared.NewID(), "SKU-1", "Bolt", 1, shared.AuditUser{ID: "user-1"}, nil)

	if created, ok := product.Events()[0].(*products.Created); !ok || created.SKU != "SKU-1" || product.CreatedUserID != "user-1" {
		t.Fatalf("got %+v", product.Events())
	}
}

func TestDecreaseStock(t *testing.T) {
	cases := []struct {
		name     string
		quantity int
		code     string
		stock    int
	}{
		{"takes the units", 3, "", 7},
		{"refuses more than the stock", 11, products.InsufficientStockCode, 10},
		{"refuses zero", 0, "invalid_parameter", 10},
	}

	for _, testCase := range cases {
		t.Run(testCase.name, func(t *testing.T) {
			product, _ := products.New(shared.NewID(), "SKU-1", "Bolt", 10, shared.AuditUser{}, nil)
			product.ClearEvents()

			err := product.DecreaseStock(testCase.quantity)

			code := ""
			if failure := shared.AsError(err); failure != nil {
				code = failure.Code
			}

			if code != testCase.code || product.Stock != testCase.stock {
				t.Fatalf("got %q and %d", code, product.Stock)
			}
		})
	}
}
```

Package `orders_test`, imports `"testing"`, `"{module}/internal/domain/orders"`, `"{module}/internal/shared"`, `"github.com/shopspring/decimal"`. In parts, in file order:

```go
func newOrder(t *testing.T) *orders.Order {
	t.Helper()

	order := orders.New(shared.NewID(), "A-1", shared.NewID(), shared.AuditUser{})
	price, _ := orders.NewMoney(decimal.RequireFromString("10.50"), "eur")

	if _, err := order.AddLine("SKU-1", price, 2); err != nil {
		t.Fatal(err)
	}

	order.ClearEvents()

	return order
}

func codeOf(err error) string {
	if failure := shared.AsError(err); failure != nil {
		return failure.Code
	}

	return ""
}

func TestAddLine_WithTheSameActiveSku_FailsDuplicate(t *testing.T) {
	order := newOrder(t)
	price, _ := orders.NewMoney(decimal.NewFromInt(1), "EUR")

	_, err := order.AddLine("SKU-1", price, 1)

	if codeOf(err) != "duplicate" {
		t.Fatalf("got %v", err)
	}
}

func TestAddLine_WithAnotherCurrency_FailsMixedCurrency(t *testing.T) {
	order := newOrder(t)
	price, _ := orders.NewMoney(decimal.NewFromInt(1), "USD")

	_, err := order.AddLine("SKU-2", price, 1)

	if codeOf(err) != orders.MixedCurrencyCode {
		t.Fatalf("got %v", err)
	}
}

func TestTotalAmount_LeavesOutCancelledLines(t *testing.T) {
	order := newOrder(t)
	price, _ := orders.NewMoney(decimal.NewFromInt(4), "EUR")
	line, _ := order.AddLine("SKU-2", price, 1)

	_ = order.CancelLine(line.ID, "not needed")

	if !order.TotalAmount().Equal(decimal.NewFromInt(21)) {
		t.Fatalf("got %s", order.TotalAmount())
	}
}
```

```go
func TestCancelWhenAllLinesCancelled_CancelsOnlyWhenNoLineIsActive(t *testing.T) {
	order := newOrder(t)

	if order.CancelWhenAllLinesCancelled() {
		t.Fatal("an order with an active line was cancelled")
	}

	_ = order.CancelLine(order.Lines[0].ID, "gone")

	if !order.CancelWhenAllLinesCancelled() || order.Status != orders.StatusCancelled {
		t.Fatalf("got %s", order.Status)
	}
}

func TestComplete_WithoutAnActiveLine_FailsNothingToFulfil(t *testing.T) {
	order := newOrder(t)
	_ = order.CancelLine(order.Lines[0].ID, "gone")

	if err := order.Complete(); codeOf(err) != orders.NothingToFulfilCode {
		t.Fatalf("got %v", err)
	}
}

func TestComplete_WhenNotOpen_FailsNotOpen(t *testing.T) {
	order := newOrder(t)
	_ = order.Complete()

	if err := order.Complete(); codeOf(err) != orders.NotOpenCode {
		t.Fatalf("got %v", err)
	}
}
```

Package `customers_test`, imports `"testing"`, `"{module}/internal/domain/customers"`, `"{module}/internal/shared"`:

```go
func TestNew_StoresTheEmailTrimmedAndLowerCased(t *testing.T) {
	customer := customers.New(shared.NewID(), "Ada", "  Ada@Example.COM ", shared.AuditUser{})

	if customer.Email != "ada@example.com" {
		t.Fatalf("got %q", customer.Email)
	}
}

func TestRegisterCompletedOrder_WhenBlocked_FailsCustomerBlocked(t *testing.T) {
	customer := customers.New(shared.NewID(), "Ada", "ada@example.com", shared.AuditUser{})
	customer.Block()

	err := customer.RegisterCompletedOrder()

	if failure := shared.AsError(err); failure == nil || failure.Code != customers.BlockedCode || customer.CompletedOrderCount != 0 {
		t.Fatalf("got %v", err)
	}
}
```

`internal/application/flow_test.go` runs the real handlers and sync handlers through the dispatcher over the in-memory doubles (`Categories` is left nil: no flow touches a category):

Package `application_test`, imports `"context"`, `"testing"`, `"{module}/internal/application"`, `appcustomers "{module}/internal/application/customers"`, `apporders "{module}/internal/application/orders"`, `"{module}/internal/domain/customers"`, `"{module}/internal/domain/orders"`, `"{module}/internal/domain/products"`, `"{module}/internal/shared"`, `"{module}/internal/testsupport"`, `"github.com/google/uuid"`, `"github.com/shopspring/decimal"`. In parts, in file order:

```go
type harness struct {
	dispatcher *shared.Dispatcher
	unitOfWork *testsupport.UnitOfWork
	products   *testsupport.Products
	customers  *testsupport.Customers
	orders     *testsupport.Orders
}

func newHarness(t *testing.T) harness {
	t.Helper()

	product, _ := products.New(shared.NewID(), "SKU-1", "Bolt", 10, shared.AuditUser{}, nil)
	customer := customers.New(shared.NewID(), "Ada", "ada@example.com", shared.AuditUser{})
	events := shared.NewEventHandlers()
	unitOfWork := &testsupport.UnitOfWork{Events: events}
	h := harness{dispatcher: shared.NewDispatcher(unitOfWork), unitOfWork: unitOfWork, products: testsupport.NewProducts(product), customers: testsupport.NewCustomers(customer), orders: testsupport.NewOrders()}
	application.Register(h.dispatcher, events, application.Repositories{Products: h.products, Orders: h.orders, Customers: h.customers, Categories: nil})

	return h
}

func (h harness) userContext() shared.CommandContext {
	return shared.CommandContext{User: shared.AuditUser{ID: "user-1"}}
}

func (h harness) createOrder(t *testing.T) apporders.OrderDto {
	t.Helper()

	var customerID uuid.UUID
	for id := range h.customers.Items {
		customerID = id
	}

	order, err := shared.Send[apporders.OrderDto](context.Background(), h.dispatcher, &apporders.CreateOrderCommand{
		WithContext: shared.WithContext{Context: h.userContext()},
		OrderNumber: "A-1",
		CustomerID:  customerID,
		Lines:       []orders.LineModel{{SKU: "SKU-1", Price: decimal.RequireFromString("10.50"), Currency: "EUR", Quantity: 2}},
	})
	if err != nil {
		t.Fatal(err)
	}

	return order
}
```

```go
func TestCreateOrder_PublishesLineAddedForTheConsumersAfterTheCommand(t *testing.T) {
	h := newHarness(t)

	order := h.createOrder(t)

	names := []string{}
	for _, event := range h.unitOfWork.Published {
		names = append(names, event.EventName())
	}

	if order.TotalAmount.String() != "21" || len(names) != 2 || names[1] != (&orders.LineAdded{}).EventName() {
		t.Fatalf("got %s and %v", order.TotalAmount, names)
	}
}

func TestCompleteOrder_CountsTheOrderInTheSameUnitOfWork(t *testing.T) {
	h := newHarness(t)
	order := h.createOrder(t)

	_, err := shared.Send[apporders.OrderDto](context.Background(), h.dispatcher, &apporders.CompleteOrderCommand{WithContext: shared.WithContext{Context: h.userContext()}, ID: order.ID})

	if err != nil || h.customers.Items[order.CustomerID].CompletedOrderCount != 1 {
		t.Fatalf("got %v", err)
	}
}

func TestCompleteOrder_ForABlockedCustomer_FailsAndPublishesNothing(t *testing.T) {
	h := newHarness(t)
	order := h.createOrder(t)
	h.unitOfWork.Published = nil
	_, _ = shared.Send[appcustomers.CustomerDto](context.Background(), h.dispatcher, &appcustomers.BlockCustomerCommand{WithContext: shared.WithContext{Context: h.userContext()}, ID: order.CustomerID})
	h.unitOfWork.Published = nil

	_, err := shared.Send[apporders.OrderDto](context.Background(), h.dispatcher, &apporders.CompleteOrderCommand{WithContext: shared.WithContext{Context: h.userContext()}, ID: order.ID})

	if failure := shared.AsError(err); failure == nil || failure.Code != customers.BlockedCode || len(h.unitOfWork.Published) != 0 {
		t.Fatalf("got %v and %d published", err, len(h.unitOfWork.Published))
	}
}

func TestCancelOrderLine_OfTheLastActiveLine_CancelsTheOrder(t *testing.T) {
	h := newHarness(t)
	order := h.createOrder(t)

	cancelled, err := shared.Send[apporders.OrderDto](context.Background(), h.dispatcher, &apporders.CancelOrderLineCommand{WithContext: shared.WithContext{Context: h.userContext()}, ID: order.ID, LineID: order.Lines[0].ID, Reason: "gone"})

	if err != nil || h.orders.Items[order.ID].Status != orders.StatusCancelled || !cancelled.Lines[0].IsCancelled {
		t.Fatalf("got %v", err)
	}
}
```

## The hosts' tests

Package `main`, imports `"testing"`:

```go
func TestParse_ReadsTheOptionsBeforeTheNameAndJSON(t *testing.T) {
	arguments, err := Parse([]string{"--", "--user", "u-1", "--email", "u-1@localhost", "CreateProduct", `{"sku":"A"}`})

	if err != nil || arguments.UserID != "u-1" || arguments.Email != "u-1@localhost" || arguments.Name != "CreateProduct" || arguments.JSON != `{"sku":"A"}` {
		t.Fatalf("got %+v, %v", arguments, err)
	}
}

func TestParse_WithAnOptionLackingItsValue_Fails(t *testing.T) {
	if _, err := Parse([]string{"--user"}); err == nil {
		t.Fatal("no error")
	}
}
```

Package `api`, imports `"net/http/httptest"`, `"strings"`, `"testing"`, `"{module}/internal/shared"`:

```go
func TestWriteJSON_WritesCamelCaseMembers(t *testing.T) {
	recorder := httptest.NewRecorder()

	WriteJSON(recorder, 200, struct {
		OrderNumber string
		Lines       []struct{ Sku string }
	}{OrderNumber: "A-1", Lines: []struct{ Sku string }{{Sku: "S"}}})

	if body := strings.TrimSpace(recorder.Body.String()); body != `{"lines":[{"sku":"S"}],"orderNumber":"A-1"}` {
		t.Fatalf("got %s", body)
	}
}

func TestWriteError_KeepsTheFieldNamesOfAValidationFailure(t *testing.T) {
	recorder := httptest.NewRecorder()

	WriteError(recorder, nil, shared.ValidationFailed(map[string][]string{"Lines": {"'Lines' must not be empty."}}))

	if recorder.Code != 422 || !strings.Contains(recorder.Body.String(), `"Lines"`) {
		t.Fatalf("got %d %s", recorder.Code, recorder.Body.String())
	}
}

func TestWriteError_WithAnUnexpectedError_Answers500WithAReference(t *testing.T) {
	recorder := httptest.NewRecorder()

	WriteError(recorder, nil, errTest("broken"))

	if recorder.Code != 500 || !strings.Contains(recorder.Body.String(), UnexpectedErrorCode) || !strings.Contains(recorder.Body.String(), "Reference ") {
		t.Fatalf("got %d %s", recorder.Code, recorder.Body.String())
	}
}

type errTest string

func (e errTest) Error() string { return string(e) }
```

Package `persistence`, imports `"testing"`:

```go
func TestConnectionString(t *testing.T) {
	cases := map[string]string{
		"Host=db;Port=5432;Database=shop;Username=app;Password=secret": "host='db' port='5432' dbname='shop' user='app' password='secret'",
		"postgres://app:secret@db:5432/shop":                           "postgres://app:secret@db:5432/shop",
	}

	for given, expected := range cases {
		if actual := ConnectionString(given); actual != expected {
			t.Fatalf("%s: got %s", given, actual)
		}
	}
}
```

The database behaviour (locks, foreign keys, the publisher after commit) is proved by `verify.sh`'s conformance scripts against a real PostgreSQL.
