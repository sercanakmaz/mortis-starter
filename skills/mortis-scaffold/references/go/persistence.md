# PostgreSQL: internal/infrastructure/persistence

`pgx` with hand-written SQL (decision 54). A repository runs its SQL on `Database.On(ctx)`: the command's transaction when there is one, the pool otherwise. After writing an aggregate it hands the aggregate's events to the command with `RaiseEvents`.

Package `persistence`, imports `"context"`, `"github.com/jackc/pgx/v5"`, `"github.com/jackc/pgx/v5/pgconn"`, `"github.com/jackc/pgx/v5/pgxpool"`:

```go
// Querier is what repositories run SQL on: the transaction of the context, or the pool.
type Querier interface {
	Exec(ctx context.Context, sql string, arguments ...any) (pgconn.CommandTag, error)
	Query(ctx context.Context, sql string, arguments ...any) (pgx.Rows, error)
	QueryRow(ctx context.Context, sql string, arguments ...any) pgx.Row
}

type transactionKey struct{}

// Database is the connection pool and the way to the context's transaction.
type Database struct {
	Pool *pgxpool.Pool
}

// Open connects to PostgreSQL.
func Open(ctx context.Context, connectionString string) (*Database, error) {
	pool, err := pgxpool.New(ctx, ConnectionString(connectionString))
	if err != nil {
		return nil, err
	}

	return &Database{Pool: pool}, nil
}

// On returns the context's transaction, or the pool when there is none.
func (d *Database) On(ctx context.Context) Querier {
	if transaction, ok := ctx.Value(transactionKey{}).(pgx.Tx); ok {
		return transaction
	}

	return d.Pool
}

func withTransaction(ctx context.Context, transaction pgx.Tx) context.Context {
	return context.WithValue(ctx, transactionKey{}, transaction)
}

func transactionOf(ctx context.Context) (pgx.Tx, bool) {
	transaction, ok := ctx.Value(transactionKey{}).(pgx.Tx)

	return transaction, ok
}
```

`connection_string.go`: the connection string comes from `ConnectionStrings__Default`, in the URL form, the keyword form, or the semicolon form the C# projects use, so `verify.sh` sets one variable for every language.

Package `persistence`, imports `"strings"`:

```go
var keywords = map[string]string{"host": "host", "port": "port", "database": "dbname", "username": "user", "user id": "user", "password": "password", "timeout": "connect_timeout"}

// ConnectionString accepts the URL or keyword form, and the semicolon form the C# projects use (Host=...;Database=...).
func ConnectionString(value string) string {
	if !strings.Contains(value, ";") && !strings.HasPrefix(strings.ToLower(value), "host=") {
		return value
	}

	parts := []string{}

	for _, pair := range strings.Split(value, ";") {
		key, setting, found := strings.Cut(pair, "=")
		if !found {
			continue
		}

		if keyword, ok := keywords[strings.ToLower(strings.TrimSpace(key))]; ok {
			parts = append(parts, keyword+"='"+strings.ReplaceAll(strings.TrimSpace(setting), "'", `\'`)+"'")
		}
	}

	return strings.Join(parts, " ")
}
```

Package `persistence`, imports `"context"`, `"{module}/internal/shared"`:

```go
// RaiseEvents hands the aggregate's events to the command's EventContext once the aggregate is written.
func RaiseEvents(ctx context.Context, aggregate shared.Aggregate) {
	if eventContext := shared.EventContextOf(ctx); eventContext != nil {
		for _, event := range aggregate.Events() {
			eventContext.AddRaised(event)
		}
	}

	aggregate.ClearEvents()
}
```

`unit_of_work.go`: the outermost `InTransaction` begins the transaction, runs the work, runs the sync handlers of every raised event in the same transaction, commits, and only then hands the dispatched events to the publisher. A failure anywhere rolls back and forgets the events. An inner call joins the outer transaction.

Package `persistence`, imports `"context"`, `"{module}/internal/shared"`:

```go
// Publisher runs the consumers of committed events.
type Publisher interface {
	Publish(ctx context.Context, events []shared.Event)
}

// UnitOfWork runs a command in one transaction: the sync handlers before the commit, the consumers after it.
type UnitOfWork struct {
	Database  *Database
	Events    *shared.EventHandlers
	Publisher Publisher
}

