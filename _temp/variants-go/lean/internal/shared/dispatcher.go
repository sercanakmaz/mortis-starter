package shared

import (
	"context"
	"fmt"
	"reflect"
	"strings"
)

// Handler runs one kind of command and returns its result.
type Handler[C any, R any] interface {
	Handle(ctx context.Context, command *C) (R, error)
}

// Middleware wraps every command: next runs the rest of the pipeline.
type Middleware func(ctx context.Context, command any, next func(ctx context.Context) (any, error)) (any, error)

type registration struct {
	name       string
	newCommand func() any
	run        func(ctx context.Context, command any) (any, error)
}

// Dispatcher sends a command to its one registered handler through the pipeline.
type Dispatcher struct {
	unitOfWork  UnitOfWork
	middlewares []Middleware
	handlers    map[reflect.Type]registration
	byName      map[string]reflect.Type
}

// NewDispatcher returns a dispatcher with no handler.
func NewDispatcher(unitOfWork UnitOfWork, middlewares ...Middleware) *Dispatcher {
	return &Dispatcher{unitOfWork: unitOfWork, middlewares: middlewares, handlers: map[reflect.Type]registration{}, byName: map[string]reflect.Type{}}
}

// Register makes handler the one that runs commands of type C.
func Register[C any, R any](dispatcher *Dispatcher, handler Handler[C, R]) {
	commandType := reflect.TypeFor[C]()
	dispatcher.handlers[commandType] = registration{
		name:       commandType.Name(),
		newCommand: func() any { return new(C) },
		run: func(ctx context.Context, command any) (any, error) {
			return handler.Handle(ctx, command.(*C))
		},
	}
	dispatcher.byName[strings.ToLower(commandType.Name())] = commandType
}

// Send runs a command and returns its result typed.
func Send[R any](ctx context.Context, dispatcher *Dispatcher, command any) (R, error) {
	var zero R

	result, err := dispatcher.Dispatch(ctx, command)
	if err != nil {
		return zero, err
	}

	return result.(R), nil
}

// New returns an empty command of the registered name, matched ignoring case and with or without the Command suffix.
func (d *Dispatcher) New(name string) (any, bool) {
	commandType, ok := d.byName[strings.ToLower(name)]
	if !ok {
		commandType, ok = d.byName[strings.ToLower(name)+"command"]
	}

	if !ok {
		return nil, false
	}

	return d.handlers[commandType].newCommand(), true
}

// Names returns the registered command names.
func (d *Dispatcher) Names() []string {
	names := make([]string, 0, len(d.handlers))
	for _, registered := range d.handlers {
		names = append(names, registered.name)
	}

	return names
}

// Dispatch runs a command (a pointer to it) through the pipeline: context, validation, middlewares, transaction, handler.
func (d *Dispatcher) Dispatch(ctx context.Context, command any) (any, error) {
	registered, ok := d.handlers[reflect.TypeOf(command).Elem()]
	if !ok {
		return nil, fmt.Errorf("no handler is registered for %T", command)
	}

	ctx = WithEventContext(ctx)

	if contextual, ok := command.(Contextual); ok {
		completed := contextual.CommandContext().Complete()
		*contextual.CommandContext() = completed

		eventContext := EventContextOf(ctx)
		eventContext.EnterCommand(completed)
		defer eventContext.LeaveCommand()
	}

	if validatable, ok := command.(Validatable); ok {
		if fieldErrors := validatable.Validate(); len(fieldErrors) > 0 {
			return nil, ValidationFailed(fieldErrors)
		}
	}

	next := func(ctx context.Context) (any, error) {
		if _, ok := command.(Transactional); !ok {
			return registered.run(ctx, command)
		}

		var result any

		err := d.unitOfWork.InTransaction(ctx, registered.name, func(ctx context.Context) error {
			var err error
			result, err = registered.run(ctx, command)

			return err
		})

		return result, err
	}

	for index := len(d.middlewares) - 1; index >= 0; index-- {
		middleware, inner := d.middlewares[index], next
		next = func(ctx context.Context) (any, error) { return middleware(ctx, command, inner) }
	}

	return next(ctx)
}
