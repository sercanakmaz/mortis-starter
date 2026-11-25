# The hosts: bootstrap, API and Console

`internal/bootstrap` wires every layer once, for every host: no container, plain constructors.

Package `bootstrap`, imports `"context"`, `"log/slog"`, `"os"`, `"{module}/internal/application"`, `"{module}/internal/infrastructure/logging"`, `"{module}/internal/infrastructure/messaging"`, `"{module}/internal/infrastructure/persistence"`, `"{module}/internal/infrastructure/persistence/categories"`, `"{module}/internal/infrastructure/persistence/customers"`, `"{module}/internal/infrastructure/persistence/orders"`, `"{module}/internal/infrastructure/persistence/products"`, `"{module}/internal/shared"`, `"github.com/shopspring/decimal"`:

```go
// ConnectionStringVariable names the environment variable that holds the PostgreSQL connection string.
const ConnectionStringVariable = "ConnectionStrings__Default"

// DefaultConnectionString is used when the variable is not set: the local development database.
const DefaultConnectionString = "postgres://postgres:postgres@localhost:5432/{module}"

// App is everything a host needs: the dispatcher, the database and the logger.
type App struct {
	Dispatcher *shared.Dispatcher
	Database   *persistence.Database
	Logger     *slog.Logger
}

// New connects to the database and wires every layer.
func New(ctx context.Context) (*App, error) {
	decimal.MarshalJSONWithoutQuotes = true

	connectionString := os.Getenv(ConnectionStringVariable)
	if connectionString == "" {
		connectionString = DefaultConnectionString
	}

	database, err := persistence.Open(ctx, connectionString)
	if err != nil {
		return nil, err
	}

	logger := slog.New(logging.NewJSONLineHandler(os.Stderr))
	events := shared.NewEventHandlers()
	unitOfWork := &persistence.UnitOfWork{Database: database, Events: events, Publisher: &messaging.EventPublisher{Events: events, Logger: logger}}
	dispatcher := shared.NewDispatcher(unitOfWork, logging.CommandMiddleware(logger))

	application.Register(dispatcher, events, application.Repositories{
		Products:   products.Repository{Database: database},
		Orders:     orders.Repository{Database: database},
		Customers:  customers.Repository{Database: database},
		Categories: categories.Repository{Database: database},
	})

	return &App{Dispatcher: dispatcher, Database: database, Logger: logger}, nil
}
```

## API

`internal/api/routes.go`: the standard library router. Each route builds its command, gives it the request's context and dispatches it. A malformed id in the path is a 404.

A block that ends inside a function continues in the next one.

Package `api`, imports `"context"`, `"encoding/json"`, `"io"`, `"log/slog"`, `"net/http"`, `"strconv"`, `"{module}/internal/application/categories"`, `"{module}/internal/application/customers"`, `"{module}/internal/application/orders"`, `"{module}/internal/application/products"`, `"{module}/internal/shared"`, `"github.com/google/uuid"`. In parts, in file order:

```go
// Routes maps every endpoint of the API to its command.
func Routes(dispatcher *shared.Dispatcher, logger *slog.Logger) http.Handler {
	mux := http.NewServeMux()
	send := sender{dispatcher: dispatcher, logger: logger}

	mux.HandleFunc("GET /health", func(writer http.ResponseWriter, _ *http.Request) { writer.WriteHeader(http.StatusOK) })

	mux.HandleFunc("POST /categories", send.created("categories", func(request *http.Request) (any, error) { return bind[categories.CreateCategoryCommand](request) }))
	mux.HandleFunc("GET /categories/{id}", send.ok(func(request *http.Request) (any, error) {
		id, err := strconv.Atoi(request.PathValue("id"))
		if err != nil {
			return nil, shared.NotFound("Category")
		}

		return &categories.GetCategoryCommand{ID: id}, nil
	}))

	mux.HandleFunc("POST /products", send.created("products", func(request *http.Request) (any, error) { return bind[products.CreateProductCommand](request) }))
	mux.HandleFunc("GET /products/{id}", send.ok(func(request *http.Request) (any, error) {
		id, err := routeID(request, "id", "Product")

		return &products.GetProductCommand{ID: id}, err
	}))

	mux.HandleFunc("POST /customers", send.created("customers", func(request *http.Request) (any, error) { return bind[customers.CreateCustomerCommand](request) }))
	mux.HandleFunc("GET /customers/{id}", send.ok(func(request *http.Request) (any, error) {
		id, err := routeID(request, "id", "Customer")

		return &customers.GetCustomerCommand{ID: id}, err
	}))
	mux.HandleFunc("PUT /customers/{id}/block", send.ok(func(request *http.Request) (any, error) {
		id, err := routeID(request, "id", "Customer")
```