// InTransaction runs work in the context's transaction, or in a new one it commits when work succeeds.
func (u *UnitOfWork) InTransaction(ctx context.Context, name string, work func(ctx context.Context) error) error {
	if _, ok := transactionOf(ctx); ok {
		return work(ctx)
	}

	transaction, err := u.Database.Pool.Begin(ctx)
	if err != nil {
		return err
	}

	transactionContext := withTransaction(ctx, transaction)
	eventContext := shared.EventContextOf(ctx)

	err = work(transactionContext)
	if err == nil {
		err = u.Events.ExecuteRaised(transactionContext)
	}

	if err != nil {
		_ = transaction.Rollback(ctx)
		eventContext.Clear()

		return err
	}

	if err := transaction.Commit(ctx); err != nil {
		eventContext.Clear()

		return err
	}

	u.Publisher.Publish(ctx, eventContext.TakeDispatched())

	return nil
}
```

## Migrations

Plain SQL files, applied in name order by `go run ./cmd/console migrate`, each in its own transaction and recorded in `schema_migrations`. A schema change is a new file; an applied file is never edited.

Package `persistence`, imports `"context"`, `"embed"`, `"fmt"`, `"io/fs"`, `"sort"`, `"github.com/jackc/pgx/v5/pgxpool"`:

```go
//go:embed migrations/*.sql
var migrations embed.FS

// Migrate applies, in name order, every migration file not applied yet, each in its own transaction.
func Migrate(ctx context.Context, pool *pgxpool.Pool) ([]string, error) {
	if _, err := pool.Exec(ctx, "create table if not exists schema_migrations (name varchar(200) primary key, applied_at timestamptz not null default now())"); err != nil {
		return nil, err
	}

	names, err := fs.Glob(migrations, "migrations/*.sql")
	if err != nil {
		return nil, err
	}

	sort.Strings(names)
	applied := []string{}

	for _, name := range names {
		var exists bool
		if err := pool.QueryRow(ctx, "select exists(select 1 from schema_migrations where name = $1)", name).Scan(&exists); err != nil {
			return applied, err
		}

		if exists {
			continue
		}

		script, err := migrations.ReadFile(name)
		if err != nil {
			return applied, err
		}

		transaction, err := pool.Begin(ctx)
		if err != nil {
			return applied, err
		}

		if _, err := transaction.Exec(ctx, string(script)); err != nil {
			_ = transaction.Rollback(ctx)

			return applied, fmt.Errorf("%s: %w", name, err)
		}

		if _, err := transaction.Exec(ctx, "insert into schema_migrations (name) values ($1)", name); err != nil {
			_ = transaction.Rollback(ctx)

			return applied, err
		}

		if err := transaction.Commit(ctx); err != nil {
			return applied, err
		}

		applied = append(applied, name)
	}

	return applied, nil
}
```

With the example, the first migration:

```sql
create table categories (
    id integer generated by default as identity primary key,
    name varchar(100) not null,
    created_at timestamptz not null,
    created_user_id varchar(64),
    created_user_name varchar(256),
    updated_at timestamptz not null,
    updated_user_id varchar(64),
    updated_user_name varchar(256)
);
create unique index ix_categories_name on categories (name);

create table customers (
    id uuid primary key,
    name varchar(200) not null,
    email varchar(320) not null,
    is_blocked boolean not null,
    completed_order_count integer not null,
    created_at timestamptz not null,
    created_user_id varchar(64),
    created_user_name varchar(256),
    updated_at timestamptz not null,
    updated_user_id varchar(64),
    updated_user_name varchar(256)
);
create unique index ix_customers_email on customers (email);

create table products (
    id uuid primary key,
    sku varchar(64) not null,
    name varchar(200) not null,
    stock integer not null,
    category_id integer,
    created_at timestamptz not null,
    created_user_id varchar(64),
    created_user_name varchar(256),
    updated_at timestamptz not null,
    updated_user_id varchar(64),
    updated_user_name varchar(256),
    constraint ak_products_sku unique (sku),
    constraint ck_products_stock_not_negative check (stock >= 0),
    constraint fk_products_categories_category_id foreign key (category_id) references categories (id) on delete restrict
);
create index ix_products_category_id on products (category_id);

create table orders (
    id uuid primary key,
    order_number varchar(64) not null,
    customer_id uuid not null,
    status varchar(32) not null,
    created_at timestamptz not null,
    created_user_id varchar(64),
    created_user_name varchar(256),
    updated_at timestamptz not null,
    updated_user_id varchar(64),
    updated_user_name varchar(256),
    constraint fk_orders_customers_customer_id foreign key (customer_id) references customers (id) on delete restrict
);
create index ix_orders_customer_id on orders (customer_id);
create unique index ix_orders_order_number on orders (order_number);

