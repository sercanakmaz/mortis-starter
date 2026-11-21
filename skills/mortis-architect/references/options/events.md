# Events

How an event raised by an aggregate leaves the process, and over what.

## events.outbox

Whether the event is stored in the command's transaction before it is published. The three models and what each can lose are in `references/architecture/events.md`. The generated project contains only the chosen model's code.

**By preset:** lean `none`, standard `after-command`, advanced `background`.

### none

**Choose when:** a prototype, or when losing an event after a crash is acceptable.

**Costs:** an event is lost if the process dies between the commit and the handler; nothing leaves the process. Two small classes, no table, no worker.

**Rules:** R1.

### after-command

Publish after the command.

**Choose when:** events must not be lost and the caller should see the consequences when it gets its answer.

**Costs:** one table, a worker in a host for sweeping and retrying, the request waits for its own events' handlers after its commit, a migration.

**Rules:** R1, R3.

### background

Publish in the command.

**Choose when:** events must not be lost and the request should not wait for publishing.

**Costs:** one table, a worker in a host, a delay of up to one polling interval before an event is handled, a migration.

**Rules:** R1, R3.

Example:

```
| Key | Value | Why |
| events.outbox | after-command | events must not be lost |
```

## events.transport

What carries an event from the outbox to its consumers.

**By preset:** lean `in-process`, standard `database`, advanced `rabbitmq`.

### in-process

**Choose when:** no outbox, everything runs in one process.

**Costs:** nothing outside the process hears of an event.

**Rules:** R1.

### database

**Choose when:** an outbox and no broker; handlers run from the outbox table.

**Costs:** one process or a few; no broker features such as routing or fan-out to other services.

**Rules:** R1.

### rabbitmq

**Choose when:** another process or service consumes the events, including services built on MassTransit 8: our bus speaks its wire format both ways.

**Costs:** a broker to run; every event consumer must be safe to run twice (the broker can redeliver); receive endpoints configured in code in the Worker, one line per consumer; failed messages land in `<queue>_error` for an operator. No request/response, sagas, scheduling or delayed redelivery.

**Rules:** R2, R4.

Example:

```
| Key | Value | Why |
| events.transport | database | no broker to run yet |
```