```go
		return &customers.BlockCustomerCommand{ID: id}, err
	}))

	mux.HandleFunc("POST /orders", send.created("orders", func(request *http.Request) (any, error) { return bind[orders.CreateOrderCommand](request) }))
	mux.HandleFunc("GET /orders/{id}", send.ok(func(request *http.Request) (any, error) {
		id, err := routeID(request, "id", "Order")

		return &orders.GetOrderCommand{ID: id}, err
	}))
	mux.HandleFunc("POST /orders/{id}/lines", send.ok(func(request *http.Request) (any, error) {
		command, err := bind[orders.AddOrderLineCommand](request)
		if err != nil {
			return nil, err
		}

		command.ID, err = routeID(request, "id", "Order")

		return command, err
	}))
	mux.HandleFunc("PUT /orders/{id}/lines/{lineId}/cancel", send.ok(func(request *http.Request) (any, error) {
		command, err := bind[orders.CancelOrderLineCommand](request)
		if err != nil {
			return nil, err
		}

		if command.ID, err = routeID(request, "id", "Order"); err != nil {
			return nil, err
		}

		command.LineID, err = routeID(request, "lineId", "OrderLine")

		return command, err
	}))
	mux.HandleFunc("PUT /orders/{id}/complete", send.ok(func(request *http.Request) (any, error) {
		id, err := routeID(request, "id", "Order")

		return &orders.CompleteOrderCommand{ID: id}, err
	}))

	return mux
}

type sender struct {
	dispatcher *shared.Dispatcher
	logger     *slog.Logger
}

func (s sender) ok(build func(request *http.Request) (any, error)) http.HandlerFunc {
	return s.respond(build, func(writer http.ResponseWriter, result any) { WriteJSON(writer, http.StatusOK, result) })
}
```

```go
func (s sender) created(resource string, build func(request *http.Request) (any, error)) http.HandlerFunc {
	return s.respond(build, func(writer http.ResponseWriter, result any) {
		var identified struct {
			ID any `json:"Id"`
		}

		encoded, _ := json.Marshal(result)
		_ = json.Unmarshal(encoded, &identified)

		writer.Header().Set("Location", resource+"/"+jsonText(identified.ID))
		WriteJSON(writer, http.StatusCreated, result)
	})
}

func (s sender) respond(build func(request *http.Request) (any, error), write func(writer http.ResponseWriter, result any)) http.HandlerFunc {
	return func(writer http.ResponseWriter, request *http.Request) {
		command, err := build(request)
		if err != nil {
			WriteError(writer, s.logger, err)

			return
		}

		if contextual, ok := command.(shared.Contextual); ok {
			*contextual.CommandContext() = ContextOf(request)
		}

		result, err := s.dispatcher.Dispatch(context.WithoutCancel(request.Context()), command)
		if err != nil {
			WriteError(writer, s.logger, err)

			return
		}

		write(writer, result)
	}
}

func bind[C any](request *http.Request) (*C, error) {
	command := new(C)

	body, err := io.ReadAll(request.Body)
	if err != nil {
		return nil, err
	}

	if len(body) == 0 {
		return command, nil
	}

	if err := json.Unmarshal(body, command); err != nil {
		return nil, shared.InvalidParameter("body", err.Error())
	}

	return command, nil
}
```

```go
func routeID(request *http.Request, name string, resource string) (uuid.UUID, error) {
	id, err := uuid.Parse(request.PathValue(name))
	if err != nil {
		return uuid.Nil, shared.NotFound(resource)
	}

	return id, nil
}

func jsonText(value any) string {
	if text, ok := value.(string); ok {
		return text
	}

	encoded, _ := json.Marshal(value)

	return string(encoded)
}
```

`internal/api/respond.go`: results go out with camelCase members; failures as `{code, message, errors}`, with the field names of `errors` kept; an unexpected failure is a 500 with a reference that its log line repeats.

Package `api`, imports `"crypto/sha256"`, `"encoding/hex"`, `"encoding/json"`, `"log/slog"`, `"net/http"`, `"unicode"`, `"unicode/utf8"`, `"{module}/internal/shared"`. In parts, in file order:

