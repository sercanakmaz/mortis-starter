package shared_test

import (
	"context"
	"errors"
	"testing"

	"boilerplate/internal/shared"
	"boilerplate/internal/testsupport"
)

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
