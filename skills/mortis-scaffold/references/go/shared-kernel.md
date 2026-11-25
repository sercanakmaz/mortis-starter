# The kernel: internal/shared

One package, `shared`, with no dependency on any other package of the project. `verify.sh` compares every exported declaration of these files with `kernel-shape.txt`; write them exactly.

## Ids, users, context

`ids.go`: the only place a new id is made. `go.sh build` fails on `uuid.New()` and the other random ids anywhere else.

Package `shared`, imports `"github.com/google/uuid"`:

```go
// NewID returns a new time-ordered identifier. It is the only way the project creates ids.
func NewID() uuid.UUID {
	return uuid.Must(uuid.NewV7())
}
```

`audit.go`:

Package `shared`, no imports:

```go
// AuditUser is the user a change is recorded against.
type AuditUser struct {
	ID    string
	Email string
}
```

`commandcontext.go`: the context a command carries. `FromEvent` is the context of a command sent in reaction to an event: same correlation and user, caused by the event.

Package `shared`, imports `"github.com/google/uuid"`:

```go
// CommandContext is what a command carries besides its own data: who asked, and which request it belongs to.
type CommandContext struct {
	CorrelationID uuid.UUID
	CausationID   uuid.UUID
	User          AuditUser
	TenantID      string
	Language      string
}

// Complete gives a context without a correlation id a new one.
func (c CommandContext) Complete() CommandContext {
	if c.CorrelationID == uuid.Nil {
		c.CorrelationID = NewID()
	}

	return c
}

// FromEvent is the context of a command sent in reaction to an event: the same request and user, caused by the event.
func FromEvent(event Event) CommandContext {
	base := event.Base()

	return CommandContext{
		CorrelationID: base.CorrelationID,
		CausationID:   base.EventID,
		User:          AuditUser{ID: base.UserID, Email: base.UserEmail},
		TenantID:      base.TenantID,
		Language:      base.Language,
	}
}

// Contextual is a command that carries a CommandContext.
type Contextual interface {
	CommandContext() *CommandContext
}

// WithContext is embedded in every command to carry its context.
type WithContext struct {
	Context CommandContext `json:"-"`
}

// CommandContext returns the embedded context so the pipeline can complete it.
func (w *WithContext) CommandContext() *CommandContext {
	return &w.Context
}
```

## Failures and validation

`errors.go`: every failure a caller can act on is an `*Error` with a stable code and a status. With `exceptions` = `minimal` nothing translates database errors: a duplicate key or a broken foreign key is a 500.

Package `shared`, imports `"errors"`, `"fmt"`, `"net/http"`:

```go
// Error is a failure the caller can act on: a stable code, an HTTP status and the values the message names.
type Error struct {
	Code    string
	Status  int
	Message string
	Params  map[string]any
	Fields  map[string][]string
	Cause   error
}

func (e *Error) Error() string {
	return e.Message
}

func (e *Error) Unwrap() error {
	return e.Cause
}

// AsError returns the Error inside err, or nil.
func AsError(err error) *Error {
	var target *Error
	if errors.As(err, &target) {
		return target
	}

	return nil
}

// NotFound is a missing record.
func NotFound(name string) *Error {
	return &Error{Code: "not_found", Status: http.StatusNotFound, Message: fmt.Sprintf("%s was not found.", name), Params: map[string]any{"name": name}}
}

// Duplicate is a record that already exists.
func Duplicate(name string, cause error) *Error {
	return &Error{Code: "duplicate", Status: http.StatusConflict, Message: fmt.Sprintf("%s already exists.", name), Params: map[string]any{"name": name}, Cause: cause}
}

// InvalidParameter is a value the caller sent that cannot be used.
func InvalidParameter(name string, reason string) *Error {
	return &Error{Code: "invalid_parameter", Status: http.StatusBadRequest, Message: fmt.Sprintf("%s is invalid: %s", name, reason), Params: map[string]any{"name": name, "reason": reason}}
}

// BusinessRule is a rule of the domain the request would break.
func BusinessRule(code string, message string) *Error {
	return &Error{Code: code, Status: http.StatusConflict, Message: message}
}

// ValidationFailed lists, per field, why a command is not valid.
func ValidationFailed(fields map[string][]string) *Error {
	return &Error{Code: "validation_failed", Status: http.StatusUnprocessableEntity, Message: "The request is not valid.", Fields: fields}
}

// ReferenceViolation is a reference to a missing record, or a record still referenced.
func ReferenceViolation(name string, cause error) *Error {
	return &Error{Code: "reference_violation", Status: http.StatusBadRequest, Message: fmt.Sprintf("The reference %s points at a missing record, or the record is still referenced.", name), Params: map[string]any{"name": name}, Cause: cause}
}
```