create table order_lines (
    id uuid primary key,
    order_id uuid not null,
    sku varchar(64) not null,
    quantity integer not null,
    is_cancelled boolean not null,
    cancellation_reason varchar(500),
    price_amount numeric(18,4) not null,
    price_currency varchar(3) not null,
    constraint fk_order_lines_orders_order_id foreign key (order_id) references orders (id) on delete cascade,
    constraint fk_order_lines_product_sku foreign key (sku) references products (sku) on delete restrict
);
create index ix_order_lines_order_id on order_lines (order_id);
create index ix_order_lines_sku on order_lines (sku);
```

Without the example, write `0001_initial.sql` containing only `select 1;`: `go:embed` needs at least one file to match.

## Repositories

Package `products`, imports `"context"`, `"errors"`, `domain "{module}/internal/domain/products"`, `"{module}/internal/infrastructure/persistence"`, `"github.com/google/uuid"`, `"github.com/jackc/pgx/v5"`:

```go
const columns = "id, sku, name, stock, category_id, created_at, created_user_id, created_user_name, updated_at, updated_user_id, updated_user_name"

// Repository stores products in the products table.
type Repository struct {
	Database *persistence.Database
}

// Create inserts a product.
func (r Repository) Create(ctx context.Context, product *domain.Product) error {
	_, err := r.Database.On(ctx).Exec(ctx, "insert into products ("+columns+") values ($1, $2, $3, $4, $5, $6, $7, $8, $9, $10, $11)",
		product.ID, product.SKU, product.Name, product.Stock, product.CategoryID, product.CreatedAt, product.CreatedUserID, product.CreatedUserName, product.UpdatedAt, product.UpdatedUserID, product.UpdatedUserName)
	if err != nil {
		return err
	}

	persistence.RaiseEvents(ctx, product)

	return nil
}

// Update writes a product's changes.
func (r Repository) Update(ctx context.Context, product *domain.Product) error {
	_, err := r.Database.On(ctx).Exec(ctx, "update products set name = $2, stock = $3, category_id = $4, updated_at = $5, updated_user_id = $6, updated_user_name = $7 where id = $1",
		product.ID, product.Name, product.Stock, product.CategoryID, product.UpdatedAt, product.UpdatedUserID, product.UpdatedUserName)
	if err != nil {
		return err
	}

	persistence.RaiseEvents(ctx, product)

	return nil
}

// FindByID reads a product, or nil.
func (r Repository) FindByID(ctx context.Context, id uuid.UUID) (*domain.Product, error) {
	return r.one(ctx, "select "+columns+" from products where id = $1", id)
}

// FindBySKUForUpdate reads a product by SKU and locks its row, or nil.
func (r Repository) FindBySKUForUpdate(ctx context.Context, sku string) (*domain.Product, error) {
	return r.one(ctx, "select "+columns+" from products where sku = $1 for update", sku)
}

func (r Repository) one(ctx context.Context, sql string, argument any) (*domain.Product, error) {
	product := &domain.Product{}

	err := r.Database.On(ctx).QueryRow(ctx, sql, argument).Scan(&product.ID, &product.SKU, &product.Name, &product.Stock, &product.CategoryID,
		&product.CreatedAt, &product.CreatedUserID, &product.CreatedUserName, &product.UpdatedAt, &product.UpdatedUserID, &product.UpdatedUserName)
	if errors.Is(err, pgx.ErrNoRows) {
		return nil, nil
	}

	if err != nil {
		return nil, err
	}

	return product, nil
}
```

The order's repository writes its lines with an upsert, so a new line is inserted and a changed one updated; money goes through `numeric` as text.

Package `orders`, imports `"context"`, `"errors"`, `domain "{module}/internal/domain/orders"`, `"{module}/internal/infrastructure/persistence"`, `"github.com/google/uuid"`, `"github.com/jackc/pgx/v5"`, `"github.com/shopspring/decimal"`. In parts, in file order:

```go
const columns = "id, order_number, customer_id, status, created_at, created_user_id, created_user_name, updated_at, updated_user_id, updated_user_name"

// Repository stores orders in the orders table and their lines in order_lines.
type Repository struct {
	Database *persistence.Database
}