```go
// The codes and texts of a failure the API did not expect.
const (
	UnexpectedErrorCode    = "unexpected_error"
	UnexpectedErrorMessage = "An unexpected error occurred."
)

// ErrorResponse is the one body of every failure.
type ErrorResponse struct {
	Code    string              `json:"code"`
	Message string              `json:"message"`
	Errors  map[string][]string `json:"errors"`
}

// WriteJSON writes a result with camelCase member names.
func WriteJSON(writer http.ResponseWriter, status int, value any) {
	encoded, err := json.Marshal(value)
	if err != nil {
		WriteError(writer, nil, err)

		return
	}

	var decoded any
	_ = json.Unmarshal(encoded, &decoded)

	writer.Header().Set("Content-Type", "application/json")
	writer.WriteHeader(status)
	_ = json.NewEncoder(writer).Encode(camelCase(decoded))
}

// WriteError writes a failure: its own status and code, or 500 with a reference for an unexpected one.
func WriteError(writer http.ResponseWriter, logger *slog.Logger, err error) {
	response := ErrorResponse{}
	status := http.StatusInternalServerError

	if failure := shared.AsError(err); failure != nil {
		status = failure.Status
		response = ErrorResponse{Code: failure.Code, Message: failure.Message, Errors: failure.Fields}
	} else {
		reference := Reference(err)
		response = ErrorResponse{Code: UnexpectedErrorCode, Message: UnexpectedErrorMessage + " Reference " + reference + "."}

		if logger != nil {
			logger.Error("Request failed with 500 "+UnexpectedErrorCode+" reference "+reference, "Exception", err.Error())
		}
	}

	writer.Header().Set("Content-Type", "application/json")
	writer.WriteHeader(status)
	_ = json.NewEncoder(writer).Encode(response)
}

// Reference is a short, stable name of a failure's kind, to find its log line.
func Reference(err error) string {
	hash := sha256.Sum256([]byte(err.Error()))

	return hex.EncodeToString(hash[:])[:8]
}
```

```go
func camelCase(value any) any {
	switch typed := value.(type) {
	case map[string]any:
		converted := make(map[string]any, len(typed))
		for key, member := range typed {
			converted[lowerFirst(key)] = camelCase(member)
		}

		return converted
	case []any:
		for index, member := range typed {
			typed[index] = camelCase(member)
		}

		return typed
	default:
		return value
	}
}

func lowerFirst(key string) string {
	first, size := utf8.DecodeRuneInString(key)

	return string(unicode.ToLower(first)) + key[size:]
}
```

Without authentication every request runs as an anonymous caller:

Package `api`, imports `"net/http"`, `"{module}/internal/shared"`:

```go
// ContextOf is the context of a request's command. Without authentication the caller is anonymous.
func ContextOf(_ *http.Request) shared.CommandContext {
	return shared.CommandContext{}
}
```

Package `main`, imports `"context"`, `"fmt"`, `"net/http"`, `"os"`, `"{module}/internal/api"`, `"{module}/internal/bootstrap"`:

```go
// AddressVariable names the environment variable that holds the address to listen on.
const AddressVariable = "MORTIS_API_ADDRESS"

func main() {
	app, err := bootstrap.New(context.Background())
	if err != nil {
		fmt.Fprintln(os.Stderr, err)
		os.Exit(1)
	}
	defer app.Database.Pool.Close()

	address := os.Getenv(AddressVariable)
	if address == "" {
		address = "localhost:5000"
	}

	app.Logger.Info("Listening on " + address)

	if err := http.ListenAndServe(address, api.Routes(app.Dispatcher, app.Logger)); err != nil {
		fmt.Fprintln(os.Stderr, err)
		os.Exit(1)
	}
}
```

## Console

`go run ./cmd/console -- --user <id> [--email <email>] <Command> [json | @file.json]` runs any registered command; `migrate` applies the migrations. Exit codes: 0 done, 1 usage or an unexpected failure, 2 a failure the caller can act on (printed on stderr as one JSON line with `code`).

Package `main`, imports `"context"`, `"os"`, `"{module}/internal/bootstrap"`:

```go
func main() {
	os.Exit(run(context.Background(), os.Args[1:], os.Stdout, os.Stderr, bootstrap.New))
}
```

Package `main`, imports `"{module}/internal/shared"`:

```go
// MigrateCommand applies the migrations instead of running a command.
const MigrateCommand = "migrate"

// Arguments are a Console run's options, the command's name and its JSON.
type Arguments struct {
	UserID   string
	Email    string
	TenantID string
	Name     string
	JSON     string
}

// Parse reads --user, --email and --tenant before the command's name, then the name and its JSON.
func Parse(args []string) (Arguments, error) {
	arguments := Arguments{}
	positional := []string{}

	for index := 0; index < len(args); index++ {
		switch args[index] {
		case "--":
			continue
		case "--user", "--email", "--tenant":
			if index+1 >= len(args) {
				return arguments, shared.InvalidParameter(args[index], "a value must follow it")
			}

			value := args[index+1]
			index++

			switch args[index-1] {
			case "--user":
				arguments.UserID = value
			case "--email":
				arguments.Email = value
			default:
				arguments.TenantID = value
			}
		default:
			positional = append(positional, args[index])
		}
	}

	if len(positional) > 0 {
		arguments.Name = positional[0]
	}

	if len(positional) > 1 {
		arguments.JSON = positional[1]
	}

	return arguments, nil
}
```

