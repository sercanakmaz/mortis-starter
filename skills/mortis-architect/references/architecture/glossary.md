# Glossary

| Term | One line |
|---|---|
| Actor | who is doing it: a user, the console, or the system |
| Anti-corruption layer | a port in the domain's words for another system, translated in Infrastructure |
| After-command | delivery model: the outbox row is sent right after the command commits |
| After-commit action | work registered to run once the commit has succeeded |
| Aggregate | a cluster of entities changed only through its root |
| Aggregate root | the one entity of an aggregate that is loaded, saved and referenced |
| Async handler | an event handler that runs after the commit, in its own transaction |
| At-least-once | delivery guarantee: a message may arrive more than once, never zero times |
| Background | delivery model: a relay loop publishes outbox rows later |
| Before/after middleware | a step the pipeline runs around a command (logging, timing) |
| Broker | the message transport between processes |
| Child entity | a part that only exists inside one aggregate |
| Command | a request to change something, with the data the change needs |
| Command handler | the use case (application service): loads the aggregates one action needs, calls them or a domain service, saves, maps the view |
| Commit order | save, sync handlers, COMMIT, dispatch events, after-commit actions |
| Consequence | a change that happens because something else happened; done by an event handler |
| Context | the value on every command: actor, correlation, causation, tenant, language |
| Correlation | the id of the whole chain started by one outside request |
| Causation | the id of the message that directly caused this one |
| Dispatcher | the only way to run a command |
| Domain event | a past-tense fact raised by an aggregate |
| Domain service | business logic that fits no single aggregate; pure by default, may read through a domain interface, never writes |
| Read port | a narrow interface in the domain's words that answers one question from storage, e.g. the open order total of a customer |
| DTO / view | a flat serialisable description of a result |
| Entity | a thing with an identity |
| Event publisher | carries committed events to handlers or the broker |
| Event handler | receives one event, sends one command; one event may have many handlers |
| Host | the entry point that turns the outside world into a command |
| Failure | a stable code, status and message for what went wrong; the shape is the same at every level |
| Inbox | records handled message ids so a repeat is skipped |
| Message | an event or command as it travels between processes |
| Middleware | see before/after middleware |
| None | delivery model: events stay in-process |
| Outbox | storage of events written in the command's transaction, to be published |
| Pipeline | the fixed sequence every command passes: validate, begin, handle, commit |
| Relay | the loop that reads outbox rows and publishes them |
| Repository | declares how the domain asks for aggregates |
| Role | a part in the architecture, named without a language's spelling |
| Rule (business rule) | a stable-coded failure raised by an aggregate |
| Sweeper | releases outbox rows whose lock expired |
| Sync handler | an event handler that runs inside the command's transaction |
| System actor | the actor used when no user caused the work |
| Tenant | the customer organisation whose data a row belongs to |
| Transaction | all-or-nothing group of writes |
| Unit of work | owns the transaction and commit order of one scope |
| Validator | checks a command's shape before anything runs |
| Value object | a small group of values compared by its parts |
| Veto | a sync handler failing so the change is rolled back |
| View | see DTO |
| Wire name | the stable name of a message on the broker |