`validation.go`: a command checks its own shape in `Validate`; field names are the JSON names (PascalCase), nested ones `Lines[0].Sku`.

Package `shared`, imports `"strconv"`:

```go
// FieldErrors collects validation failures per field.
type FieldErrors map[string][]string

// Add records a failure of one field.
func (f FieldErrors) Add(field string, message string) {
	f[field] = append(f[field], message)
}

// Check records the failure when the condition does not hold.
func (f FieldErrors) Check(ok bool, field string, message string) {
	if !ok {
		f.Add(field, message)
	}
}

// Validatable is a command that checks its own shape before it runs.
type Validatable interface {
	Validate() FieldErrors
}

// NotEmpty records that a text field is required.
func (f FieldErrors) NotEmpty(field string, value string) {
	f.Check(value != "", field, "'"+field+"' must not be empty.")
}

// MaxLength records that a text field is too long.
func (f FieldErrors) MaxLength(field string, value string, maximum int) {
	f.Check(len([]rune(value)) <= maximum, field, "'"+field+"' must be "+strconv.Itoa(maximum)+" characters or fewer.")
}
```

## Events and aggregates

`events.go`:

Package `shared`, imports `"time"`, `"github.com/google/uuid"`:

```go
// EventBase is what every event carries besides its own data.
type EventBase struct {
	EventID       uuid.UUID
	OccurredAt    time.Time
	CorrelationID uuid.UUID
	CausationID   uuid.UUID
	TenantID      string
	Language      string
	UserID        string
	UserEmail     string
}

// Base returns the event's base so the kernel can stamp it.
func (b *EventBase) Base() *EventBase {
	return b
}

// NewEventBase starts the base of a new event.
func NewEventBase() EventBase {
	return EventBase{EventID: NewID(), OccurredAt: time.Now().UTC()}
}

// Event is a fact an aggregate raised.
type Event interface {
	Base() *EventBase
	EventName() string
}
```

`aggregate.go`: embedded in every aggregate. Creation sets both the created and the updated audit (the columns are not null).

Package `shared`, imports `"time"`:

```go
// Audit records who created and last changed an aggregate.
type Audit struct {
	CreatedAt       time.Time
	CreatedUserID   string
	CreatedUserName string
	UpdatedAt       time.Time
	UpdatedUserID   string
	UpdatedUserName string
}

// AggregateRoot is embedded in every aggregate: it collects the events the aggregate raises and its audit.
type AggregateRoot struct {
	Audit
	events []Event
}

// Raise records an event the aggregate raised.
func (a *AggregateRoot) Raise(event Event) {
	a.events = append(a.events, event)
}

// Events returns the events raised and not yet taken.
func (a *AggregateRoot) Events() []Event {
	return a.events
}

// ClearEvents forgets the raised events once they are taken.
func (a *AggregateRoot) ClearEvents() {
	a.events = nil
}

// SetCreatedAudit records who created the aggregate.
func (a *AggregateRoot) SetCreatedAudit(user AuditUser) {
	now := time.Now().UTC()
	a.CreatedAt = now
	a.CreatedUserID = user.ID
	a.CreatedUserName = user.Email
	a.UpdatedAt = now
	a.UpdatedUserID = user.ID
	a.UpdatedUserName = user.Email
}

// SetUpdatedAudit records who changed the aggregate last.
func (a *AggregateRoot) SetUpdatedAudit(user AuditUser) {
	a.UpdatedAt = time.Now().UTC()
	a.UpdatedUserID = user.ID
	a.UpdatedUserName = user.Email
}

// Aggregate is what a repository needs from any aggregate.
type Aggregate interface {
	Events() []Event
	ClearEvents()
}
```

`eventcontext.go`: for one top-level command and everything it causes: the stack of command contexts and the queues of raised and dispatched events. It travels in the `context.Context`.

Package `shared`, imports `"context"`. In parts, in file order:

```go
type eventContextKey struct{}

// EventContext holds, for one top-level command and everything it causes, the commands running and the events raised.
type EventContext struct {
	commands   []CommandContext
	raised     []Event
	dispatched []Event
}

// WithEventContext returns ctx with a new EventContext, unless it has one.
func WithEventContext(ctx context.Context) context.Context {
	if EventContextOf(ctx) != nil {
		return ctx
	}

	return context.WithValue(ctx, eventContextKey{}, &EventContext{})
}

// EventContextOf returns the EventContext of ctx, or nil.
func EventContextOf(ctx context.Context) *EventContext {
	eventContext, _ := ctx.Value(eventContextKey{}).(*EventContext)

	return eventContext
}

// EnterCommand makes a command's context the one events are stamped with.
func (e *EventContext) EnterCommand(commandContext CommandContext) {
	e.commands = append(e.commands, commandContext)
}

// LeaveCommand restores the context of the outer command.
func (e *EventContext) LeaveCommand() {
	e.commands = e.commands[:len(e.commands)-1]
}

// AddRaised queues an event, stamped with the context of the command that raised it.
func (e *EventContext) AddRaised(event Event) {
	if len(e.commands) > 0 {
		current := e.commands[len(e.commands)-1]
		base := event.Base()
		base.CorrelationID = current.CorrelationID
		base.CausationID = current.CausationID
		base.TenantID = current.TenantID
		base.Language = current.Language
		base.UserID = current.User.ID
		base.UserEmail = current.User.Email
	}

	e.raised = append(e.raised, event)
}
```