// Create inserts an order with its lines.
func (r Repository) Create(ctx context.Context, order *domain.Order) error {
	_, err := r.Database.On(ctx).Exec(ctx, "insert into orders ("+columns+") values ($1, $2, $3, $4, $5, $6, $7, $8, $9, $10)",
		order.ID, order.OrderNumber, order.CustomerID, string(order.Status), order.CreatedAt, order.CreatedUserID, order.CreatedUserName, order.UpdatedAt, order.UpdatedUserID, order.UpdatedUserName)
	if err != nil {
		return err
	}

	if err := r.writeLines(ctx, order); err != nil {
		return err
	}

	persistence.RaiseEvents(ctx, order)

	return nil
}

// Update writes an order's changes; a new line is inserted and a changed one updated in place.
func (r Repository) Update(ctx context.Context, order *domain.Order) error {
	_, err := r.Database.On(ctx).Exec(ctx, "update orders set status = $2, updated_at = $3, updated_user_id = $4, updated_user_name = $5 where id = $1",
		order.ID, string(order.Status), order.UpdatedAt, order.UpdatedUserID, order.UpdatedUserName)
	if err != nil {
		return err
	}

	if err := r.writeLines(ctx, order); err != nil {
		return err
	}

	persistence.RaiseEvents(ctx, order)

	return nil
}

// FindByID reads an order with its lines, or nil.
func (r Repository) FindByID(ctx context.Context, id uuid.UUID) (*domain.Order, error) {
	return r.one(ctx, "select "+columns+" from orders where id = $1", id)
}

// FindByIDForUpdate reads an order with its lines and locks the order's row, or nil.
func (r Repository) FindByIDForUpdate(ctx context.Context, id uuid.UUID) (*domain.Order, error) {
	return r.one(ctx, "select "+columns+" from orders where id = $1 for update", id)
}
```

```go
func (r Repository) writeLines(ctx context.Context, order *domain.Order) error {
	for _, line := range order.Lines {
		var reason *string
		if line.CancellationReason != "" {
			reason = &line.CancellationReason
		}

		_, err := r.Database.On(ctx).Exec(ctx, `insert into order_lines (id, order_id, sku, quantity, is_cancelled, cancellation_reason, price_amount, price_currency)
values ($1, $2, $3, $4, $5, $6, $7::numeric, $8)
on conflict (id) do update set quantity = excluded.quantity, is_cancelled = excluded.is_cancelled, cancellation_reason = excluded.cancellation_reason`,
			line.ID, line.OrderID, line.SKU, line.Quantity, line.IsCancelled, reason, line.Price.Amount.String(), line.Price.Currency)
		if err != nil {
			return err
		}
	}

	return nil
}
```

```go
func (r Repository) one(ctx context.Context, sql string, id uuid.UUID) (*domain.Order, error) {
	order := &domain.Order{}

	var status string

	err := r.Database.On(ctx).QueryRow(ctx, sql, id).Scan(&order.ID, &order.OrderNumber, &order.CustomerID, &status,
		&order.CreatedAt, &order.CreatedUserID, &order.CreatedUserName, &order.UpdatedAt, &order.UpdatedUserID, &order.UpdatedUserName)
	if errors.Is(err, pgx.ErrNoRows) {
		return nil, nil
	}

	if err != nil {
		return nil, err
	}

	order.Status = domain.Status(status)

	rows, err := r.Database.On(ctx).Query(ctx, "select id, order_id, sku, quantity, is_cancelled, coalesce(cancellation_reason, ''), price_amount::text, price_currency from order_lines where order_id = $1 order by id", id)
	if err != nil {
		return nil, err
	}
	defer rows.Close()

	for rows.Next() {
		line := &domain.Line{}

		var amount string

		if err := rows.Scan(&line.ID, &line.OrderID, &line.SKU, &line.Quantity, &line.IsCancelled, &line.CancellationReason, &amount, &line.Price.Currency); err != nil {
			return nil, err
		}

		line.Price.Amount, err = decimal.NewFromString(amount)
		if err != nil {
			return nil, err
		}

		order.Lines = append(order.Lines, line)
	}

	return order, rows.Err()
}
```

Package `customers`, imports `"context"`, `"errors"`, `domain "{module}/internal/domain/customers"`, `"{module}/internal/infrastructure/persistence"`, `"github.com/google/uuid"`, `"github.com/jackc/pgx/v5"`:

```go
const columns = "id, name, email, is_blocked, completed_order_count, created_at, created_user_id, created_user_name, updated_at, updated_user_id, updated_user_name"

