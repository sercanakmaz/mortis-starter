# Events

How an event raised by an aggregate leaves the process, and over what.

## events.outbox

Whether the event is stored in the command's transaction before it is published. The three models and what each can lose are in `references/architecture/events.md`.

**By preset:** lean `none`, standard `after-command`, advanced `background`.

### none

**Choose when:** a prototype, or when losing an event after a crash is acceptable.

**Costs:** an event is lost if the process dies between the commit and the handler; nothing leaves the process.

**Rules:** R1.

### after-command

**Choose when:** events must not be lost and publishing right after the commit keeps latency low.

**Costs:** one more table, a sweeper for leftovers, a worker host, handlers that must be safe to run twice.

**Rules:** R1, R3.

### background

**Choose when:** events must not be lost and the request should not wait for publishing.

**Costs:** a delay of one polling interval before an event leaves; a relay to run.

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

**Choose when:** another process or service consumes the events.

**Costs:** a broker to run, exchanges and queues to manage, retry and dead-letter queues, a consumer host.

**Rules:** R2, R4.

Example:

```
| Key | Value | Why |
| events.transport | database | no broker to run yet |
```
