# Placement guide

Where code belongs: the picture, the questions to ask in order, the usual mistakes, and a lookup table of fifty situations.

## How they work together

```
Host ──► Command ──► Pipeline ──► Command handler (use case) ─────────► Aggregate.operation()   raises Event A, Event B, ...
 (fills context)     validates        loads what the action needs,     │
                     begins tx        saves what it changed            └─► Domain service ──► several entities, anti-corruption layer
                                          │
                                       COMMIT
                                          ├─ before it: sync handlers ──► send Command ──► another aggregate (may veto)
                                          └─ after it:  async handlers ──► send Command ──► another aggregate, email, stock
                                                        event dispatchers ──► outbox ──► relay ──► broker
```

One command runs one handler. The handler changes one aggregate, or several when the action is about all of them. Each change can raise several events, and each event can have several handlers. Each event handler sends one command, so one request can fan out into many commands, all with the same correlation id.

## Decision cheat sheet

Ask these questions in order and stop at the first yes.

1. **Can it be checked from the command alone** (required, format, range)? → validator.
2. **Is it a business rule?**
   - It uses only the state of one aggregate → an operation on that aggregate.
   - It needs several entities, or an answer from another system → a domain service (the other system behind an anti-corruption layer). The handler passes in what it already loads; the service reads for itself only what the rule decides it needs, through a narrow read port; it never writes.
3. **Is it the outside world** (who is calling, the tenant, the language, the response format, authentication)? → host.
4. **Is it running one user action** (load, call the domain, save, map the result)? → command handler; the pipeline gives the transaction.
5. **Does the action change a second aggregate?** Decide between the command handler and an event handler with the test below.
6. **Should it happen because something else happened, without the originator knowing?** → event handler. It sends one command; it holds no rule of its own.
7. **Must it succeed or fail together with the change?** → sync handler (inside the transaction, can veto). **May it lag or fail alone?** → async handler. **Must it leave the process?** → the event dispatcher with the outbox model chosen for the project.
8. **Does it touch storage, a broker, the clock, ids or a third party?** → infrastructure, behind something declared in Shared or Domain.
9. **Is it a read for a screen or report?** → a query, not an aggregate.

If two answers seem possible, the rule is in the wrong shape: a decision belongs to an aggregate or a domain service, a coordination step to the command handler.

### A second aggregate: in the command handler or in an event handler?

The default is an **event handler**. Change the second aggregate in the **command handler** only when every line below is true.

**The one question:** *If the change to the second aggregate were taken out, would the command still do what its name says?*
- **No** → the second change is part of the action. Do it in the command handler. `TransferStock` without the receiving warehouse is not a transfer.
- **Yes** → the second change is a consequence. Use an event handler. `CompleteOrder` without counting it on the customer is still a completed order.

Do it in the command handler when all of these hold:

| Check | Why it matters |
|---|---|
| The command's name describes the change to both aggregates | the user asked for both; neither is a side effect |
| A business rule spans both, and a failure of either must undo both | they have to be one transaction |
| Both are in the same context, the same database and the same process | otherwise one transaction is impossible |
| The aggregates are few and known up front (two warehouses, a buyer and a seller) | not "every order of the customer"; a wide transaction locks a lot and conflicts often |
| Nobody will ask to add more reactions to this action later | a handler that grows a line per new wish should have been an event |

Use an event handler when any of these holds:

| Sign | Handler kind |
|---|---|
| It is a consequence: "when X happens, also do Y" | sync if it must hold with the change or may veto it; async otherwise |
| Another context, module or team owns the second aggregate | async, usually through the outbox |
| More reactions are likely to follow | any; each reaction is its own handler |
| It may lag, is slow, or may fail on its own (stock, email, read models) | async |
| It must reach another process | async, through the outbox and the transport |

A sync event handler and a multi-aggregate command handler are both one transaction. Choose the command handler when the change is the *intent* of the action; choose the sync event handler when it is a *consequence* that must still be atomic, so the originator does not need to know about it.

## Common pitfalls

- **Fat handlers.** A command handler with `if` statements full of business rules is the commonest smell. The rule belongs on an aggregate, or in a domain service when it needs several entities.
- **Consequences inside the command handler.** A handler that counts the order on the customer, updates a report and sends an email has hidden three reactions in one place. They are events and event handlers; the handler keeps only what its name promises.
- **Events hiding a synchronous requirement.** If the business says "must happen together", an async handler that can fail alone is the wrong tool; use the command handler (if it is the intent) or a sync handler (if it is a consequence).
- **Wide transactions.** Loading and changing many aggregates in one command ("close every open order of the customer") locks a lot and conflicts often. One command per aggregate, sent from a loop or an event handler.
- **Event handlers with business logic inline.** An event handler sends a command and nothing more. The rule is in the aggregate or the domain service the command reaches.
- **One event, one reaction assumed.** Several handlers may react to the same event, in several aggregates' folders. None may depend on the order of the others unless an order is declared.
- **"Service" as a dumping ground.** A `*Service` named after an aggregate with forty operations is a god-class. The operations belong on the aggregate, the coordination in command handlers, the cross-entity logic in small domain services named after their concept.
- **A domain service that writes.** A service that calls create, update, upsert or delete splits the use case's persistence across two classes: nobody can see in one place what gets saved, and a write may run before a later rule throws. It mutates or returns; the handler saves.
- **Outside models leaking in.** A domain service that takes the tax provider's response type has let another system's model into Domain. Put an anti-corruption layer in between.
- **Raising an event after the write.** It stays on the aggregate and is never handled.
- **Publishing before the commit.** A rollback then leaves a message about something that never happened.