A block that ends inside a function continues in the next one.

Package `main`, imports `"context"`, `"encoding/json"`, `"fmt"`, `"io"`, `"os"`, `"strings"`, `"{module}/internal/bootstrap"`, `"{module}/internal/infrastructure/persistence"`, `"{module}/internal/shared"`. In parts, in file order:

```go
const usage = "Usage: go run ./cmd/console -- --user <id> [--email <email>] <Command> [json | @file.json]   |   migrate"

func run(ctx context.Context, args []string, stdout io.Writer, stderr io.Writer, newApp func(context.Context) (*bootstrap.App, error)) int {
	arguments, err := Parse(args)
	if err != nil {
		writeError(stderr, err)

		return 2
	}

	if arguments.Name == "" {
		fmt.Fprintln(stderr, usage)

		return 1
	}

	app, err := newApp(ctx)
	if err != nil {
		fmt.Fprintln(stderr, err)

		return 1
	}
	defer app.Database.Pool.Close()

	if strings.EqualFold(arguments.Name, MigrateCommand) {
		applied, err := persistence.Migrate(ctx, app.Database.Pool)
		if err != nil {
			fmt.Fprintln(stderr, err)

			return 1
		}

		fmt.Fprintf(stdout, "Applied %d migration(s).\nDone.\n", len(applied))
```

```go
		return 0
	}

	command, ok := app.Dispatcher.New(arguments.Name)
	if !ok {
		writeError(stderr, shared.InvalidParameter("command", fmt.Sprintf("no command is named %s", arguments.Name)))

		return 2
	}

	if arguments.UserID == "" {
		writeError(stderr, shared.InvalidParameter("--user", "every run names its user"))

		return 2
	}

	if err := decode(arguments.JSON, command); err != nil {
		writeError(stderr, shared.InvalidParameter("json", err.Error()))

		return 2
	}

	if contextual, ok := command.(shared.Contextual); ok {
		*contextual.CommandContext() = shared.CommandContext{User: shared.AuditUser{ID: arguments.UserID, Email: arguments.Email}, TenantID: arguments.TenantID}
	}

	result, err := app.Dispatcher.Dispatch(ctx, command)
	if err != nil {
		writeError(stderr, err)

		if shared.AsError(err) != nil {
			return 2
		}

		return 1
	}

	output, _ := json.Marshal(result)
	fmt.Fprintln(stdout, string(output))

	return 0
}
```

```go
func decode(text string, command any) error {
	if strings.HasPrefix(text, "@") {
		content, err := os.ReadFile(text[1:])
		if err != nil {
			return err
		}

		text = string(content)
	}

	if strings.TrimSpace(text) == "" {
		text = "{}"
	}

	return json.Unmarshal([]byte(text), command)
}

func writeError(stderr io.Writer, err error) {
	failure := shared.AsError(err)
	if failure == nil {
		failure = &shared.Error{Code: "unexpected_error", Message: err.Error()}
	}

	line, _ := json.Marshal(map[string]any{"code": failure.Code, "message": failure.Message, "errors": failure.Fields})
	fmt.Fprintln(stderr, string(line))
}
```

## Editor

`.vscode/launch.json` runs a command from the editor; every configuration passes `--user`.

```json
{
  "version": "0.2.0",
  "inputs": [
    { "id": "name", "type": "promptString", "description": "Command name, for example CreateProduct" },
    { "id": "json", "type": "promptString", "description": "The command's JSON, or @file.json", "default": "{}" },
    { "id": "user", "type": "promptString", "description": "The user the command runs as", "default": "developer" }
  ],
  "configurations": [
    {
      "name": "Console: run a command",
      "type": "go",
      "request": "launch",
      "mode": "auto",
      "program": "${workspaceFolder}/cmd/console",
      "args": ["--user", "${input:user}", "${input:name}", "${input:json}"],
      "env": { "ConnectionStrings__Default": "Host=localhost;Port=5432;Database={module};Username=postgres;Password=postgres" }
    },
    {
      "name": "Console: apply the migrations",
      "type": "go",
      "request": "launch",
      "mode": "auto",
      "program": "${workspaceFolder}/cmd/console",
      "args": ["--user", "developer", "migrate"],
      "env": { "ConnectionStrings__Default": "Host=localhost;Port=5432;Database={module};Username=postgres;Password=postgres" }
    }
  ]
}
```