```go
// TakeRaised removes and returns the oldest raised event, or nil.
func (e *EventContext) TakeRaised() Event {
	if len(e.raised) == 0 {
		return nil
	}

	event := e.raised[0]
	e.raised = e.raised[1:]

	return event
}

// AddDispatched records an event whose sync handlers have run.
func (e *EventContext) AddDispatched(event Event) {
	e.dispatched = append(e.dispatched, event)
}

// TakeDispatched removes and returns every event whose sync handlers have run.
func (e *EventContext) TakeDispatched() []Event {
	dispatched := e.dispatched
	e.dispatched = nil

	return dispatched
}

// Clear forgets every raised and dispatched event, after a rollback.
func (e *EventContext) Clear() {
	e.raised = nil
	e.dispatched = nil
}
```

`handlers.go`: sync handlers run in the command's transaction, in sequence order, until no raised event is left; consumers run after the commit (`events-inprocess.md`).

Package `shared`, imports `"context"`, `"sort"`:

```go
// SyncHandler reacts to an event inside the transaction of the command that raised it; its failure rolls the command back.
type SyncHandler interface {
	Handle(ctx context.Context, event Event) error
}

// Consumer reacts to an event after the command that raised it committed; its failure is logged and never undoes the command.
type Consumer interface {
	Handle(ctx context.Context, event Event) error
}

type syncRegistration struct {
	sequence int
	handler  SyncHandler
}

// EventHandlers knows, per event name, the sync handlers and the consumers.
type EventHandlers struct {
	sync      map[string][]syncRegistration
	consumers map[string][]Consumer
}

// NewEventHandlers returns an empty set of handlers.
func NewEventHandlers() *EventHandlers {
	return &EventHandlers{sync: map[string][]syncRegistration{}, consumers: map[string][]Consumer{}}
}

// AddSync registers a sync handler of an event; handlers of one event run in ascending sequence.
func (h *EventHandlers) AddSync(eventName string, sequence int, handler SyncHandler) {
	h.sync[eventName] = append(h.sync[eventName], syncRegistration{sequence: sequence, handler: handler})
	sort.SliceStable(h.sync[eventName], func(i, j int) bool { return h.sync[eventName][i].sequence < h.sync[eventName][j].sequence })
}

// AddConsumer registers a consumer of an event.
func (h *EventHandlers) AddConsumer(eventName string, consumer Consumer) {
	h.consumers[eventName] = append(h.consumers[eventName], consumer)
}

// ConsumersOf returns the consumers of an event.
func (h *EventHandlers) ConsumersOf(eventName string) []Consumer {
	return h.consumers[eventName]
}

// ExecuteRaised runs the sync handlers of every raised event, including events their commands raise, in raised order.
func (h *EventHandlers) ExecuteRaised(ctx context.Context) error {
	eventContext := EventContextOf(ctx)

	for event := eventContext.TakeRaised(); event != nil; event = eventContext.TakeRaised() {
		for _, registration := range h.sync[event.EventName()] {
			if err := registration.handler.Handle(ctx, event); err != nil {
				return err
			}
		}

		eventContext.AddDispatched(event)
	}

	return nil
}
```

## The pipeline

`unitofwork.go`:

Package `shared`, imports `"context"`:

```go
// UnitOfWork runs work in one transaction; a call inside another joins it.
type UnitOfWork interface {
	InTransaction(ctx context.Context, name string, work func(ctx context.Context) error) error
}

// Transactional marks a command that runs in a transaction.
type Transactional interface {
	Transactional()
}

// InTransaction is embedded in a command that runs in a transaction.
type InTransaction struct{}

// Transactional marks the command.
func (InTransaction) Transactional() {}
```

`dispatcher.go`: one handler per command type, registered with `Register`; `Dispatch` completes the context, validates, runs the middlewares (logging), then the handler, inside `InTransaction` when the command embeds `shared.InTransaction`. Validation runs before the middlewares, so an invalid command logs nothing.

Package `shared`, imports `"context"`, `"fmt"`, `"reflect"`, `"strings"`. In parts, in file order:

```go
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
```

```go
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
```

```go
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
```