## The lookup table

Look up "I need to …" and read across: the layer, the block, and why. Roles are defined in [glossary.md](glossary.md); the blocks in [building-blocks.md](building-blocks.md).

| I need to … | Layer | Block | Why |
|---|---|---|---|
| check that an order has lines before completing | Domain | Aggregate operation | the aggregate enforces its own rules |
| check a required field is present | Application | Validator | shape checks need no data |
| check an email has a valid format | Application | Validator | shape check |
| check a value is within a range | Application | Validator | shape check |
| check a status transition is allowed | Domain | Aggregate operation | state rule |
| check a sku is unique | Domain | Domain service with a repository query; storage uniqueness as backstop | needs other entities; the database constraint becomes a duplicate failure |
| change another aggregate as a consequence of a change | Domain / Application | Domain event, then an event handler sending a command | the originator does not need to know |
| change two aggregates that the action is about (a transfer) | Application | Command handler loading both | the change to both is the intent; one transaction |
| decide something that needs several entities | Domain | Domain service | fits no single aggregate |
| read data a rule decides it needs (previous orders, a rate) | Domain | Read port called by a domain service | the handler would otherwise have to know the rule's data needs |
| save what a domain service changed or created | Application | Command handler | all writes and the commit live in the use case |
| ask another system something the domain needs to decide | Domain / Infrastructure | Anti-corruption layer, called by a domain service | the outside model stays out of Domain |
| make another aggregate change succeed or fail together with this one | Application | Sync event handler | runs inside the transaction and can veto |
| update stock after a line is added | Application | Async event handler | may lag, must not undo the line |
| send an email after a change | Application | Async event handler | slow and allowed to fail |
| send a push notification | Application | Async event handler | slow and allowed to fail |
| call a third-party web service that changes something | Infrastructure | An adapter declared in Domain or Shared, called from an async handler's command | a slow outside write never sits inside the transaction |
| read the current user | Host | Fill context.actor from the credential | there is no ambient user |
| read the correlation id | Host | Fill context.correlation from headers or generate one | hosts own the outside world |
| read the tenant | Host | Fill context.tenant from the credential or a header | hosts own the outside world |
| read the language of the caller | Host | Fill context.language from headers | hosts own the outside world |
| pass the user to the aggregate | Application | Command handler passes context.actor to the operation | the actor is data |
| stamp who created or changed a row | Domain | Aggregate operation and touch | audit is part of the aggregate |
| get the current time | Infrastructure | Clock declared in Shared, injected | never read the system clock in Domain |
| create a new id | Infrastructure | Id source declared in Shared, injected | never generate ids inline |
| save an aggregate | Application | Command handler calls the repository interface | the use case owns load, change, save |
| write the SQL or storage mapping | Infrastructure | Repository implementation and mapping | storage code stays out of Domain |
| begin or commit a transaction | Shared | Pipeline and unit of work | handlers never open transactions |
| make a command run in a transaction | Application | Mark the command transactional | the pipeline does the rest |
| choose which handler runs for a command | Shared | Dispatcher | callers name the command only |
| format a response for the client | Host | Host adapter and view mapping | hosts translate |
| map an aggregate to a result | Application | View mapping function in the handler's folder | one explicit function per view |
| return an error code and message to the client | Host | Host adapter from the failure's code and status | one failure shape everywhere |
| decide a retry after a failure | Infrastructure | Relay or async-handler retry policy | retry is delivery, not business |
| retry on a concurrency conflict | Infrastructure | Async-handler runner | a fresh transaction per attempt |
| write a query for a list screen | Infrastructure | Query on the read side, see queries page | reads need no aggregate |
| page, filter and sort a list | Infrastructure | Query implementation | storage concern |
| compute a total of an order | Domain | Aggregate operation or value object | business arithmetic |
| hold an address or money amount | Domain | Value object | compared by parts |
| model a line inside an order | Domain | Child entity of the aggregate | only exists inside the root |
| import a thousand rows | Application | One command per row from a loop in a host or Worker | each row has its own transaction |
| run a job on a timer | Host | Worker that sends a command | hosts turn triggers into commands |
| consume a message from a broker | Host | Worker consumer that sends a command | the message becomes a command |
| publish a message to a broker | Infrastructure | Event dispatcher (outbox relay) | delivery is infrastructure |
| make publishing survive a crash | Infrastructure | Outbox written in the command's transaction | one fact: changed and will be sent |
| ignore a message seen before | Infrastructure | Inbox check before the handler | at-least-once delivery |
| authorise that a user may do something | Application | A rule on the command or a policy the aggregate checks | needs the actor from context |
| authenticate a request | Host | Authentication step before parsing | who is calling is an outside-world fact |
| filter rows by tenant | Infrastructure | Tenant filter applied by the repository and queries | never by callers |
| log a command and its timing | Shared | Middleware | same for every command |
| translate a database error into a failure | Infrastructure | Exception translator | storage details stop here |
| seed reference data | Infrastructure | Seed step at startup | not a business operation |
| run a one-off manual test | Host | Console scenario | same wiring, no HTTP |
| name a stable failure code | Domain | Rule code on the failing operation | codes are part of the contract |
| version an event's shape | Domain | New event name or additive field | wire names are forever |

## How to use it

1. Find the closest row.
2. If no row fits, ask which layer's single job the thing serves ([layers.md](layers.md)).
3. If two layers seem to fit, the rule is in the wrong shape: a business decision goes to Domain, a coordination step to Application.