// Repository stores customers in the customers table.
type Repository struct {
	Database *persistence.Database
}

// Create inserts a customer.
func (r Repository) Create(ctx context.Context, customer *domain.Customer) error {
	_, err := r.Database.On(ctx).Exec(ctx, "insert into customers ("+columns+") values ($1, $2, $3, $4, $5, $6, $7, $8, $9, $10, $11)",
		customer.ID, customer.Name, customer.Email, customer.IsBlocked, customer.CompletedOrderCount, customer.CreatedAt, customer.CreatedUserID, customer.CreatedUserName, customer.UpdatedAt, customer.UpdatedUserID, customer.UpdatedUserName)
	if err != nil {
		return err
	}

	persistence.RaiseEvents(ctx, customer)

	return nil
}

// Update writes a customer's changes.
func (r Repository) Update(ctx context.Context, customer *domain.Customer) error {
	_, err := r.Database.On(ctx).Exec(ctx, "update customers set name = $2, email = $3, is_blocked = $4, completed_order_count = $5, updated_at = $6, updated_user_id = $7, updated_user_name = $8 where id = $1",
		customer.ID, customer.Name, customer.Email, customer.IsBlocked, customer.CompletedOrderCount, customer.UpdatedAt, customer.UpdatedUserID, customer.UpdatedUserName)
	if err != nil {
		return err
	}

	persistence.RaiseEvents(ctx, customer)

	return nil
}

// FindByID reads a customer, or nil.
func (r Repository) FindByID(ctx context.Context, id uuid.UUID) (*domain.Customer, error) {
	return r.one(ctx, "select "+columns+" from customers where id = $1", id)
}

// FindByIDForUpdate reads a customer and locks its row, or nil.
func (r Repository) FindByIDForUpdate(ctx context.Context, id uuid.UUID) (*domain.Customer, error) {
	return r.one(ctx, "select "+columns+" from customers where id = $1 for update", id)
}

func (r Repository) one(ctx context.Context, sql string, id uuid.UUID) (*domain.Customer, error) {
	customer := &domain.Customer{}

	err := r.Database.On(ctx).QueryRow(ctx, sql, id).Scan(&customer.ID, &customer.Name, &customer.Email, &customer.IsBlocked, &customer.CompletedOrderCount,
		&customer.CreatedAt, &customer.CreatedUserID, &customer.CreatedUserName, &customer.UpdatedAt, &customer.UpdatedUserID, &customer.UpdatedUserName)
	if errors.Is(err, pgx.ErrNoRows) {
		return nil, nil
	}

	if err != nil {
		return nil, err
	}

	return customer, nil
}
```

Package `categories`, imports `"context"`, `"errors"`, `domain "{module}/internal/domain/categories"`, `"{module}/internal/infrastructure/persistence"`, `"github.com/jackc/pgx/v5"`:

```go
// Repository stores categories in the categories table; the database assigns the id.
type Repository struct {
	Database *persistence.Database
}

// Create inserts a category, takes the id the database assigned, then marks it created.
func (r Repository) Create(ctx context.Context, category *domain.Category) error {
	err := r.Database.On(ctx).QueryRow(ctx, "insert into categories (name, created_at, created_user_id, created_user_name, updated_at, updated_user_id, updated_user_name) values ($1, $2, $3, $4, $5, $6, $7) returning id",
		category.Name, category.CreatedAt, category.CreatedUserID, category.CreatedUserName, category.UpdatedAt, category.UpdatedUserID, category.UpdatedUserName).Scan(&category.ID)
	if err != nil {
		return err
	}

	if err := category.MarkCreated(); err != nil {
		return err
	}

	persistence.RaiseEvents(ctx, category)

	return nil
}

// FindByID reads a category, or nil.
func (r Repository) FindByID(ctx context.Context, id int) (*domain.Category, error) {
	category := &domain.Category{}

	err := r.Database.On(ctx).QueryRow(ctx, "select id, name, created_at, created_user_id, created_user_name, updated_at, updated_user_id, updated_user_name from categories where id = $1", id).
		Scan(&category.ID, &category.Name, &category.CreatedAt, &category.CreatedUserID, &category.CreatedUserName, &category.UpdatedAt, &category.UpdatedUserID, &category.UpdatedUserName)
	if errors.Is(err, pgx.ErrNoRows) {
		return nil, nil
	}

	if err != nil {
		return nil, err
	}

	return category, nil
}
```
