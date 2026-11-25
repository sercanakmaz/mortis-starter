package api

import (
	"context"
	"encoding/json"
	"io"
	"log/slog"
	"net/http"
	"strconv"

	"delta/internal/application/categories"
	"delta/internal/application/customers"
	"delta/internal/application/orders"
	"delta/internal/application/products"
	"delta/internal/shared"

	"github.com/google/uuid"
)

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
