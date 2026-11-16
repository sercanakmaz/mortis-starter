# Phase 1, step 09 — The bus: RabbitMQ, wire-compatible with MassTransit (knowledge only)

Needs: phase 1 steps 08. Size: large. Options: `transport-rabbitmq`. No C# is written, built or generated in this step.

## Goal

Our own small bus on `RabbitMQ.Client`, selected by `events.transport` = `rabbitmq`. It publishes outbox rows to RabbitMQ and consumes them in the Worker. A service that uses MassTransit 8 can consume what we publish and we can consume what it publishes.

## Read first

`docs/specs/messaging-wire-format.md` (binding; its *(verify)* items stay open in this phase and are settled by the interoperability tests in phase 2 step 03), the outbox from step 08, the MassTransit 8 source for the RabbitMQ transport and the JSON envelope serializer.

## Design

```
Infrastructure/Messaging/RabbitMq/
  RabbitMqConnection          one connection, channel per publish scope, publisher confirms
  MessageNameFormatter        type -> "<Namespace>:<TypeName>", urn list (type, bases, interfaces)
  MessageEnvelope             the JSON envelope; EnvelopeSerializer (System.Text.Json, camelCase, case-insensitive read, unknown members ignored)
  RabbitMqEventBus            IEventBus: declare exchange once per type, publish persistent, wait for confirm
  RabbitMqOutboxTransport     the outbox "send": build the envelope from the row, publish
  ReceiveEndpoint             queue + exchange + bindings, prefetch, consume loop, retry, _error, _skipped
  EventConsumer<TEvent>       restores the context, inbox check, runs BaseAsyncEventHandler<TEvent>
  IConsumer<T>, ConsumeContext<T>   for messages that are not our events
  RabbitMqTopologyOptions     per-type exchange type and name override
```

- The outbox marks a row `published` only after the broker confirms.
- A receive endpoint is declared explicitly: `services.AddReceiveEndpoint("shop-stock", endpoint => endpoint.Handle<LineAdded>())`. The Worker starts the endpoints; the Api only publishes.
- Retry: immediate retries (default 3) inside the consumer, then the message goes to `<queue>_error` with the fault headers. Redelivery by delay is out of scope.
- The inbox key is `(envelope.messageId, handler)`; a duplicate is acknowledged without running.
- Health: the connection recovers automatically; publishing while disconnected fails fast so the outbox retries.

## Deliverables

Knowledge only. Nothing here is compiled or run in this phase.

1. **Layer 1** (`skills/mortis-architect/references/architecture/` and `references/options/`): what the option is, its pseudo-code, its rules, when to choose it and what it costs.
2. **Layer 2 for C#** (`skills/mortis-scaffold/references/csharp/`): one page per type or concern, as contract, behaviour rules, a snippet for every member with a real body, and the tests to write with their snippets. Snippets, never whole files.
3. **Entries** in `manifest.json` (every file the option adds, and which existing files it changes), `kernel-shape.txt` and `naming.md`.
4. **Conformance checks** (`skills/mortis-scaffold/scripts/conformance/`): one check per item under "Behaviour to prove in phase 2", selected by `MORTIS.md`. Written now, first run in phase 2.
5. **The decision record** a generated project gets for this option, as an outline in `references/csharp/project-docs.md` (`0008-own-bus-compatible-with-masstransit.md`).
6. **A line in `docs/unverified.md`** for every page and check of this step: none of it has been compiled or run yet. Phase 2 removes the lines as it proves them.
7. `tests/check_docs.py` and `tests/check_installable.sh` pass.

## Behaviour to prove in phase 2

Each item becomes a conformance check or a test snippet now. None is run in this phase; phase 2 runs them on the generated project.

- [ ] Interoperability checks 1 to 5 pass against RabbitMQ.
- [ ] The example's `stock-flow` gives the same final stock with `transport` = `database` and `rabbitmq` (conformance).
- [ ] With the broker stopped, commands still succeed, rows stay `pending`, and they are published after the broker returns (conformance).
- [ ] A poisoned message lands in `<queue>_error` with `MT-Fault-*` headers and the queue keeps flowing.
- [ ] No type or package from MassTransit is referenced outside the test project (`grep` in `verify.sh`).

## Out of scope

Request/response, sagas, scheduling, delayed redelivery, other brokers, publishing `Fault<T>` events.

## Traps

- Declaring an exchange with different arguments than an existing one closes the channel with `PRECONDITION_FAILED`. Check 4 exists for this.
- MassTransit includes base types and interfaces in `messageType`. Our events derive from `EventBase`, so its URN is in the list; a MassTransit consumer of the concrete type is unaffected, but confirm it with the test.
- Existing MassTransit services often use the Newtonsoft serializer and set `topic` on exchanges. Both are covered by the spec; test both.
